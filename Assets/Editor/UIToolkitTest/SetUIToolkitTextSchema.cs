using System.ComponentModel;
using io.github.hatayama.uLoopMCP;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Schema for SetUIToolkitText tool parameters
    /// Provides type-safe parameter access for setting text in UI Toolkit test window
    /// </summary>
    public class SetUIToolkitTextSchema : BaseToolSchema
    {
        /// <summary>
        /// Text to display in the UI Toolkit test window
        /// </summary>
        [Description("Text to display in the UI Toolkit test window")]
        public string Text { get; set; } = "";

        /// <summary>
        /// Whether to automatically open the window if it's not already open
        /// </summary>
        [Description("Whether to automatically open the window if it's not already open")]
        public bool AutoOpenWindow { get; set; } = true;

        /// <summary>
        /// Whether to log the operation to console
        /// </summary>
        [Description("Whether to log the operation to console")]
        public bool LogToConsole { get; set; } = true;
    }
}