using System.ComponentModel;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Base schema class for all Unity MCP tool parameters
    /// Provides common properties like timeout configuration
    /// </summary>
    public abstract class BaseToolSchema
    {
        /// <summary>
        /// Timeout for tool execution in seconds (default: 15 seconds)
        /// </summary>
        [Description("Timeout for tool execution in seconds (default: 15 seconds)")]
        public virtual int TimeoutSeconds { get; set; } = McpConstants.Timeouts.DEFAULT_TOOL_SECONDS;
    }
} 