using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// GetLogs tool handler - Type-safe implementation using Schema and Response
    /// Retrieves Unity console logs with filtering options
    /// </summary>
    [McpTool(Description = "Retrieve logs from Unity Console")]
    public class GetLogsTool : AbstractUnityTool<GetLogsSchema, GetLogsResponse>
    {
        public override string ToolName => "get-logs";



        protected override Task<GetLogsResponse> ExecuteAsync(GetLogsSchema parameters, CancellationToken cancellationToken)
        {
            // Check for cancellation before starting
            cancellationToken.ThrowIfCancellationRequested();
            
            // Type-safe parameter access - no more string parsing!
            McpLogType logType = parameters.LogType;
            int maxCount = parameters.MaxCount;
            string searchText = parameters.SearchText;
            bool useRegex = parameters.UseRegex;
            bool searchInStackTrace = parameters.SearchInStackTrace;
            bool includeStackTrace = parameters.IncludeStackTrace;
            
            // Convert enum to string for LogGetter
            string logTypeString = logType.ToString();
            
            // Get Unity Console Log using the LogGetter class.
            LogDisplayDto logData;
            if (string.IsNullOrEmpty(searchText))
            {
                if (logType == McpLogType.All)
                {
                    logData = LogGetter.GetConsoleLog();
                }
                else
                {
                    logData = LogGetter.GetConsoleLog(logType);
                }
            }
            else
            {
                logData = LogGetter.GetConsoleLog(logType, searchText, useRegex, searchInStackTrace);
            }
            
            // Check for cancellation before processing
            cancellationToken.ThrowIfCancellationRequested();
            
            // Limit logs according to maxCount - take latest logs (tail behavior) and reverse to newest first.
            LogEntryDto[] limitedEntries = logData.LogEntries;
            if (limitedEntries.Length > maxCount)
            {
                limitedEntries = limitedEntries.Skip(limitedEntries.Length - maxCount).Reverse().ToArray();
            }
            else
            {
                limitedEntries = limitedEntries.Reverse().ToArray();
            }
            
            // Create type-safe response
            LogEntry[] logs = limitedEntries.Select(entry => new LogEntry(
                type: entry.LogType,
                message: entry.Message,
                stackTrace: includeStackTrace ? entry.StackTrace : null
            )).ToArray();
            
            GetLogsResponse response = new GetLogsResponse(
                totalCount: logData.TotalCount,
                displayedCount: limitedEntries.Length,
                logType: logTypeString,
                maxCount: maxCount,
                searchText: searchText,
                includeStackTrace: includeStackTrace,
                logs: logs
            );
            
            return Task.FromResult(response);
        }
    }
} 