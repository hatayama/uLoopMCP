namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Constants for UI Toolkit implementation
    /// </summary>
    internal static class McpUIToolkitConstants
    {
        // Update intervals in milliseconds
        public const int UPDATE_INTERVAL_DEFAULT = 500;
        public const int UPDATE_INTERVAL_FOCUSED = 200;
        
        // UI element sizes
        public const int BUTTON_HEIGHT_NORMAL = 25;
        public const int BUTTON_HEIGHT_MULTILINE = 40;
        
        // Element names
        public const string ELEMENT_MAIN_SCROLL_VIEW = "main-scroll-view";
        public const string ELEMENT_SERVER_STATUS = "server-status";
        public const string ELEMENT_SERVER_CONTROLS = "server-controls";
        public const string ELEMENT_CONNECTED_TOOLS = "connected-tools";
        public const string ELEMENT_EDITOR_CONFIG = "editor-config";
        public const string ELEMENT_SECURITY_SETTINGS = "security-settings";
    }
}