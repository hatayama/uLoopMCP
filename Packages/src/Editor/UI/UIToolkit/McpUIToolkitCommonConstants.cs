namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Common constants for UI Toolkit implementation
    /// </summary>
    internal static class McpUIToolkitCommonConstants
    {
        // Update intervals in milliseconds
        public const int UPDATE_INTERVAL_DEFAULT = 500;
        public const int UPDATE_INTERVAL_FOCUSED = 200;
        
        // UI element sizes
        public const int BUTTON_HEIGHT_NORMAL = 25;
        public const int BUTTON_HEIGHT_MULTILINE = 40;
        
        // Element names - Main containers
        public const string ELEMENT_MAIN_SCROLL_VIEW = "main-scroll-view";
        public const string ELEMENT_SERVER_CONTROLS = "server-controls";
        public const string ELEMENT_CONNECTED_TOOLS = "connected-tools";
        public const string ELEMENT_EDITOR_CONFIG = "editor-config";
        public const string ELEMENT_SECURITY_SETTINGS = "security-settings";
        
        // CSS Classes - Common
        public const string CLASS_MCP_SECTION = "mcp-section";
        public const string CLASS_MCP_HELPBOX = "mcp-helpbox";
        public const string CLASS_MCP_HELPBOX_INFO = "mcp-helpbox--info";
        public const string CLASS_MCP_HELPBOX_WARNING = "mcp-helpbox--warning";
        public const string CLASS_MCP_HELPBOX_ERROR = "mcp-helpbox--error";
        public const string CLASS_MCP_BUTTON = "mcp-button";
        public const string CLASS_MCP_BUTTON_PRIMARY = "mcp-button--primary";
        public const string CLASS_MCP_BUTTON_SECONDARY = "mcp-button--secondary";
        public const string CLASS_MCP_BUTTON_WARNING = "mcp-button--warning";
        public const string CLASS_MCP_BUTTON_DISABLED = "mcp-button--disabled";
        public const string CLASS_MCP_ENUM_FIELD = "mcp-enum-field";
        

    }
}