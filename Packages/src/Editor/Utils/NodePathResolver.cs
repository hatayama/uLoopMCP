using System.Diagnostics;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Utility class to verify Node.js availability on the system.
    /// Uses 'node -v' to check if Node.js is accessible via PATH.
    /// </summary>
    public static class NodePathResolver
    {
        private static string _cachedNodePath;
        private static bool _cacheInitialized;

        private const int PROCESS_TIMEOUT_MS = 5000;

        /// <summary>
        /// Gets the Node.js command name if available on PATH.
        /// Returns "node" if available, null if Node.js is not found.
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
            string command;
            string arguments;

            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                command = "cmd.exe";
                arguments = "/c node -v";
            }
            else
            {
                command = "/bin/bash";
                arguments = "-c \"node -v\"";
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
                    return null;
                }

                string output = process.StandardOutput.ReadToEnd().Trim();
                process.StandardError.ReadToEnd();

                if (!process.WaitForExit(PROCESS_TIMEOUT_MS))
                {
                    process.Kill();
                    return null;
                }

                // node -v returns something like "v20.10.0"
                if (process.ExitCode == 0 && output.StartsWith("v"))
                {
                    return McpConstants.NODE_COMMAND;
                }
            }

            return null;
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
