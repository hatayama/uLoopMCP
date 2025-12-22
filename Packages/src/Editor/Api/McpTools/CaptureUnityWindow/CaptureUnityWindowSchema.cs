using System.ComponentModel;

namespace io.github.hatayama.uLoopMCP
{
    public enum CaptureWindowTarget
    {
        GameView = 0,
        SceneView = 1
    }

    public class CaptureUnityWindowSchema : BaseToolSchema
    {
        [Description("Target window to capture (GameView(0) - Game window, SceneView(1) - Scene window, auto-detects Prefab edit mode)")]
        public CaptureWindowTarget Target { get; set; } = CaptureWindowTarget.GameView;

        [Description("Resolution scale multiplier (0.1 to 1.0, where 1.0 is original size)")]
        public float ResolutionScale { get; set; } = 1.0f;
    }
}
