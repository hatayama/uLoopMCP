namespace io.github.hatayama.UnityCliLoop
{
    /// <summary>
    /// Host capability used by tools that need a read-only snapshot of Unity Console entries.
    /// </summary>
    public interface IUnityCliLoopConsoleLogService
    {
        UnityCliLoopConsoleLogResult GetLogs(string logType);

        UnityCliLoopConsoleLogResult GetLogsWithSearch(
            string logType,
            string searchText,
            bool useRegex,
            bool searchInStackTrace);
    }

    /// <summary>
    /// Tool host services that are injected into tools which need platform-provided capabilities.
    /// </summary>
    public interface IUnityCliLoopToolHostServices
    {
        IUnityCliLoopConsoleLogService ConsoleLogs { get; }
        IUnityCliLoopCompilationService Compilation { get; }
        IUnityCliLoopDynamicCodeExecutionService DynamicCodeExecution { get; }
    }

    /// <summary>
    /// Optional hook for tools that need platform-provided capabilities after normal tool construction.
    /// </summary>
    public interface IUnityCliLoopToolHostServicesReceiver
    {
        void InitializeHostServices(IUnityCliLoopToolHostServices services);
    }

    /// <summary>
    /// Supported Unity Console log families used by log-related tools and host services.
    /// </summary>
    public static class UnityCliLoopLogType
    {
        public const string Log = "Log";
        public const string Warning = "Warning";
        public const string Error = "Error";
        public const string All = "All";
    }

    /// <summary>
    /// Immutable Unity Console entry snapshot shared across tool boundaries.
    /// </summary>
    public sealed class UnityCliLoopConsoleLogEntry
    {
        public readonly string Type;
        public readonly string Message;
        public readonly string StackTrace;

        public UnityCliLoopConsoleLogEntry(string type, string message, string stackTrace)
        {
            Type = type;
            Message = message;
            StackTrace = stackTrace;
        }
    }

    /// <summary>
    /// Immutable Unity Console result snapshot shared across tool boundaries.
    /// </summary>
    public sealed class UnityCliLoopConsoleLogResult
    {
        public readonly UnityCliLoopConsoleLogEntry[] LogEntries;
        public readonly int TotalCount;

        public UnityCliLoopConsoleLogResult(UnityCliLoopConsoleLogEntry[] logEntries, int totalCount)
        {
            LogEntries = logEntries ?? new UnityCliLoopConsoleLogEntry[0];
            TotalCount = totalCount;
        }
    }
}
