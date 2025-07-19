using System.ComponentModel;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Schema for Compile command parameters
    /// Provides type-safe parameter access with default values
    /// </summary>
    public class CompileSchema : BaseToolSchema
    {
        /// <summary>
        /// Whether to perform forced recompilation
        /// </summary>
        [Description("Whether to perform forced recompilation. Note: Force compilation results do not include error/warning messages in the response. Use get-logs tool after execution to retrieve compilation messages.")]
        public bool ForceRecompile { get; set; } = false;
    }
} 