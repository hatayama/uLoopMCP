namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Response for PlayStopUnity command execution
    /// Contains execution result and current play mode state
    /// 
    /// Design Reference: @Packages/docs/ARCHITECTURE_Unity.md - UseCase + Tool Pattern (DDD Integration)
    /// 
    /// Related classes:
    /// - PlayStopUnityTool: Tool implementation
    /// - PlayStopUnitySchema: Type-safe parameter schema
    /// </summary>
    public class PlayStopUnityResponse : BaseToolResponse
    {
        /// <summary>
        /// Execution result message
        /// </summary>
        public string Message { get; set; } = "";

        /// <summary>
        /// Current Unity play mode state after execution
        /// </summary>
        public bool IsPlaying { get; set; }

        /// <summary>
        /// Action that was performed
        /// </summary>
        public string ActionPerformed { get; set; } = "";

        /// <summary>
        /// Whether the operation succeeded
        /// </summary>
        public bool Success { get; set; }
    }
}