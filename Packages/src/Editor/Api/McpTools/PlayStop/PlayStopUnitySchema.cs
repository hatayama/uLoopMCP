using System.ComponentModel;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Schema for PlayStopUnity command parameters
    /// Provides type-safe parameter access for Unity play mode control
    /// 
    /// Design Reference: @Packages/docs/ARCHITECTURE_Unity.md - UseCase + Tool Pattern (DDD Integration)
    /// 
    /// Related classes:
    /// - PlayStopUnityTool: Tool implementation
    /// - PlayStopUnityResponse: Type-safe response structure
    /// </summary>
    public class PlayStopUnitySchema : BaseToolSchema
    {
        /// <summary>
        /// Action to perform (play or stop)
        /// </summary>
        [Description("Action to perform: 'play' to start Unity play mode, 'stop' to stop Unity play mode")]
        public string Action { get; set; } = "";
    }
}