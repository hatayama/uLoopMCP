using System;
using UnityEditor;
using UnityEngine;
using System.Text.RegularExpressions;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// A class that provides a general-purpose static API for retrieving console logs.
    /// Uses ConsoleLogRetriever to access Unity's console logs directly via reflection.
    /// </summary>
    [InitializeOnLoad]
    public static class LogGetter
    {
        private static readonly ConsoleLogRetriever LogRetriever;
        
        static LogGetter()
        {
            LogRetriever = new ConsoleLogRetriever();
        }

        /// <summary>
        /// Converts McpLogType to Unity's LogType
        /// </summary>
        /// <param name="mcpLogType">MCP log type</param>
        /// <returns>Corresponding Unity LogType</returns>
        private static LogType ConvertMcpLogTypeToLogType(string mcpLogType)
        {
            return mcpLogType switch
            {
                var s when string.Equals(s, McpLogType.Error, StringComparison.OrdinalIgnoreCase) => LogType.Error,
                var s when string.Equals(s, McpLogType.Warning, StringComparison.OrdinalIgnoreCase) => LogType.Warning,
                var s when string.Equals(s, McpLogType.Log, StringComparison.OrdinalIgnoreCase) => LogType.Log,
                _ => LogType.Log  // Default for unknown types, None will be handled separately
            };
        }

        /// <summary>
        /// Retrieves all console logs and returns them as a LogDisplayDto.
        /// </summary>
        /// <returns>The retrieved log data.</returns>
        public static LogDisplayDto GetAllConsoleLogs()
        {
            System.Collections.Generic.List<LogEntryDto> logEntries = LogRetriever.GetAllLogs();
            return new LogDisplayDto(logEntries.ToArray(), logEntries.Count);
        }

        /// <summary>
        /// Directly retrieves an array of console log entries.
        /// </summary>
        /// <returns>An array of log entries.</returns>
        public static LogEntryDto[] GetConsoleLogEntries()
        {
            return LogRetriever.GetAllLogs().ToArray();
        }

        /// <summary>
        /// Filters and retrieves console logs by log type.
        /// </summary>
        /// <param name="logType">The log type to filter by (if "All", all types are retrieved).</param>
        /// <returns>The filtered log data.</returns>
        public static LogDisplayDto GetConsoleLogsByType(string logType)
        {
            System.Collections.Generic.List<LogEntryDto> allEntries;
            
            if (string.Equals(logType, McpLogType.All, StringComparison.OrdinalIgnoreCase))
            {
                allEntries = LogRetriever.GetAllLogs();
            }
            else
            {
                // Convert string logType to LogType for ConsoleLogRetriever
                UnityEngine.LogType unityLogType = ConvertMcpLogTypeToLogType(logType);
                allEntries = LogRetriever.GetLogsByType(unityLogType);
            }
            
            return new LogDisplayDto(allEntries.ToArray(), allEntries.Count);
        }

        /// <summary>
        /// Filters and retrieves console logs by log type and searches within message content.
        /// </summary>
        /// <param name="logType">The log type to filter by (if "All", all types are included).</param>
        /// <param name="searchText">The text to search for within messages (if null or empty, no search is performed).</param>
        /// <param name="useRegex">Whether to use regular expression for search.</param>
        /// <param name="searchInStackTrace">Whether to search within stack trace as well.</param>
        /// <returns>The filtered log data.</returns>
        public static LogDisplayDto SearchConsoleLogs(string logType, string searchText, bool useRegex, bool searchInStackTrace)
        {
            // Get logs based on type
            System.Collections.Generic.List<LogEntryDto> allEntries;
            if (string.Equals(logType, McpLogType.All, StringComparison.OrdinalIgnoreCase))
            {
                allEntries = LogRetriever.GetAllLogs();
            }
            else
            {
                UnityEngine.LogType unityLogType = ConvertMcpLogTypeToLogType(logType);
                allEntries = LogRetriever.GetLogsByType(unityLogType);
            }
            
            // Filter by search text if provided
            if (!string.IsNullOrEmpty(searchText))
            {
                if (useRegex)
                {
                    Regex regex = new Regex(searchText);
                    allEntries = allEntries.FindAll(entry => 
                    {
                        bool messageMatch = regex.IsMatch(entry.Message);
                        bool stackTraceMatch = searchInStackTrace && !string.IsNullOrEmpty(entry.StackTrace) && regex.IsMatch(entry.StackTrace);
                        return messageMatch || stackTraceMatch;
                    });
                }
                else
                {
                    allEntries = allEntries.FindAll(entry => 
                    {
                        bool messageMatch = entry.Message.IndexOf(searchText, System.StringComparison.OrdinalIgnoreCase) >= 0;
                        bool stackTraceMatch = searchInStackTrace && !string.IsNullOrEmpty(entry.StackTrace) && 
                                             entry.StackTrace.IndexOf(searchText, System.StringComparison.OrdinalIgnoreCase) >= 0;
                        return messageMatch || stackTraceMatch;
                    });
                }
            }
            
            return new LogDisplayDto(allEntries.ToArray(), allEntries.Count);
        }

        /// <summary>
        /// Gets the total number of console logs.
        /// </summary>
        /// <returns>The total number of logs.</returns>
        public static int GetConsoleLogCount()
        {
            return LogRetriever.GetLogCount();
        }

        /// <summary>
        /// Clears the logs of the custom log manager.
        /// </summary>
        public static void ClearCustomLogs()
        {
            // This method is no longer needed since we're using ConsoleLogRetriever
            // Console logs are managed by Unity itself
        }
    }
}