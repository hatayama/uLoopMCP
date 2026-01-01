using System.ComponentModel;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Window name matching modes for FindWindowsByName
    /// </summary>
    public enum WindowMatchMode
    {
        /// <summary>
        /// Exact match (case-insensitive)
        /// </summary>
        exact = 0,

        /// <summary>
        /// Prefix match (case-insensitive)
        /// </summary>
        prefix = 1,

        /// <summary>
        /// Contains match (case-insensitive)
        /// </summary>
        contains = 2
    }

    public class CaptureWindowSchema : BaseToolSchema
    {
        [Description("Window name to capture (e.g., 'Game', 'Scene', 'Console', 'Inspector', 'Project', 'Hierarchy', or any EditorWindow title)")]
        public string WindowName { get; set; } = "Game";

        [Description("Resolution scale multiplier (0.1 to 1.0, where 1.0 is original size)")]
        public float ResolutionScale { get; set; } = 1.0f;

        [Description("Window name matching mode: exact(0)=exact match, prefix(1)=starts with, contains(2)=partial match. All modes are case-insensitive.")]
        public WindowMatchMode MatchMode { get; set; } = WindowMatchMode.exact;
    }
}
