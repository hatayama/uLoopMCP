using System.ComponentModel;

namespace io.github.hatayama.uLoopMCP
{
    public class CaptureUnityWindowSchema : BaseToolSchema
    {
        [Description("Window name to capture (e.g., 'Game', 'Scene', 'Console', 'Inspector', 'Project', 'Hierarchy', or any EditorWindow title)")]
        public string WindowName { get; set; } = "Game";

        [Description("Resolution scale multiplier (0.1 to 1.0, where 1.0 is original size)")]
        public float ResolutionScale { get; set; } = 1.0f;
    }
}
