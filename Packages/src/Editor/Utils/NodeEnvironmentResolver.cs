using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Utility class for detecting Node.js and npm executables.
    /// Uses login shell to resolve PATH, matching the user's terminal environment.
    /// </summary>
    public static class NodeEnvironmentResolver
    {
        private const int PROCESS_TIMEOUT_MS = 5000;

        public static string FindNodePath()
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return FindExecutableWindows("node");
            }

            return FindExecutableUnix("node");
        }

        public static IEnumerable<string> FindAllNodePaths()
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return FindAllExecutablePathsWindows("node");
            }

            return FindAllExecutablePathsUnix("node");
        }

        /// <summary>
        /// Finds an executable path using platform-appropriate resolution.
        /// On Windows, resolves .cmd shims via 'where' command.
        /// On Unix, resolves via login shell 'which' command.
        /// </summary>
        public static string FindExecutablePath(string executableName)
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return FindExecutableWindows(executableName);
            }

            return FindExecutableUnix(executableName);
        }

        public static string FindNpmPath()
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return FindExecutableWindows("npm");
            }

            return FindExecutableUnix("npm");
        }

        /// <summary>
        /// Sets up the PATH environment variable for a process.
        /// On Unix, retrieves PATH from login shell to match the user's terminal environment.
        /// </summary>
        public static void SetupEnvironmentPath(ProcessStartInfo startInfo, string executablePath)
        {
            if (string.IsNullOrEmpty(executablePath))
            {
                return;
            }

            string executableDir = Path.GetDirectoryName(executablePath);
            string loginShellPath = GetLoginShellPath();
            string basePath = !string.IsNullOrEmpty(loginShellPath)
                ? loginShellPath
                : (System.Environment.GetEnvironmentVariable("PATH") ?? "");

            string separator = Application.platform == RuntimePlatform.WindowsEditor ? ";" : ":";

            if (!string.IsNullOrEmpty(executableDir))
            {
                startInfo.EnvironmentVariables["PATH"] = executableDir + separator + basePath;
            }
            else
            {
                startInfo.EnvironmentVariables["PATH"] = basePath;
            }
        }

        private static string FindExecutableUnix(string executableName)
        {
            return TryWhichCommand(executableName);
        }

        private static string FindExecutableWindows(string executableName)
        {
            return TryWhereCommand(executableName);
        }

        // Only returns the login shell's which result — no hardcoded fallback paths.
        // Scanning version-manager directories directly caused false positives (e.g. detecting
        // an uninstalled CLI version), which was the original bug this PR fixes.
        private static IEnumerable<string> FindAllExecutablePathsUnix(string executableName)
        {
            string whichPath = TryWhichCommand(executableName);
            if (!string.IsNullOrEmpty(whichPath))
            {
                yield return whichPath;
            }
        }

        private static IEnumerable<string> FindAllExecutablePathsWindows(string executableName)
        {
            string[] wherePaths = TryWhereCommandAll(executableName);
            if (wherePaths != null)
            {
                foreach (string path in wherePaths)
                {
                    if (!string.IsNullOrEmpty(path))
                    {
                        yield return path;
                    }
                }
            }
        }

        // Interactive login shell (-l -i) loads .zprofile and .zshrc/.bashrc, matching the user's terminal
        // Markers isolate which output from shell startup banners; ExtractAbsolutePathLine filters alias text
        private static string TryWhichCommand(string executableName)
        {
            string shell = GetUserShell();
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = shell,
                Arguments = "-l -i -c \"echo " + WHICH_START_MARKER + "; which " + executableName + "; echo " + WHICH_END_MARKER + "\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            string output = ExecuteAndGetOutput(startInfo);
            string block = ExtractBetweenMarkers(output, WHICH_START_MARKER, WHICH_END_MARKER);
            return ExtractAbsolutePathLine(block);
        }

        private static string TryWhereCommand(string executableName)
        {
            string[] paths = TryWhereCommandAll(executableName);
            return paths != null && paths.Length > 0 ? paths[0] : null;
        }

        private static string[] TryWhereCommandAll(string executableName)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c where {executableName}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            string output = ExecuteAndGetOutput(startInfo);
            if (!string.IsNullOrEmpty(output))
            {
                string[] lines = output.Split('\n');
                List<string> result = new List<string>();
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        result.Add(trimmed);
                    }
                }
                return result.Count > 0 ? result.ToArray() : null;
            }

            return null;
        }

        private static string ExecuteAndGetOutput(ProcessStartInfo startInfo)
        {
            UnityEngine.Debug.Assert(startInfo != null, "startInfo must not be null");
            UnityEngine.Debug.Assert(startInfo.RedirectStandardOutput, "RedirectStandardOutput must be true");
            UnityEngine.Debug.Assert(startInfo.RedirectStandardError, "RedirectStandardError must be true");

            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    return null;
                }

                System.Text.StringBuilder stdoutBuilder = new System.Text.StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        stdoutBuilder.AppendLine(e.Data);
                    }
                };
                process.ErrorDataReceived += (sender, e) => { };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(PROCESS_TIMEOUT_MS))
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit(1000);
                    }

                    return null;
                }

                // Parameterless WaitForExit flushes async output buffers
                process.WaitForExit();

                string output = stdoutBuilder.ToString().Trim();
                if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    return output;
                }
            }

            return null;
        }

        private const string PATH_START_MARKER = "__PATH_START__";
        private const string PATH_END_MARKER = "__PATH_END__";
        private const string WHICH_START_MARKER = "__WHICH_START__";
        private const string WHICH_END_MARKER = "__WHICH_END__";

        // Uses markers to extract PATH value, ignoring any banner/echo output from shell startup files
        private static string GetLoginShellPath()
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return null;
            }

            string shell = GetUserShell();
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = shell,
                Arguments = "-l -i -c \"echo " + PATH_START_MARKER + "; printenv PATH; echo " + PATH_END_MARKER + "\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            string output = ExecuteAndGetOutput(startInfo);
            return ExtractBetweenMarkers(output, PATH_START_MARKER, PATH_END_MARKER);
        }

        internal static string ExtractBetweenMarkers(string output, string startMarker, string endMarker)
        {
            if (string.IsNullOrEmpty(output))
            {
                return null;
            }

            int startIndex = output.IndexOf(startMarker);
            int endIndex = output.IndexOf(endMarker);
            if (startIndex < 0 || endIndex < 0 || endIndex <= startIndex)
            {
                return null;
            }

            return output.Substring(startIndex + startMarker.Length, endIndex - startIndex - startMarker.Length).Trim();
        }

        internal static string ExtractAbsolutePathLine(string block)
        {
            if (string.IsNullOrEmpty(block))
            {
                return null;
            }

            string[] lines = block.Split('\n');
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (!string.IsNullOrEmpty(line) && Path.IsPathRooted(line))
                {
                    return line;
                }
            }

            return null;
        }

        private static string GetUserShell()
        {
            string shell = System.Environment.GetEnvironmentVariable("SHELL");
            return string.IsNullOrEmpty(shell) ? "/bin/sh" : shell;
        }
    }
}
