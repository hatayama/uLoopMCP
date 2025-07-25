using System.Threading.Tasks;
using System.Threading;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Handles temporal cohesion for log retrieval processing
    /// Processing sequence: 1. Log retrieval, 2. Filtering, 3. Limiting and formatting
    /// Related classes: GetLogsTool, LogRetrievalService, LogFilteringService
    /// Design reference: @Packages/docs/ARCHITECTURE_Unity.md - UseCase + Tool Pattern (DDD Integration)
    /// </summary>
    public class GetLogsUseCase : AbstractUseCase<GetLogsSchema, GetLogsResponse>
    {
        /// <summary>
        /// Executes log retrieval processing
        /// </summary>
        /// <param name="parameters">Log retrieval parameters</param>
        /// <param name="cancellationToken">Cancellation control token</param>
        /// <returns>Log retrieval result</returns>
        public override Task<GetLogsResponse> ExecuteAsync(GetLogsSchema parameters, CancellationToken cancellationToken)
        {
            // 1. Log retrieval
            LogRetrievalService retrievalService = new();
            LogDisplayDto logData;
            
            if (string.IsNullOrEmpty(parameters.SearchText))
            {
                logData = retrievalService.GetLogs(parameters.LogType);
            }
            else
            {
                logData = retrievalService.GetLogsWithSearch(
                    parameters.LogType, 
                    parameters.SearchText, 
                    parameters.UseRegex, 
                    parameters.SearchInStackTrace);
            }
            
            // 2. Filtering and limiting
            cancellationToken.ThrowIfCancellationRequested();
            LogFilteringService filteringService = new();
            LogEntry[] logs = filteringService.FilterAndLimitLogs(
                logData.LogEntries, 
                parameters.MaxCount, 
                parameters.IncludeStackTrace);
            
            // 3. Response creation
            GetLogsResponse response = new GetLogsResponse(
                totalCount: logData.TotalCount,
                displayedCount: logs.Length,
                logType: parameters.LogType.ToString(),
                maxCount: parameters.MaxCount,
                searchText: parameters.SearchText,
                includeStackTrace: parameters.IncludeStackTrace,
                logs: logs
            );

            return Task.FromResult(response);
        }
    }
}