using System;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Base response class for all Unity MCP tool responses
    /// Provides common properties like execution timing information
    /// </summary>
    public abstract class BaseToolResponse
    {
        /// <summary>
        /// Tool execution duration in milliseconds
        /// </summary>
        public long ExecutionTimeMs { get; set; }

        /// <summary>
        /// uLoopMCP server version for CLI version compatibility check
        /// </summary>
        public string ULoopServerVersion => McpVersion.VERSION;

        /// <summary>
        /// Set timing information automatically
        /// </summary>
        public void SetTimingInfo(DateTime startTime, DateTime endTime)
        {
            ExecutionTimeMs = (long)(endTime - startTime).TotalMilliseconds;
        }
    }
} 