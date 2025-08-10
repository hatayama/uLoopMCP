using System;
using UnityEditor;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Unity 6 ConsoleWindowUtility API recreation for older Unity versions
    /// Provides event-driven console log monitoring and simple count retrieval
    /// </summary>
    public static class GenericConsoleWindowUtility
    {
        private static ConsoleLogRetriever logRetriever;
        private static int lastLogCount = 0;
        private static int lastErrorCount = 0;
        private static int lastWarningCount = 0;
        private static double lastCheckTime = 0;
        private static readonly double CheckInterval = 1.0; // Check every 1000ms (1 second)

        /// <summary>
        /// Event that fires when console logs change (Unity 6 compatible)
        /// </summary>
        public static event System.Action consoleLogsChanged;

        static GenericConsoleWindowUtility()
        {
            Initialize();
        }

        /// <summary>
        /// Initialize the utility and start monitoring
        /// </summary>
        private static void Initialize()
        {
            logRetriever = new ConsoleLogRetriever();
            EditorApplication.update += CheckForLogChanges;
        }

        /// <summary>
        /// Monitor for console log changes and fire events
        /// </summary>
        private static void CheckForLogChanges()
        {
            if (logRetriever == null) return;
            
            double currentTime = EditorApplication.timeSinceStartup;
            if (currentTime - lastCheckTime < CheckInterval) return;
            
            lastCheckTime = currentTime;

            try
            {
                // Get current counts
                GetConsoleLogCounts(out int errorCount, out int warningCount, out int logCount);
                
                // Check if anything changed
                if (errorCount != lastErrorCount || warningCount != lastWarningCount || logCount != lastLogCount)
                {
                    lastErrorCount = errorCount;
                    lastWarningCount = warningCount;
                    lastLogCount = logCount;
                    
                    // Fire the event
                    consoleLogsChanged?.Invoke();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Log monitoring failed: {ex.Message}");
                // Mark as failed and stop monitoring to prevent repeated errors
                logRetriever = null;
                EditorApplication.update -= CheckForLogChanges;
                
                // This is a critical failure - log monitoring is now broken
                throw new System.InvalidOperationException(
                    "Console log monitoring has failed and been disabled. Log retrieval functionality is compromised.", ex);
            }
        }

        /// <summary>
        /// Gets console log counts by type (Unity 6 compatible)
        /// </summary>
        /// <param name="errorCount">Number of error logs</param>
        /// <param name="warningCount">Number of warning logs</param>
        /// <param name="logCount">Number of info logs</param>
        public static void GetConsoleLogCounts(out int errorCount, out int warningCount, out int logCount)
        {
            errorCount = 0;
            warningCount = 0;
            logCount = 0;

            if (logRetriever == null) return;

            try
            {
                // Save current mask state
                int originalMask = logRetriever.GetCurrentMask();
                
                try
                {
                    // Temporarily set mask to show all log types to get accurate counts
                    logRetriever.SetMask(7); // Show all: Error(1) + Warning(2) + Log(4) = 7
                    
                    // Use Unity's internal GetCount method which respects Console clear state
                    int totalCount = logRetriever.GetLogCount();
                    
                    if (totalCount == 0)
                    {
                        // Console is empty, all counts are 0
                        return;
                    }
                    
                    // Get all logs and count by type
                    var allLogs = logRetriever.GetAllLogs();
                    
                    foreach (LogEntryDto log in allLogs)
                    {
                        switch (log.LogType)
                        {
                            case var s when string.Equals(s, McpLogType.Error, StringComparison.OrdinalIgnoreCase):
                                errorCount++;
                                break;
                            case var s when string.Equals(s, McpLogType.Warning, StringComparison.OrdinalIgnoreCase):
                                warningCount++;
                                break;
                            case var s when string.Equals(s, McpLogType.Log, StringComparison.OrdinalIgnoreCase):
                                logCount++;
                                break;
                        }
                    }
                }
                finally
                {
                    // Always restore original mask
                    logRetriever.SetMask(GetSimpleMaskFromUnityMask(originalMask));
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Console log count retrieval failed: {ex.Message}");
                // Don't suppress this exception - caller needs to know count retrieval failed
                throw new System.InvalidOperationException(
                    "Failed to retrieve console log counts. Console monitoring may be compromised.", ex);
            }
        }

        /// <summary>
        /// Clears the Unity Console (Unity 6 compatible)
        /// </summary>
        public static void ClearConsole()
        {
            try
            {
                var _logEntriesType = System.Reflection.Assembly.GetAssembly(typeof(EditorWindow))
                    .GetType("UnityEditor.LogEntries");
                
                var clearMethod = _logEntriesType.GetMethod("Clear", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                
                if (clearMethod != null)
                {
                    clearMethod.Invoke(null, null);
                    
                    // Reset our tracking counts
                    lastLogCount = 0;
                    lastErrorCount = 0;
                    lastWarningCount = 0;
                    // Fire the change event
                    consoleLogsChanged?.Invoke();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Console clear operation failed: {ex.Message}");
                // Don't suppress this exception - caller needs to know clear failed
                throw new System.InvalidOperationException(
                    "Failed to clear Unity console. Console operations may be compromised.", ex);
            }
        }

        /// <summary>
        /// Gets all console logs (convenience method)
        /// </summary>
        /// <returns>List of all log entries</returns>
        public static System.Collections.Generic.List<LogEntryDto> GetAllLogs()
        {
            if (logRetriever == null) 
                return new System.Collections.Generic.List<LogEntryDto>();
            
            try
            {
                return logRetriever.GetAllLogs();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to retrieve console logs: {ex.Message}");
                // Don't suppress this exception - caller needs to know retrieval failed
                throw new System.InvalidOperationException(
                    "Console log retrieval failed. Log data is not available.", ex);
            }
        }

        /// <summary>
        /// Gets current console mask state
        /// </summary>
        /// <returns>Current console mask</returns>
        public static int GetCurrentMask()
        {
            if (logRetriever == null) return 0;
            
            try
            {
                return logRetriever.GetCurrentMask();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Console mask retrieval failed: {ex.Message}");
                // Don't suppress this exception - caller needs to know mask retrieval failed
                throw new System.InvalidOperationException(
                    "Failed to get console mask state. Console operations may be compromised.", ex);
            }
        }

        /// <summary>
        /// Sets console mask state
        /// </summary>
        /// <param name="mask">Mask value to set</param>
        public static void SetMask(int mask)
        {
            if (logRetriever == null) return;
            
            try
            {
                logRetriever.SetMask(mask);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Console mask setting failed: {ex.Message}");
                // Don't suppress this exception - caller needs to know mask setting failed
                throw new System.InvalidOperationException(
                    $"Failed to set console mask to {mask}. Console operations may be compromised.", ex);
            }
        }

        /// <summary>
        /// Converts Unity internal mask back to simple mask format
        /// </summary>
        /// <param name="unityMask">Unity's internal mask value</param>
        /// <returns>Simple mask (1=Error, 2=Warning, 4=Log)</returns>
        private static int GetSimpleMaskFromUnityMask(int unityMask)
        {
            int simpleMask = 0;
            
            if ((unityMask & 0x200) != 0) // Error bit
                simpleMask |= 1;
            
            if ((unityMask & 0x100) != 0) // Warning bit
                simpleMask |= 2;
                
            if ((unityMask & 0x80) != 0) // Log bit
                simpleMask |= 4;
            
            return simpleMask;
        }

        /// <summary>
        /// Cleanup when domain reloads
        /// </summary>
        [UnityEditor.InitializeOnLoadMethod]
        private static void OnDomainReload()
        {
            EditorApplication.update -= CheckForLogChanges;
            Initialize();
        }
    }
}