using UnityEngine;
#if UNITY_6000_0_OR_NEWER
using UnityEditor;
#endif

namespace io.github.hatayama.uMCP
{
    /// <summary>
    /// Universal Console Utility wrapper
    /// Uses Unity 6+ standard API when available, fallback to custom implementation for older versions
    /// </summary>
    public static class ConsoleUtility
    {
        /// <summary>
        /// Event that fires when console logs change (Universal API)
        /// </summary>
        public static event System.Action consoleLogsChanged
        {
            add
            {
#if UNITY_6000_0_OR_NEWER
                ConsoleWindowUtility.consoleLogsChanged += value;
#else
                GenelicConsoleWindowUtility.consoleLogsChanged += value;
#endif
            }
            remove
            {
#if UNITY_6000_0_OR_NEWER
                ConsoleWindowUtility.consoleLogsChanged -= value;
#else
                GenelicConsoleWindowUtility.consoleLogsChanged -= value;
#endif
            }
        }

        /// <summary>
        /// Gets console log counts by type (Universal API)
        /// </summary>
        /// <param name="errorCount">Number of error logs</param>
        /// <param name="warningCount">Number of warning logs</param>
        /// <param name="logCount">Number of info logs</param>
        public static void GetConsoleLogCounts(out int errorCount, out int warningCount, out int logCount)
        {
#if UNITY_6000_0_OR_NEWER
            ConsoleWindowUtility.GetConsoleLogCounts(out errorCount, out warningCount, out logCount);
#else
            GenelicConsoleWindowUtility.GetConsoleLogCounts(out errorCount, out warningCount, out logCount);
#endif
        }

        /// <summary>
        /// Gets Unity version info for debugging
        /// </summary>
        /// <returns>Unity version and API info</returns>
        public static string GetVersionInfo()
        {
#if UNITY_6000_0_OR_NEWER
            return $"Unity {Application.unityVersion} - Using Unity 6+ Standard ConsoleWindowUtility API";
#else
            return $"Unity {Application.unityVersion} - Using Custom LogByReflection ConsoleWindowUtility API";
#endif
        }

        /// <summary>
        /// Checks if Unity 6+ standard API is being used
        /// </summary>
        /// <returns>True if using Unity 6+ standard API</returns>
        public static bool IsUsingStandardAPI()
        {
#if UNITY_6000_0_OR_NEWER
            return true;
#else
            return false;
#endif
        }
    }
}