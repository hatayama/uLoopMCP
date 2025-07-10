using System.ComponentModel;

namespace io.github.hatayama.uMCP
{
    /// <summary>
    /// Supported log types for filtering
    /// </summary>
    public enum McpLogType
    {
        None = -1,
        Log = 0,
        Warning = 1,
        Error = 2,
        All = 3,
    }

    /// <summary>
    /// Schema for GetLogs command parameters
    /// Provides type-safe parameter access with default values
    /// </summary>
    public class GetLogsSchema : BaseToolSchema
    {
        /// <summary>
        /// Log type to filter (Error, Warning, Log, All)
        /// </summary>
        [Description("Log type to filter (Error(2), Warning(1), Log(0), All(3))")]
        public McpLogType LogType { get; set; } = McpLogType.All;

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
        /// Whether to display stack trace
        /// </summary>
        [Description("Whether to display stack trace")]
        public bool IncludeStackTrace { get; set; } = true;
    }
} 