namespace io.github.hatayama.UnityCliLoop
{
    /// <summary>
    /// Central constants for shared package paths, command names, and protocol-independent limits.
    /// </summary>
    public static class UnityCliLoopConstants
    {
        private static UnityEditor.PackageManager.PackageInfo _cachedPackageInfo;

        public static UnityEditor.PackageManager.PackageInfo PackageInfo
        {
            get
            {
                if (_cachedPackageInfo == null)
                {
                    _cachedPackageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                        typeof(UnityCliLoopConstants).Assembly);

                    if (_cachedPackageInfo == null)
                    {
                        throw new System.InvalidOperationException(
                            "Failed to resolve PackageInfo for UnityCliLoop. " +
                            "Ensure the package is properly installed via Package Manager.");
                    }
                }
                return _cachedPackageInfo;
            }
        }

        public static string PackageName => PackageInfo.name;

        public static string PackageAssetPath => PackageInfo.assetPath;

        public static string PackageResolvedPath => PackageInfo.resolvedPath;

        public static string PackageNamePattern => $"{PackageName}@*";

        public static string PackageNamespace => typeof(UnityCliLoopConstants).Namespace;

        public const string PROJECT_NAME = "UnityCliLoop";
        
        // Editor settings
        public const string SETTINGS_FILE_NAME = "UnityCliLoopSettings.json";
        public const string USER_SETTINGS_FOLDER = "UserSettings";
        
        // Scripting define symbols
        public const string SCRIPTING_DEFINE_ULOOP_DEBUG = "ULOOP_DEBUG";
        
        // Environment variable keys for development mode
        public const string ENV_KEY_ULOOP_DEBUG = "ULOOP_DEBUG";
        // Reconnection settings
        public const int RECONNECTION_TIMEOUT_SECONDS = 10;
        
        // Package path constants
        public const string PACKAGES_DIR = "Packages";
        public const string SRC_DIR = "src";
        public const string LIBRARY_DIR = "Library";
        public const string TEMP_DIR = "Temp";
        public const string PACKAGE_CACHE_DIR = "PackageCache";
        public const string UNITYCLILOOP_DIR = "UnityCliLoop";
        public const string COMPILE_RESULTS_DIR = "compile-results";
        public const string JSON_FILE_EXTENSION = ".json";
        
        // .uloop directory
        public const string ULOOP_DIR = ".uloop";
        public const string ULOOP_SETTINGS_FILE_NAME = "settings.permissions.json";
        public const string ULOOP_TOOL_SETTINGS_FILE_NAME = "settings.tools.json";

        // Command name constants
        public const string TOOL_NAME_EXECUTE_DYNAMIC_CODE = "execute-dynamic-code";
        public const string TOOL_NAME_RUN_TESTS = "run-tests";
        public const string COMMAND_NAME_GET_TOOL_DETAILS = "get-tool-details";
        public const string COMMAND_NAME_GET_VERSION = "get-version";

        // File output directories
        public const string OUTPUT_ROOT_DIR = ".uloop/outputs";
        public const string TEST_RESULTS_DIR = "TestResults";
        public const string SEARCH_RESULTS_DIR = "SearchResults";
        public const string HIERARCHY_RESULTS_DIR = "HierarchyResults";
        public const string FIND_GAMEOBJECTS_RESULTS_DIR = "FindGameObjectsResults";
        public const string SCREENSHOTS_DIR = "Screenshots";
        public const string VIBE_LOGS_DIR = "VibeLogs";

        public const int CORRELATION_ID_LENGTH = 8;
        public const string GUID_FORMAT_NO_HYPHENS = "N";
        
        public const string ERROR_MESSAGE_DUPLICATE_ASMDEF = "Duplicate asmdef assembly name detected. Unity may not start compilation until duplicates are removed.";
        
        public const string ERROR_MESSAGE_EXECUTION_IN_PROGRESS = "Another execution is already in progress";
        public const string ERROR_MESSAGE_EXECUTION_CANCELLED = "Execution was cancelled or timed out";
        public const string ERROR_MESSAGE_NO_COMPILED_ASSEMBLY = "No compiled assembly provided";
        public const string ERROR_MESSAGE_NO_EXECUTE_METHOD = "No Execute method found in compiled assembly";
        public const string ERROR_MESSAGE_FAILED_TO_CREATE_INSTANCE = "Failed to create instance of target type";
        public const string ERROR_MESSAGE_UNSUPPORTED_SIGNATURE = "Execute method signature not supported";
        
        public const int TEST_COMPILE_TIMEOUT_MS = 5000;

        // Screenshot coordinate system values
        public const string COORDINATE_SYSTEM_GAME_VIEW = "gameView";
        public const string COORDINATE_SYSTEM_WINDOW = "window";

        // SimulateMouseUi constants
        public const float SIMULATE_MOUSE_UI_DEFAULT_DRAG_SPEED = 2000f;

        public const int COMPILE_START_TIMEOUT_MS = 5000;
        public const int COMPILE_START_POLL_INTERVAL_MS = 100;
        public const int COMPILE_DOMAIN_RELOAD_WAIT_TIMEOUT_MS = 10000;
        public const int COMPILE_DOMAIN_RELOAD_WAIT_POLL_INTERVAL_MS = 100;
        
        public const int MAX_JSON_SIZE_BYTES = 1024 * 1024;
        public const int MAX_SETTINGS_SIZE_BYTES = 1024 * 16;
        public const string SECURITY_LOG_PREFIX = "[UnityCliLoop Security]";
        
        // Security: Allowed namespaces for reflection operations
        public static readonly string[] ALLOWED_NAMESPACES = {
            "UnityEditor",
            "Unity.EditorCoroutines",
            "Unity.VisualScripting",
            PackageNamespace
        };
        
        // Security: Denied types for reflection operations
        public static readonly string[] DENIED_SYSTEM_TYPES = {
            "System.Diagnostics.Process",
            "System.IO.File",
            "System.IO.Directory", 
            "System.Reflection.Assembly",
            "System.Activator"
        };
        
        public static string GenerateCorrelationId()
        {
            return System.Guid.NewGuid().ToString(GUID_FORMAT_NO_HYPHENS)[..CORRELATION_ID_LENGTH];
        }
    }
} 
