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
#pragma warning disable IDE0051 // Remove unused private members
        public long ExecutionTimeMs { get; set; }
#pragma warning restore IDE0051

        /// <summary>
        /// Set timing information automatically
        /// </summary>
        public void SetTimingInfo(DateTime startTime, DateTime endTime)
        {
            ExecutionTimeMs = (long)(endTime - startTime).TotalMilliseconds;
        }
    }
} 