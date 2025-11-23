#if UNITY_EDITOR_OSX

using System.ComponentModel;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Schema for the Focus Unity Window MCP tool.
    /// Bringing the currently connected Unity Editor window to the foreground
    /// does not require any extra parameters beyond the base timeout.
    /// </summary>
    public class FocusUnityWindowSchema : BaseToolSchema
    {
        /// <summary>
        /// Retain the default timeout from the base implementation.
        /// Having the property here allows MCP clients to override it if desired.
        /// </summary>
        [Description("Timeout for bringing the Unity Editor window to the foreground (seconds).")]
        public override int TimeoutSeconds
        {
            get => base.TimeoutSeconds;
            set => base.TimeoutSeconds = value;
        }
    }
}

#endif

