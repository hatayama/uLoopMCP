using System;
using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Dynamic Code Execution Result
    /// 
    /// Related Classes: DynamicCodeExecutor, CommandRunner
    /// </summary>
    public class ExecutionResult
    {
        /// <summary>Execution Success</summary>
        public bool Success { get; set; }

        /// <summary>Execution Result</summary>
        public object Result { get; set; }

        /// <summary>Error Message</summary>
        public string ErrorMessage { get; set; }

        /// <summary>Exception</summary>
        public Exception Exception { get; set; }

        /// <summary>Execution Time</summary>
        public TimeSpan ExecutionTime { get; set; }

        /// <summary>Logs</summary>
        public List<string> Logs { get; set; } = new();

        /// <summary>Execution Statistics</summary>
        public ExecutionStatistics Statistics { get; set; }

        /// <summary>
        /// Structured compilation errors when compilation failed.
        /// Preserved to enable rich diagnostics formatting at the tool layer.
        /// </summary>
        public List<CompilationError> CompilationErrors { get; set; } = new();

        /// <summary>
        /// Code that was actually compiled after wrapping/using hoist.
        /// Useful for debugging reported line/column numbers.
        /// </summary>
        public string UpdatedCode { get; set; }
    }
}