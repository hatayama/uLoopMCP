using System.Linq;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// ログフィルタリングサービス
    /// 単一機能：ログエントリのフィルタリングと制限を行う
    /// 関連クラス: GetLogsTool, GetLogsUseCase, LogEntry
    /// 設計書参照: DDDリファクタリング仕様 - Application Service Layer
    /// </summary>
    public class LogFilteringService
    {
        /// <summary>
        /// ログエントリをフィルタリングし、制限を適用する
        /// </summary>
        /// <param name="entries">ログエントリ配列</param>
        /// <param name="maxCount">最大取得件数</param>
        /// <param name="includeStackTrace">スタックトレースを含めるか</param>
        /// <returns>フィルタリング後のログエントリ配列</returns>
        public LogEntry[] FilterAndLimitLogs(LogEntryDto[] entries, int maxCount, bool includeStackTrace)
        {
            // Get the most recent logs, limited by maxCount
            LogEntryDto[] limitedEntries = entries.Length > maxCount
                ? entries.Skip(entries.Length - maxCount).ToArray()
                : entries;
            
            // Reverse to show newest first
            limitedEntries = limitedEntries.Reverse().ToArray();

            // LogEntryDtoからLogEntryに変換
            return limitedEntries.Select(entry => new LogEntry(
                type: entry.LogType,
                message: entry.Message,
                stackTrace: includeStackTrace ? entry.StackTrace : null
            )).ToArray();
        }
    }
}