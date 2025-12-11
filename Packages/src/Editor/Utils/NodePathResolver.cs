using System.Diagnostics;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Utility class to verify Node.js availability on the system.
    /// Uses NodeEnvironmentResolver for detection with fallback strategy.
    /// </summary>
    public static class NodePathResolver
    {
        private static string _cachedNodePath;
        private static bool _cacheInitialized;

        private const int PROCESS_TIMEOUT_MS = 5000;

        /// <summary>
        /// Gets the Node.js executable path if available.
        /// Returns full path to node executable, or null if not found.
        /// </summary>
        public static string GetNodeExecutablePath()
        {
            if (_cacheInitialized)
            {
                return _cachedNodePath;
            }

            _cachedNodePath = ResolveNodePath();
            _cacheInitialized = true;
            return _cachedNodePath;
        }

        /// <summary>
        /// Checks if Node.js is available on the system.
        /// </summary>
        public static bool IsNodeAvailable()
        {
            return !string.IsNullOrEmpty(GetNodeExecutablePath());
        }

        private static string ResolveNodePath()
        {
            string nodePath = NodeEnvironmentResolver.FindNodePath();
            if (string.IsNullOrEmpty(nodePath))
            {
                return null;
            }

            if (ValidateNodeExecutable(nodePath))
            {
                return nodePath;
            }

            return null;
        }

        private static bool ValidateNodeExecutable(string nodePath)
        {
            string command;
            string arguments;

            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                command = "cmd.exe";
                arguments = $"/c \"{nodePath}\" -v";
            }
            else
            {
                command = nodePath;
                arguments = "-v";
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    return false;
                }

                string output = process.StandardOutput.ReadToEnd().Trim();
                process.StandardError.ReadToEnd();

                if (!process.WaitForExit(PROCESS_TIMEOUT_MS))
                {
                    process.Kill();
                    return false;
                }

                return process.ExitCode == 0 && output.StartsWith("v");
            }
        }

        /// <summary>
        /// Clears the cached Node.js path (for testing purposes).
        /// </summary>
        internal static void ClearCache()
        {
            _cachedNodePath = null;
            _cacheInitialized = false;
        }
    }
}
