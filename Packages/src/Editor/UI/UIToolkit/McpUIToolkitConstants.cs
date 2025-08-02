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
        
        // Element names - Main containers
        public const string ELEMENT_MAIN_SCROLL_VIEW = "main-scroll-view";
        public const string ELEMENT_SERVER_CONTROLS = "server-controls";
        public const string ELEMENT_CONNECTED_TOOLS = "connected-tools";
        public const string ELEMENT_EDITOR_CONFIG = "editor-config";
        public const string ELEMENT_SECURITY_SETTINGS = "security-settings";
        
        // Element names - Server Controls
        public const string ELEMENT_STATUS_LABEL = "status-label";
        public const string ELEMENT_PORT_FIELD = "port-field";
        public const string ELEMENT_PORT_WARNING_BOX = "port-warning-box";
        public const string ELEMENT_PORT_WARNING_LABEL = "port-warning-label";
        public const string ELEMENT_TOGGLE_BUTTON = "toggle-button";
        public const string ELEMENT_AUTO_START_TOGGLE = "auto-start-toggle";
        public const string ELEMENT_AUTO_START_LABEL = "auto-start-label";
        
        // Element names - Editor Config
        public const string ELEMENT_EDITOR_TYPE_FIELD = "editor-type-field";
        public const string ELEMENT_ERROR_BOX = "error-box";
        public const string ELEMENT_ERROR_LABEL = "error-label";
        public const string ELEMENT_CONFIGURE_BUTTON = "configure-button";
        public const string ELEMENT_OPEN_SETTINGS_BUTTON = "open-settings-button";
        
        // Element names - Security Settings
        public const string ELEMENT_ENABLE_TESTS_TOGGLE = "enable-tests-toggle";
        public const string ELEMENT_ENABLE_TESTS_LABEL = "enable-tests-label";
        public const string ELEMENT_ALLOW_MENU_TOGGLE = "allow-menu-toggle";
        public const string ELEMENT_ALLOW_MENU_LABEL = "allow-menu-label";
        public const string ELEMENT_ALLOW_THIRD_PARTY_TOGGLE = "allow-third-party-toggle";
        public const string ELEMENT_ALLOW_THIRD_PARTY_LABEL = "allow-third-party-label";
        
        // Element names - Connected Tools
        public const string ELEMENT_CONNECTED_TOOLS_CONTENT = "connected-tools-content";
        public const string ELEMENT_EDITOR_CONFIG_CONTENT = "editor-config-content";
        public const string ELEMENT_SECURITY_SETTINGS_CONTENT = "security-settings-content";
        
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
        
        // CSS Classes - Server Controls
        public const string CLASS_MCP_SERVER_CONTROLS = "mcp-server-controls";
        public const string CLASS_MCP_SERVER_CONTROLS_STATUS_PORT_ROW = "mcp-server-controls__status-port-row";
        public const string CLASS_MCP_SERVER_CONTROLS_STATUS_LABEL = "mcp-server-controls__status-label";
        public const string CLASS_MCP_SERVER_CONTROLS_PORT_LABEL = "mcp-server-controls__port-label";
        public const string CLASS_MCP_SERVER_CONTROLS_PORT_FIELD = "mcp-server-controls__port-field";
        public const string CLASS_MCP_SERVER_CONTROLS_TOGGLE_BUTTON = "mcp-server-controls__toggle-button";
        public const string CLASS_MCP_SERVER_CONTROLS_TOGGLE_BUTTON_START = "mcp-server-controls__toggle-button--start";
        public const string CLASS_MCP_SERVER_CONTROLS_TOGGLE_BUTTON_STOP = "mcp-server-controls__toggle-button--stop";
        public const string CLASS_MCP_SERVER_CONTROLS_AUTO_START_ROW = "mcp-server-controls__auto-start-row";
        public const string CLASS_MCP_SERVER_CONTROLS_AUTO_START_TOGGLE = "mcp-server-controls__auto-start-toggle";
        public const string CLASS_MCP_SERVER_CONTROLS_AUTO_START_LABEL = "mcp-server-controls__auto-start-label";
        
        // CSS Classes - Editor Config
        public const string CLASS_MCP_EDITOR_CONFIG = "mcp-editor-config";
        public const string CLASS_MCP_EDITOR_CONFIG_ROW = "mcp-editor-config__row";
        public const string CLASS_MCP_EDITOR_CONFIG_LABEL = "mcp-editor-config__label";
        
        // CSS Classes - Security Settings
        public const string CLASS_MCP_SECURITY_SETTINGS = "mcp-security-settings";
        public const string CLASS_MCP_SECURITY_SETTINGS_TOGGLE_ROW = "mcp-security-settings__toggle-row";
        public const string CLASS_MCP_SECURITY_SETTINGS_TOGGLE = "mcp-security-settings__toggle";
        public const string CLASS_MCP_SECURITY_SETTINGS_LABEL = "mcp-security-settings__label";
        
        // CSS Classes - Connected Tools
        public const string CLASS_MCP_CONNECTED_TOOLS = "mcp-connected-tools";
        public const string CLASS_MCP_CONNECTED_TOOLS_CLIENT_ITEM = "mcp-connected-tools__client-item";
        public const string CLASS_MCP_CONNECTED_TOOLS_CLIENT_ICON = "mcp-connected-tools__client-icon";
        public const string CLASS_MCP_CONNECTED_TOOLS_CLIENT_NAME = "mcp-connected-tools__client-name";
        public const string CLASS_MCP_CONNECTED_TOOLS_CLIENT_PORT = "mcp-connected-tools__client-port";
    }
}