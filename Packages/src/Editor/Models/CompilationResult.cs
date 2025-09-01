using System.Collections.Generic;
using System.Reflection;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Compilation Result
    /// 
    /// Related Class: RoslynCompiler
    /// </summary>
    public class CompilationResult
    {
        /// <summary>Compilation Success</summary>
        public bool Success { get; set; }

        /// <summary>Compiled Assembly</summary>
        public Assembly CompiledAssembly { get; set; }

        /// <summary>Errors</summary>
        public List<CompilationError> Errors { get; set; } = new();

        /// <summary>Warnings</summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Code Formatted for Compilation
        /// (After extracting and moving using statements, applying class/method wrapping)
        /// </summary>
        public string UpdatedCode { get; set; }

        /// <summary>Flag for Security Violations</summary>
        public bool HasSecurityViolations { get; set; }

        /// <summary>Security Violation Details</summary>
        public List<SecurityViolation> SecurityViolations { get; set; } = new();

        /// <summary>Failure Reason Category</summary>
        public CompilationFailureReason FailureReason { get; set; } = CompilationFailureReason.None;
    }

    /// <summary>
    /// Compilation Failure Reason Categories
    /// </summary>
    public enum CompilationFailureReason
    {
        /// <summary>No Failure (Success)</summary>
        None,

        /// <summary>Compilation Error</summary>
        CompilationError,

        /// <summary>Security Violation</summary>
        SecurityViolation,

        /// <summary>Dynamic Assembly Addition Failed</summary>
        DynamicAssemblyFailed,

        /// <summary>Using Statement Addition Failed</summary>
        UsingStatementFailed
    }
}