using System.ComponentModel;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Parameter schema for the GameView capture tool
    /// Related classes: CaptureGameViewTool, CaptureGameViewResponse
    /// </summary>
    public class CaptureGameViewSchema : BaseToolSchema
    {
        /// <summary>
        /// Resolution scale multiplier (0.1 - 1.0, default: 1.0)
        /// 1.0 is original size, 0.5 is half resolution, 0.1 is 10% resolution
        /// </summary>
        [Description("Resolution scale multiplier (0.1 to 1.0, where 1.0 is original size)")]
        public float ResolutionScale { get; set; } = 1.0f;
    }
}