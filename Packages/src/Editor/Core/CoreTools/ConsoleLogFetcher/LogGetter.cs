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
        private static LogType ConvertMcpLogTypeToLogType(McpLogType mcpLogType)
        {
            return mcpLogType switch
            {
                McpLogType.Error => LogType.Error,
                McpLogType.Warning => LogType.Warning,
                McpLogType.Log => LogType.Log,
                _ => LogType.Log
            };
        }

        /// <summary>
        /// Retrieves console logs and returns them as a LogDisplayDto.
        /// </summary>
        /// <returns>The retrieved log data.</returns>
        public static LogDisplayDto GetConsoleLog()
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
        /// Filters and retrieves console logs based on specified conditions.
        /// </summary>
        /// <param name="logType">The log type to filter by (if null, all types are retrieved).</param>
        /// <returns>The filtered log data.</returns>
        public static LogDisplayDto GetConsoleLog(McpLogType logType)
        {
            System.Collections.Generic.List<LogEntryDto> allEntries;
            
            if (logType == McpLogType.All)
            {
                allEntries = LogRetriever.GetAllLogs();
            }
            else
            {
                // Convert McpLogType to LogType for ConsoleLogRetriever
                UnityEngine.LogType unityLogType = ConvertMcpLogTypeToLogType(logType);
                allEntries = LogRetriever.GetLogsByType(unityLogType);
            }
            
            return new LogDisplayDto(allEntries.ToArray(), allEntries.Count);
        }

        /// <summary>
        /// Filters and retrieves console logs by log type and message content.
        /// </summary>
        /// <param name="logType">The log type to filter by (if null or "All", all types are included).</param>
        /// <param name="searchText">The text to search for within messages (if null or empty, no search is performed).</param>
        /// <param name="useRegex">Whether to use regular expression for search.</param>
        /// <param name="searchInStackTrace">Whether to search within stack trace as well.</param>
        /// <returns>The filtered log data.</returns>
        public static LogDisplayDto GetConsoleLog(McpLogType logType, string searchText, bool useRegex, bool searchInStackTrace)
        {
            // Get logs based on type
            System.Collections.Generic.List<LogEntryDto> allEntries;
            if (logType == McpLogType.All)
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