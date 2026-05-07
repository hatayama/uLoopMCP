using System.ComponentModel;

using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Schema for GetLogs command parameters
    /// Provides type-safe parameter access with default values
    /// </summary>
    public class GetLogsSchema : UnityCliLoopToolSchema
    {
        /// <summary>
        /// Log type to filter (Error, Warning, Log, All)
        /// </summary>
        [Description("Log type to filter (Error, Warning, Log, All)")]
        public string LogType { get; set; } = UnityCliLoopLogType.All;

        /// <summary>
        /// Maximum number of logs to retrieve
        /// </summary>
        [Description("Maximum number of logs to retrieve")]
        public int MaxCount { get; set; } = 100;

        /// <summary>
        /// Text to search within log messages (retrieve all if empty)
        /// </summary>
        [Description("Text to search within log messages (retrieve all if empty)")]
        public string SearchText { get; set; } = "";

        /// <summary>
        /// Whether to use regular expression for search
        /// </summary>
        [Description("Whether to use regular expression for search")]
        public bool UseRegex { get; set; } = false;

        /// <summary>
        /// Whether to search within stack trace as well
        /// </summary>
        [Description("Whether to search within stack trace as well")]
        public bool SearchInStackTrace { get; set; } = false;

        /// <summary>
        /// Whether to display stack trace
        /// </summary>
        [Description("Whether to display stack trace")]
        public bool IncludeStackTrace { get; set; } = false;
    }
}
