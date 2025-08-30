namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Central constants repository for Unity MCP system.
    /// 
    /// Design document reference: Packages/src/Editor/ARCHITECTURE.md
    /// 
    /// Related classes:
    /// - McpConfigService: Uses these constants for configuration management
    /// - McpServerConfigFactory: Uses port and environment variable constants
    /// - McpEditorWindow: Uses SessionState keys for UI state persistence
    /// - McpSessionManager: Uses SessionState keys for connection state management
    /// - EditorConfigProvider: Provides client names via GetClientNameForEditor method
    /// </summary>
    public static class McpConstants
    {
        public const string PROJECT_NAME = "uLoopMCP";
        
        // JSON configuration keys
        public const string JSON_KEY_MCP_SERVERS = "mcpServers";
        public const string JSON_KEY_COMMAND = "command";
        public const string JSON_KEY_ARGS = "args";
        public const string JSON_KEY_ENV = "env";
        
        // Editor settings
        public const string SETTINGS_FILE_NAME = "UnityMcpSettings.json";
        public const string USER_SETTINGS_FOLDER = "UserSettings";
        
        // Server configuration
        public const string NODE_COMMAND = "node";
        public const string UNITY_TCP_PORT_ENV_KEY = "UNITY_TCP_PORT";
        
        // Scripting define symbols
        public const string SCRIPTING_DEFINE_ULOOPMCP_DEBUG = "ULOOPMCP_DEBUG";
        public const string SCRIPTING_DEFINE_ULOOPMCP_HAS_ROSLYN = "ULOOPMCP_HAS_ROSLYN";
        
        // Environment variable keys for development mode
        public const string ENV_KEY_ULOOPMCP_DEBUG = "ULOOPMCP_DEBUG";
        public const string ENV_KEY_ULOOPMCP_PRODUCTION = "ULOOPMCP_PRODUCTION";
        public const string ENV_KEY_MCP_DEBUG = "MCP_DEBUG";
        public const string ENV_KEY_NODE_OPTIONS = "NODE_OPTIONS";
        // MCP_CLIENT_NAME removed - now using clientInfo.name from MCP protocol
        
        // Environment variable values
        public const string ENV_VALUE_TRUE = "true";
        public const string NODE_OPTIONS_ENABLE_SOURCE_MAPS = "--enable-source-maps";
        
        // Client names for different editors
        public const string CLIENT_NAME_CURSOR = "Cursor";
        public const string CLIENT_NAME_CLAUDE_CODE = "Claude Code";
        public const string CLIENT_NAME_VSCODE = "VSCode";
        public const string CLIENT_NAME_GEMINI_CLI = "Gemini CLI";
        public const string CLIENT_NAME_WINDSURF = "Windsurf";
        public const string CLIENT_NAME_MCP_INSPECTOR = "MCP Inspector";
        public const string UNKNOWN_CLIENT_NAME = "Unknown Client";
        
        // Command messages
        public const string CLIENT_SUCCESS_MESSAGE_TEMPLATE = "Client name registered successfully: {0}";
        
        // Reconnection settings
        public const int RECONNECTION_TIMEOUT_SECONDS = 10;
        
        // Editor settings keys (development mode)
        public const string SETTINGS_KEY_ENABLE_DEVELOPMENT_MODE = "EnableDevelopmentMode";
        
        // TypeScript server related constants
        public const string TYPESCRIPT_SERVER_DIR = "TypeScriptServer~";
        public const string DIST_DIR = "dist";
        public const string SERVER_BUNDLE_FILE = "server.bundle.js";
        
        // Package path constants
        public const string PACKAGES_DIR = "Packages";
        public const string SRC_DIR = "src";
        public const string LIBRARY_DIR = "Library";
        public const string PACKAGE_CACHE_DIR = "PackageCache";
        public const string PACKAGE_NAME_PATTERN = "io.github.hatayama.uloopmcp@*";
        
        // File output directories
        public const string OUTPUT_ROOT_DIR = "uLoopMCPOutputs";
        public const string TEST_RESULTS_DIR = "TestResults";
        public const string SEARCH_RESULTS_DIR = "SearchResults";
        public const string HIERARCHY_RESULTS_DIR = "HierarchyResults";
        public const string VIBE_LOGS_DIR = "VibeLogs";
        
        // SessionState keys
        public const string SESSION_KEY_SERVER_RUNNING = "uLoopMCP.ServerRunning";
        public const string SESSION_KEY_SERVER_PORT = "uLoopMCP.ServerPort";
        public const string SESSION_KEY_AFTER_COMPILE = "uLoopMCP.AfterCompile";
        public const string SESSION_KEY_DOMAIN_RELOAD_IN_PROGRESS = "uLoopMCP.DomainReloadInProgress";
        public const string SESSION_KEY_SELECTED_EDITOR_TYPE = "uLoopMCP.SelectedEditorType";
        public const string SESSION_KEY_COMMUNICATION_LOG_HEIGHT = "uLoopMCP.CommunicationLogHeight";
        public const string SESSION_KEY_COMMUNICATION_LOGS = "uLoopMCP.CommunicationLogs";
        public const string SESSION_KEY_PENDING_REQUESTS = "uLoopMCP.PendingRequests";
        public const string SESSION_KEY_RECONNECTING = "uLoopMCP.Reconnecting";
        public const string SESSION_KEY_SHOW_RECONNECTING_UI = "uLoopMCP.ShowReconnectingUI";
        
        // Correlation ID constants
        public const int CORRELATION_ID_LENGTH = 8; // Length for correlation ID generation
        public const string GUID_FORMAT_NO_HYPHENS = "N"; // GUID format without hyphens
        
        // Error message constants
        public const string ERROR_SECURITY_VIOLATION = "SECURITY_VIOLATION";
        public const string ERROR_EXECUTION_DISABLED = "EXECUTION_DISABLED";
        public const string ERROR_COMPILATION_DISABLED_LEVEL0 = "COMPILATION_DISABLED_AT_LEVEL0";
        public const string ERROR_MESSAGE_SECURITY_LEVEL_CHANGE_BLOCKED = "Changing security level is not allowed in Restricted mode.";
        public const string ERROR_MESSAGE_EXECUTION_DISABLED = "Dynamic code execution is currently disabled. Enable in McpEditorSettings > Security Level.";
        public const string ERROR_MESSAGE_COMPILATION_DISABLED_LEVEL0 = "Compilation is disabled at isolation level 0. Raise to level 1+ to compile.";
        
        // Execution error messages
        public const string ERROR_ROSLYN_REQUIRED = "ROSLYN_REQUIRED";
        public const string ERROR_MESSAGE_ROSLYN_REQUIRED = "Dynamic code execution requires Roslyn. Please enable it from Security Settings.";
        public const string ERROR_MESSAGE_EXECUTION_IN_PROGRESS = "Another execution is already in progress";
        public const string ERROR_MESSAGE_EXECUTION_CANCELLED = "Execution was cancelled or timed out";
        public const string ERROR_MESSAGE_NO_COMPILED_ASSEMBLY = "No compiled assembly provided";
        public const string ERROR_MESSAGE_NO_EXECUTE_METHOD = "No Execute method found in compiled assembly";
        public const string ERROR_MESSAGE_FAILED_TO_CREATE_INSTANCE = "Failed to create instance of target type";
        public const string ERROR_MESSAGE_UNSUPPORTED_SIGNATURE = "Execute method signature not supported";
        
        // Test constants
        public const int TEST_COMPILE_TIMEOUT_MS = 5000; // 5 seconds for test compilation
        
        // Security constants
        public const int MAX_JSON_SIZE_BYTES = 1024 * 1024; // 1MB limit for JSON files
        public const int MAX_SETTINGS_SIZE_BYTES = 1024 * 16; // 16KB limit for settings files
        public const string SECURITY_LOG_PREFIX = "[uLoopMCP Security]";
        
        // Security: Allowed namespaces for reflection operations
        public static readonly string[] ALLOWED_NAMESPACES = {
            "UnityEditor",
            "Unity.EditorCoroutines", 
            "Unity.VisualScripting",
            "io.github.hatayama.uLoopMCP"
        };
        
        // Security: Denied types for reflection operations
        public static readonly string[] DENIED_SYSTEM_TYPES = {
            "System.Diagnostics.Process",
            "System.IO.File",
            "System.IO.Directory", 
            "System.Reflection.Assembly",
            "System.Activator"
        };
        
        /// <summary>
        /// Gets the client name for the specified editor type
        /// </summary>
        /// <param name="editorType">The editor type</param>
        /// <returns>The client name</returns>
        public static string GetClientNameForEditor(McpEditorType editorType)
        {
            return EditorConfigProvider.GetClientName(editorType);
        }
        
        /// <summary>
        /// Generates a short correlation ID for logging and tracking
        /// </summary>
        /// <returns>8-character correlation ID</returns>
        public static string GenerateCorrelationId()
        {
            return System.Guid.NewGuid().ToString(GUID_FORMAT_NO_HYPHENS)[..CORRELATION_ID_LENGTH];
        }
    }
} 