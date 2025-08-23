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
    }
}