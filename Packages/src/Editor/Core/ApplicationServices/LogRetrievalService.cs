namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// ログ取得サービス
    /// 単一機能：Unity Console ログの取得を行う
    /// 関連クラス: LogGetter, GetLogsTool, GetLogsUseCase
    /// 設計書参照: DDDリファクタリング仕様 - Application Service Layer
    /// </summary>
    public class LogRetrievalService
    {
        /// <summary>
        /// 指定したログタイプのログを取得する
        /// </summary>
        /// <param name="logType">取得するログタイプ</param>
        /// <returns>ログデータ</returns>
        public LogDisplayDto GetLogs(McpLogType logType)
        {
            if (logType == McpLogType.All)
            {
                return LogGetter.GetConsoleLog();
            }
            else
            {
                return LogGetter.GetConsoleLog(logType);
            }
        }

        /// <summary>
        /// 検索条件を指定してログを取得する
        /// </summary>
        /// <param name="logType">取得するログタイプ</param>
        /// <param name="searchText">検索テキスト</param>
        /// <param name="useRegex">正規表現を使用するか</param>
        /// <param name="searchInStackTrace">スタックトレース内も検索するか</param>
        /// <returns>ログデータ</returns>
        public LogDisplayDto GetLogsWithSearch(McpLogType logType, string searchText, bool useRegex, bool searchInStackTrace)
        {
            return LogGetter.GetConsoleLog(logType, searchText, useRegex, searchInStackTrace);
        }
    }
}