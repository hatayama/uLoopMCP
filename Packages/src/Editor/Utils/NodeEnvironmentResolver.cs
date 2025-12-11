using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Utility class for detecting Node.js and npm executables across various installation methods.
    /// Supports standard installations and version managers (nvm, volta, asdf, fnm).
    /// </summary>
    public static class NodeEnvironmentResolver
    {
        private const int PROCESS_TIMEOUT_MS = 5000;

        private static readonly string[] COMMON_BIN_PATHS = {
            "/usr/local/bin",
            "/opt/homebrew/bin",
            "/usr/bin"
        };

        private static readonly (string basePath, string binSubPath)[] VERSION_MANAGERS = {
            (".nvm/versions/node", "bin"),
            (".volta/tools/image/node", "bin"),
            (".asdf/installs/nodejs", "bin"),
            (".local/share/fnm/node-versions", "installation/bin"),
        };

        /// <summary>
        /// Finds the Node.js executable path using fallback strategy.
        /// Order: which command -> common paths -> version manager paths
        /// </summary>
        public static string FindNodePath()
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return FindExecutableWindows("node");
            }

            return FindExecutableUnix("node");
        }

        /// <summary>
        /// Finds the npm executable path using fallback strategy.
        /// Order: which command -> common paths -> version manager paths
        /// </summary>
        public static string FindNpmPath()
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return FindExecutableWindows("npm");
            }

            return FindExecutableUnix("npm");
        }

        /// <summary>
        /// Sets up the PATH environment variable for a process to include Node.js paths.
        /// </summary>
        public static void SetupEnvironmentPath(ProcessStartInfo startInfo, string executablePath)
        {
            if (string.IsNullOrEmpty(executablePath))
            {
                return;
            }

            string executableDir = Path.GetDirectoryName(executablePath);
            string currentPath = System.Environment.GetEnvironmentVariable("PATH") ?? "";

            List<string> additionalPaths = new List<string>();

            if (!string.IsNullOrEmpty(executableDir))
            {
                additionalPaths.Add(executableDir);
            }

            foreach (string path in COMMON_BIN_PATHS)
            {
                if (Directory.Exists(path) && !additionalPaths.Contains(path))
                {
                    additionalPaths.Add(path);
                }
            }

            AddVersionManagerPaths(additionalPaths);

            string separator = Application.platform == RuntimePlatform.WindowsEditor ? ";" : ":";
            string newPath = string.Join(separator, additionalPaths) + separator + currentPath;

            startInfo.EnvironmentVariables["PATH"] = newPath;
        }

        private static string FindExecutableUnix(string executableName)
        {
            string path = TryWhichCommand(executableName);
            if (!string.IsNullOrEmpty(path))
            {
                return path;
            }

            path = SearchCommonPaths(executableName);
            if (!string.IsNullOrEmpty(path))
            {
                return path;
            }

            path = SearchVersionManagerPaths(executableName);
            if (!string.IsNullOrEmpty(path))
            {
                return path;
            }

            return null;
        }

        private static string FindExecutableWindows(string executableName)
        {
            string path = TryWhereCommand(executableName);
            if (!string.IsNullOrEmpty(path))
            {
                return path;
            }

            path = SearchVersionManagerPaths(executableName);
            if (!string.IsNullOrEmpty(path))
            {
                return path;
            }

            return null;
        }

        private static string TryWhichCommand(string executableName)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = executableName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            return ExecuteAndGetOutput(startInfo);
        }

        private static string TryWhereCommand(string executableName)
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
                if (lines.Length > 0)
                {
                    return lines[0].Trim();
                }
            }

            return null;
        }

        private static string ExecuteAndGetOutput(ProcessStartInfo startInfo)
        {
            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    return null;
                }

                string output = process.StandardOutput.ReadToEnd().Trim();
                process.StandardError.ReadToEnd();

                if (!process.WaitForExit(PROCESS_TIMEOUT_MS))
                {
                    process.Kill();
                    return null;
                }

                if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    return output;
                }
            }

            return null;
        }

        private static string SearchCommonPaths(string executableName)
        {
            foreach (string binPath in COMMON_BIN_PATHS)
            {
                string fullPath = Path.Combine(binPath, executableName);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return null;
        }

        private static string SearchVersionManagerPaths(string executableName)
        {
            string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);

            foreach ((string basePath, string binSubPath) in VERSION_MANAGERS)
            {
                string fullBasePath = Path.Combine(home, basePath);
                if (!Directory.Exists(fullBasePath))
                {
                    continue;
                }

                string[] versionDirs = Directory.GetDirectories(fullBasePath);
                System.Array.Sort(versionDirs);
                System.Array.Reverse(versionDirs);

                foreach (string versionDir in versionDirs)
                {
                    string executablePath = Path.Combine(versionDir, binSubPath, executableName);
                    if (File.Exists(executablePath))
                    {
                        return executablePath;
                    }
                }
            }

            return null;
        }

        private static void AddVersionManagerPaths(List<string> paths)
        {
            string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);

            foreach ((string basePath, string binSubPath) in VERSION_MANAGERS)
            {
                string fullBasePath = Path.Combine(home, basePath);
                if (!Directory.Exists(fullBasePath))
                {
                    continue;
                }

                string[] versionDirs = Directory.GetDirectories(fullBasePath);
                foreach (string versionDir in versionDirs)
                {
                    string binPath = Path.Combine(versionDir, binSubPath);
                    if (Directory.Exists(binPath) && !paths.Contains(binPath))
                    {
                        paths.Add(binPath);
                    }
                }
            }
        }
    }
}
