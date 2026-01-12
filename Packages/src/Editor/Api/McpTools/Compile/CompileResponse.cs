namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Compile error or warning information
    /// </summary>
    public class CompileIssue
    {
        public string Message { get; set; }
        public string File { get; set; }
        public int Line { get; set; }

        public CompileIssue(string message, string file, int line)
        {
            Message = message;
            File = file;
            Line = line;
        }

        public CompileIssue() { }
    }

    /// <summary>
    /// Response schema for Compile command
    /// Provides type-safe response structure
    /// </summary>
    public class CompileResponse : BaseToolResponse
    {
        /// <summary>
        /// Whether compilation was successful. Null indicates indeterminate status.
        /// </summary>
        public bool? Success { get; set; }

        /// <summary>
        /// Number of compilation errors. Null when status is indeterminate.
        /// </summary>
        public int? ErrorCount { get; set; }

        /// <summary>
        /// Number of compilation warnings. Null when status is indeterminate.
        /// </summary>
        public int? WarningCount { get; set; }

        /// <summary>
        /// Compilation errors. Null when status is indeterminate.
        /// </summary>
        public CompileIssue[] Errors { get; set; }

        /// <summary>
        /// Compilation warnings. Null when status is indeterminate.
        /// </summary>
        public CompileIssue[] Warnings { get; set; }

        /// <summary>
        /// Optional message for additional information
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Create a new CompileResponse
        /// </summary>
        public CompileResponse(
            bool? success,
            int? errorCount,
            int? warningCount,
            CompileIssue[] errors,
            CompileIssue[] warnings,
            string message = null
        )
        {
            Success = success;
            ErrorCount = errorCount;
            WarningCount = warningCount;
            Errors = errors;
            Warnings = warnings;
            Message = message;
        }

        /// <summary>
        /// Parameterless constructor for JSON deserialization
        /// </summary>
        public CompileResponse()
        {
        }
    }
}
