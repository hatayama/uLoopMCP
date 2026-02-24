using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Pre-flight diagnostics and error classification for npm global install.
    /// Windows npm installs often fail silently due to directory permissions
    /// (e.g. C:\Program Files\nodejs requires admin elevation).
    /// </summary>
    public static class NpmInstallDiagnostics
    {
        private const int PREFIX_CHECK_TIMEOUT_MS = 5000;

        private const string ERROR_PATTERN_EPERM = "EPERM";
        private const string ERROR_PATTERN_EACCES = "EACCES";
        private const string ERROR_PATTERN_OPERATION_NOT_PERMITTED = "operation not permitted";

        /// <summary>
        /// Returns the npm global prefix path by running `npm prefix -g`, or null if resolution fails.
        /// </summary>
        public static string GetGlobalPrefix(string npmPath)
        {
            UnityEngine.Debug.Assert(!string.IsNullOrEmpty(npmPath), "npmPath must not be null or empty");

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = npmPath,
                Arguments = "prefix -g",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            NodeEnvironmentResolver.SetupEnvironmentPath(startInfo, NodeEnvironmentResolver.FindNodePath());

            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    return null;
                }

                StringBuilder stdoutBuilder = new StringBuilder();

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

                if (!process.WaitForExit(PREFIX_CHECK_TIMEOUT_MS))
                {
                    // Process may exit between WaitForExit(timeout) and Kill() (TOCTOU race)
                    try
                    {
                        process.Kill();
                        process.WaitForExit(1000);
                    }
                    catch (System.InvalidOperationException)
                    {
                        UnityEngine.Debug.Log("Process already exited before Kill() was called");
                    }

                    return null;
                }

                process.WaitForExit();

                string output = stdoutBuilder.ToString().Trim();
                if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    return output;
                }
            }

            return null;
        }

        /// <summary>
        /// Checks whether the npm global prefix directory is writable by creating and deleting a temp file.
        /// Returns true if writable, false otherwise.
        /// </summary>
        public static bool IsGlobalPrefixWritable(string globalPrefixPath)
        {
            UnityEngine.Debug.Assert(!string.IsNullOrEmpty(globalPrefixPath), "globalPrefixPath must not be null or empty");

            try
            {
                // npm creates the prefix directory on first install, so a non-existent directory
                // does not mean non-writable — test by creating it (no-op if it already exists)
                if (!Directory.Exists(globalPrefixPath))
                {
                    Directory.CreateDirectory(globalPrefixPath);
                }

                string testFilePath = Path.Combine(
                    globalPrefixPath,
                    ".uloop_write_test_" + System.Guid.NewGuid().ToString("N").Substring(0, 8));
                File.WriteAllText(testFilePath, "");
                File.Delete(testFilePath);
                return true;
            }
            // OS permission denial cannot be pre-checked with assertions; must attempt the operation
            catch (System.UnauthorizedAccessException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
        }

        /// <summary>
        /// Analyzes npm stderr output and returns a user-friendly guidance message.
        /// Returns null if no specific pattern is matched (caller falls back to raw error).
        /// </summary>
        public static string ClassifyInstallError(string stderrOutput)
        {
            if (string.IsNullOrEmpty(stderrOutput))
            {
                return null;
            }

            if (!IsPermissionError(stderrOutput))
            {
                return null;
            }

            return "npm does not have permission to write to the global directory.\n\n"
                 + "Solutions:\n"
                 + "1. Open a terminal as Administrator and run the manual command below\n"
                 + "2. Or change npm's global prefix to a user-writable directory:\n"
                 + "   npm config set prefix \"%USERPROFILE%\\.npm-global\"";
        }

        internal static bool IsPermissionError(string stderrOutput)
        {
            UnityEngine.Debug.Assert(stderrOutput != null, "stderrOutput must not be null");

            return stderrOutput.Contains(ERROR_PATTERN_EPERM)
                || stderrOutput.Contains(ERROR_PATTERN_EACCES)
                || stderrOutput.IndexOf(ERROR_PATTERN_OPERATION_NOT_PERMITTED, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
