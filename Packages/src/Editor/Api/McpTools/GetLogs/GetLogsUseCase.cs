using System.Threading.Tasks;
using System.Threading;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// ログ取得処理の時間的凝集を担当
    /// 処理順序：1. ログの取得, 2. フィルタリング, 3. 制限とフォーマット
    /// 関連クラス: GetLogsTool, LogRetrievalService, LogFilteringService
    /// 設計書参照: DDDリファクタリング仕様 - UseCase Layer
    /// </summary>
    public class GetLogsUseCase : AbstractUseCase<GetLogsSchema, GetLogsResponse>
    {
        /// <summary>
        /// ログ取得処理を実行する
        /// </summary>
        /// <param name="parameters">ログ取得パラメータ</param>
        /// <param name="cancellationToken">キャンセレーション制御用トークン</param>
        /// <returns>ログ取得結果</returns>
        public override Task<GetLogsResponse> ExecuteAsync(GetLogsSchema parameters, CancellationToken cancellationToken)
        {
            // 1. ログの取得
            var retrievalService = new LogRetrievalService();
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
            
            // 2. フィルタリングと制限
            cancellationToken.ThrowIfCancellationRequested();
            var filteringService = new LogFilteringService();
            LogEntry[] logs = filteringService.FilterAndLimitLogs(
                logData.LogEntries, 
                parameters.MaxCount, 
                parameters.IncludeStackTrace);
            
            // 3. レスポンス作成
            var response = new GetLogsResponse(
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