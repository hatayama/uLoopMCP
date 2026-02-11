using System;
using System.IO;
using UnityEngine;
using System.Linq; // Added for .Concat()

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Supported editor types.
    /// </summary>
    public enum McpEditorType
    {
        Cursor,
        ClaudeCode,
        VSCode,
        GeminiCLI,
        Windsurf,
        Codex,
        McpInspector,
    }

    /// <summary>
    /// Class for resolving paths related to Unity MCP.
    /// Single Responsibility Principle: Only responsible for path resolution.
    /// 
    /// ## Architecture Overview
    /// 
    /// This project uses the Visitor pattern to eliminate switch statements and centralize
    /// editor-specific configuration logic. The EditorConfigProvider class now handles
    /// all editor-specific configuration retrieval.
    /// 
    /// ## Adding New LLM Tools Support
    /// 
    /// When adding support for a new LLM tool, follow these steps:
    /// 
    /// ### 1. Update McpEditorType enum (this file)
    /// - Add new enum value (e.g., `NewEditor`)
    /// 
    /// ### 2. Update IEditorConfigVisitor interface
    /// - Add new Visit method (e.g., `VisitNewEditor()`)
    /// 
    /// ### 3. Update EditorConfigProvider.ConfigurationVisitor
    /// - Implement the new Visit method with configuration details
    /// - Return EditorConfig with client name, config path, and config directory
    /// 
    /// ### 4. Update McpEditorTypeExtensions.Accept method
    /// - Add case for new editor type in switch expression
    /// 
    /// ### 5. Add path resolution methods (this file)
    /// - Add GetXXXConfigPath() method
    /// - Add GetXXXConfigDirectory() method (if needed)
    /// - Add configuration file path constants
    /// 
    /// ### 6. Update McpConstants.cs
    /// - Add CLIENT_NAME_XXX constant
    /// 
    /// ### 7. Related Classes
    /// - EditorConfigProvider: Centralized configuration management using Visitor pattern
    /// - McpConfigService: Handles configuration file read/write operations
    /// - McpConfigRepository: Manages configuration data persistence
    /// - McpConfigServiceFactory: Automatically creates services for all enum values
    /// - McpServerConfigFactory: Creates server configuration objects with editor-specific handling
    /// - McpEditorSettings: Manages editor-specific settings
    /// 
    /// ### 8. Configuration Details
    /// 
    /// #### Server Key Format:
    /// - Windsurf: "uLoopMCP-{port}" (includes port number)
    /// - Other editors: "uLoopMCP" (no port number)
    /// 
    /// #### Server Path Format:
    /// - Desktop editors (Cursor, VSCode, Windsurf): Absolute path
    /// - CLI / project-level editors (Claude Code, Gemini CLI, Codex): Relative path from project root
    /// 
    /// #### Environment Variables:
    /// ```json
    /// {
    ///   "mcpServers": {
    ///     "uLoopMCP": {
    ///       "command": "node",
    ///       "args": ["{path_to_server.bundle.js}"],
    ///       "env": {
    ///         "UNITY_TCP_PORT": "{port}"
    ///         // MCP_CLIENT_NAME removed - now using clientInfo.name from MCP protocol
    ///       }
    ///     }
    ///   }
    /// }
    /// ```
    /// 
    /// ### 9. Testing Checklist
    /// - Verify compilation succeeds without errors
    /// - Test configuration file creation/update
    /// - Verify client name appears correctly via MCP protocol handshake
    /// - Test server start/stop functionality
    /// - Verify correct server key format (with/without port)
    /// - Test path format (absolute/relative) based on editor type
    /// - Verify factory automatically creates service for new editor type
    /// </summary>
    public static class UnityMcpPathResolver
    {
        private const string CURSOR_CONFIG_DIR = ".cursor";
        private const string VSCODE_CONFIG_DIR = ".vscode";
        private const string GEMINI_CONFIG_DIR = ".gemini";
        private const string CODEX_CONFIG_DIR = ".codex";
        private const string CODEIUM_CONFIG_DIR = ".codeium";
        private const string WINDSURF_SUBDIR = "windsurf";
        private const string MCP_CONFIG_FILE = "mcp.json";
        private const string CLAUDE_CODE_CONFIG_FILE = ".mcp.json";
        private const string GEMINI_CONFIG_FILE = "settings.json";
        private const string WINDSURF_CONFIG_FILE = "mcp_config.json";
        private const string CODEX_CONFIG_FILE = "config.toml";
        private const string MCP_INSPECTOR_CONFIG_FILE = ".inspector.mcp.json";

        /// <summary>
        /// Gets the path to the project root directory.
        /// </summary>
        public static string GetProjectRoot()
        {
            return Path.GetDirectoryName(Application.dataPath);
        }

        /// <summary>
        /// Gets the base root for configuration files.
        /// When addRepositoryRoot is true and Git root differs, Git root is returned.
        /// </summary>
        private static string GetConfigurationRoot()
        {
            string projectRoot = GetProjectRoot();
            bool useRepositoryRoot = McpEditorSettings.GetAddRepositoryRoot();

            if (!useRepositoryRoot)
            {
                return projectRoot;
            }

            string gitRoot = GetGitRepositoryRoot();
            if (!string.IsNullOrEmpty(gitRoot))
            {
                return gitRoot;
            }

            return projectRoot;
        }

        private static string CombineWithConfigurationRoot(params string[] paths)
        {
            string root = GetConfigurationRoot();
            return Path.Combine(new[] { root }.Concat(paths).ToArray());
        }

        /// <summary>
        /// Makes an absolute path relative to the current configuration root.
        /// If the absolutePath is under the configuration root, returns relative path with '/' separators.
        /// Otherwise returns the original absolutePath.
        /// </summary>
        public static string MakeRelativeToConfigurationRoot(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
            {
                return absolutePath;
            }

            string root = GetConfigurationRoot();
            if (string.IsNullOrEmpty(root))
            {
                return absolutePath;
            }

            // Ensure both have consistent trailing separators for comparison
            string normalizedRoot = root.Replace('\\', '/');
            string normalizedPath = absolutePath.Replace('\\', '/');

            if (!normalizedRoot.EndsWith("/"))
            {
                normalizedRoot += "/";
            }

            // Use case-insensitive comparison to support Windows file systems
            if (normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                string relative = normalizedPath.Substring(normalizedRoot.Length);
                // Ensure forward slashes
                return relative.Replace('\\', '/');
            }

            return absolutePath;
        }

        private static string _cachedGitRepositoryRoot;
        private static bool _cachedGitRootComputed;
        private static bool _cachedGitRootDiffers;
        private static bool _cachedGitRootDifferenceComputed;

        /// <summary>
        /// Clears cached Git repository detection results. Call when toggling repository-root setting.
        /// </summary>
        public static void InvalidateGitRootCache()
        {
            _cachedGitRepositoryRoot = null;
            _cachedGitRootComputed = false;
            _cachedGitRootDiffers = false;
            _cachedGitRootDifferenceComputed = false;
        }

        /// <summary>
        /// Try to get the Git repository root directory.
        /// Returns null when not found.
        /// </summary>
        public static string GetGitRepositoryRoot()
        {
            if (_cachedGitRootComputed)
            {
                return _cachedGitRepositoryRoot;
            }

            try
            {
                string projectRoot = GetProjectRoot();
                string currentDirectory = projectRoot;
            const int maxDepth = 10;
                int depth = 0;

            while (!string.IsNullOrEmpty(currentDirectory) && depth < maxDepth)
                {
                    string gitDirectoryPath = Path.Combine(currentDirectory, ".git");
                    if (Directory.Exists(gitDirectoryPath) || File.Exists(gitDirectoryPath))
                    {
                        _cachedGitRepositoryRoot = currentDirectory;
                        _cachedGitRootComputed = true;
                        return _cachedGitRepositoryRoot;
                    }

                    string parentDirectory = Path.GetDirectoryName(currentDirectory);
                    if (string.IsNullOrEmpty(parentDirectory) || string.Equals(parentDirectory, currentDirectory, StringComparison.Ordinal))
                    {
                        break;
                    }

                    currentDirectory = parentDirectory;
                    depth++;
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Failed to detect Git repository root: {ex.Message}");
            }

            _cachedGitRepositoryRoot = null;
            _cachedGitRootComputed = true;
            return null;
        }

        /// <summary>
        /// Determines if Git repository root differs from Unity project root.
        /// </summary>
        public static bool GitRootDiffersFromProjectRoot()
        {
            if (_cachedGitRootDifferenceComputed)
            {
                return _cachedGitRootDiffers;
            }

            string projectRoot = GetProjectRoot();
            string gitRoot = GetGitRepositoryRoot();

            _cachedGitRootDiffers = !string.IsNullOrEmpty(gitRoot) && !string.Equals(gitRoot, projectRoot, StringComparison.Ordinal);
            _cachedGitRootDifferenceComputed = true;
            return _cachedGitRootDiffers;
        }

        /// <summary>
        /// Gets the path to .cursor/mcp.json in the project root.
        /// </summary>
        public static string GetMcpConfigPath()
        {
            return CombineWithConfigurationRoot(CURSOR_CONFIG_DIR, MCP_CONFIG_FILE);
        }

        /// <summary>
        /// Gets the path to the Claude Code configuration file (.mcp.json).
        /// </summary>
        public static string GetClaudeCodeConfigPath()
        {
            return CombineWithConfigurationRoot(CLAUDE_CODE_CONFIG_FILE);
        }

        /// <summary>
        /// Gets the path to the VSCode configuration file (.vscode/mcp.json).
        /// </summary>
        public static string GetVSCodeConfigPath()
        {
            return CombineWithConfigurationRoot(VSCODE_CONFIG_DIR, MCP_CONFIG_FILE);
        }

        /// <summary>
        /// Gets the path to the Gemini CLI configuration file (.gemini/settings.json).
        /// </summary>
        public static string GetGeminiCLIConfigPath()
        {
            return CombineWithConfigurationRoot(GEMINI_CONFIG_DIR, GEMINI_CONFIG_FILE);
        }

        /// <summary>
        /// Gets the path to the Windsurf configuration file (~/.codeium/windsurf/mcp_config.json).
        /// </summary>
        public static string GetWindsurfConfigPath()
        {
            string homeDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            return Path.Combine(homeDirectory, CODEIUM_CONFIG_DIR, WINDSURF_SUBDIR, WINDSURF_CONFIG_FILE);
        }

        /// <summary>
        /// Gets the path to the MCP Inspector configuration file (.inspector.mcp.json).
        /// </summary>
        public static string GetMcpInspectorConfigPath()
        {
            return CombineWithConfigurationRoot(MCP_INSPECTOR_CONFIG_FILE);
        }

        /// <summary>
        /// Gets the path to the Codex configuration file.
        /// Resolves to {projectRoot}/.codex/config.toml (or {gitRoot}/.codex/config.toml when repository root is enabled).
        /// </summary>
        public static string GetCodexConfigPath()
        {
            return CombineWithConfigurationRoot(CODEX_CONFIG_DIR, CODEX_CONFIG_FILE);
        }

        /// <summary>
        /// Gets the configuration file path for the specified editor.
        /// </summary>
        /// <param name="editorType">The type of editor.</param>
        /// <returns>The path to the configuration file.</returns>
        public static string GetConfigPath(McpEditorType editorType)
        {
            return EditorConfigProvider.GetConfigPath(editorType);
        }

        /// <summary>
        /// Gets the configuration file path for the specified editor using an explicit base root.
        /// For editors whose configs live under the project/repository root, the path is resolved under baseRoot.
        /// For editors with home-directory configs (e.g., Windsurf), returns their standard home path.
        /// </summary>
        /// <param name="editorType">Editor type</param>
        /// <param name="baseRoot">Base root directory (e.g., Git root or Unity project root)</param>
        /// <returns>Absolute path to the configuration file</returns>
        public static string GetConfigPathForRoot(McpEditorType editorType, string baseRoot)
        {
            string root = string.IsNullOrEmpty(baseRoot) ? GetProjectRoot() : baseRoot;

            return editorType switch
            {
                McpEditorType.Cursor => Path.Combine(root, CURSOR_CONFIG_DIR, MCP_CONFIG_FILE),
                McpEditorType.ClaudeCode => Path.Combine(root, CLAUDE_CODE_CONFIG_FILE),
                McpEditorType.VSCode => Path.Combine(root, VSCODE_CONFIG_DIR, MCP_CONFIG_FILE),
                McpEditorType.GeminiCLI => Path.Combine(root, GEMINI_CONFIG_DIR, GEMINI_CONFIG_FILE),
                McpEditorType.McpInspector => Path.Combine(root, MCP_INSPECTOR_CONFIG_FILE),
                McpEditorType.Windsurf => GetWindsurfConfigPath(),
                McpEditorType.Codex => Path.Combine(root, CODEX_CONFIG_DIR, CODEX_CONFIG_FILE),
                _ => throw new ArgumentOutOfRangeException(nameof(editorType), editorType, null),
            };
        }

        /// <summary>
        /// Gets the path to the .cursor directory.
        /// </summary>
        public static string GetCursorConfigDirectory()
        {
            return CombineWithConfigurationRoot(CURSOR_CONFIG_DIR);
        }

        /// <summary>
        /// Gets the path to the .vscode directory.
        /// </summary>
        public static string GetVSCodeConfigDirectory()
        {
            return CombineWithConfigurationRoot(VSCODE_CONFIG_DIR);
        }

        /// <summary>
        /// Gets the path to the .gemini directory.
        /// </summary>
        public static string GetGeminiConfigDirectory()
        {
            return CombineWithConfigurationRoot(GEMINI_CONFIG_DIR);
        }

        /// <summary>
        /// Gets the path to the .codeium/windsurf directory for Windsurf.
        /// </summary>
        public static string GetWindsurfConfigDirectory()
        {
            string homeDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            return Path.Combine(homeDirectory, CODEIUM_CONFIG_DIR, WINDSURF_SUBDIR);
        }

        /// <summary>
        /// Gets the Codex configuration directory path.
        /// Resolves to {projectRoot}/.codex (or {gitRoot}/.codex when repository root is enabled).
        /// </summary>
        public static string GetCodexConfigDirectory()
        {
            return CombineWithConfigurationRoot(CODEX_CONFIG_DIR);
        }

        /// <summary>
        /// Gets the configuration directory for the specified editor (only if it exists).
        /// </summary>
        /// <param name="editorType">The type of editor.</param>
        /// <returns>The path to the configuration directory (null for Claude Code and MCP Inspector).</returns>
        public static string GetConfigDirectory(McpEditorType editorType)
        {
            return EditorConfigProvider.GetConfigDirectory(editorType);
        }

        /// <summary>
        /// Gets the relative path from package base to TypeScript server file.
        /// </summary>
        /// <returns>The relative path to server.bundle.js</returns>
        private static string GetTypeScriptServerRelativePath()
        {
            return Path.Combine(McpConstants.TYPESCRIPT_SERVER_DIR, McpConstants.DIST_DIR, McpConstants.SERVER_BUNDLE_FILE);
        }

        /// <summary>
        /// Builds the full path to TypeScript server file from a base path.
        /// </summary>
        /// <param name="basePath">The base path to combine with</param>
        /// <returns>The full path to server.bundle.js</returns>
        private static string BuildTypeScriptServerPath(string basePath)
        {
            return Path.Combine(basePath, GetTypeScriptServerRelativePath());
        }

        /// <summary>
        /// Gets the path to the TypeScript server.
        /// Priority: 1. Local development, 2. Fixed path (Library/uLoopMCP), 3. Package path via Unity API, 4. PackageCache (fallback)
        /// </summary>
        public static string GetTypeScriptServerPath()
        {
            string projectRoot = GetProjectRoot();

            // 1. Local development path (Packages/src) - highest priority for developers
            string localPath = Path.Combine(projectRoot, McpConstants.PACKAGES_DIR, McpConstants.SRC_DIR);
            string localServerPath = BuildTypeScriptServerPath(localPath);
            if (File.Exists(localServerPath))
            {
                return localServerPath;
            }

            // 2. Fixed path in Library - stable path for mcp.json
            string fixedPath = Path.Combine(
                projectRoot,
                McpConstants.LIBRARY_DIR,
                McpConstants.ULOOPMCP_DIR,
                McpConstants.SERVER_BUNDLE_FILE
            );
            if (File.Exists(fixedPath))
            {
                return fixedPath;
            }

            // 3. Package path via Unity Package Manager API (supports submodules and local packages)
            string packagePath = McpConstants.PackageResolvedPath;
            if (!string.IsNullOrEmpty(packagePath))
            {
                string serverPath = BuildTypeScriptServerPath(packagePath);
                if (File.Exists(serverPath))
                {
                    return serverPath;
                }
            }

            // 4. PackageCache - fallback for initial setup before ServerBundleCopier runs
            string packageCacheDir = Path.Combine(projectRoot, McpConstants.LIBRARY_DIR, McpConstants.PACKAGE_CACHE_DIR);
            if (Directory.Exists(packageCacheDir))
            {
                string[] packageDirs = Directory.GetDirectories(packageCacheDir, McpConstants.PackageNamePattern);
                foreach (string packageDir in packageDirs)
                {
                    string serverPath = BuildTypeScriptServerPath(packageDir);
                    if (File.Exists(serverPath))
                    {
                        return serverPath;
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Gets the base path of the package.
        /// Supports both installation via Package Manager and local development.
        /// </summary>
        public static string GetPackageBasePath()
        {
            string projectRoot = GetProjectRoot();

            // 1. First, check the path for local development (Packages/src).
            string localPath = Path.Combine(projectRoot, McpConstants.PACKAGES_DIR, McpConstants.SRC_DIR);
            string localServerPath = BuildTypeScriptServerPath(localPath);
            if (File.Exists(localServerPath))
            {
                // Using local package path
                return localPath;
            }

            // 2. Search for the path when installed via Package Manager.
            string packageCacheDir = Path.Combine(projectRoot, McpConstants.LIBRARY_DIR, McpConstants.PACKAGE_CACHE_DIR);
            if (Directory.Exists(packageCacheDir))
            {
                string[] packageDirs = Directory.GetDirectories(packageCacheDir, McpConstants.PackageNamePattern);

                foreach (string packageDir in packageDirs)
                {
                    string serverPath = BuildTypeScriptServerPath(packageDir);
                    if (File.Exists(serverPath))
                    {
                        // Using Package Manager package path
                        return packageDir;
                    }
                }
            }

            // 3. If neither is found, return the local path (with an error log).

            return localPath;
        }

        /// <summary>
        /// Detects if running in WSL2 environment.
        /// </summary>
        /// <returns>True if running in WSL2, false otherwise.</returns>
        public static bool IsWSL2Environment()
        {
            // Check WSL_DISTRO_NAME environment variable (WSL2 specific)
            string wslDistroName = System.Environment.GetEnvironmentVariable("WSL_DISTRO_NAME");
            if (!string.IsNullOrEmpty(wslDistroName))
            {
                return true;
            }

            // Check WSLENV environment variable (WSL1/WSL2 common)
            string wslEnv = System.Environment.GetEnvironmentVariable("WSLENV");
            if (!string.IsNullOrEmpty(wslEnv))
            {
                return true;
            }

            // Check if /proc/version contains Microsoft (WSL indicator)
            string versionFilePath = "/proc/version";
            if (File.Exists(versionFilePath))
            {
                string versionContent = File.ReadAllText(versionFilePath);
                if (versionContent.Contains("Microsoft", System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Converts Windows path to WSL2 path format.
        /// Example: C:\Users\user\project -> /mnt/c/Users/user/project
        /// </summary>
        /// <param name="windowsPath">Windows absolute path</param>
        /// <returns>WSL2 formatted path</returns>
        public static string ConvertWindowsPathToWSL2(string windowsPath)
        {
            if (string.IsNullOrEmpty(windowsPath))
            {
                return windowsPath;
            }

            // Check if it's already a Unix-style path
            if (windowsPath.StartsWith("/"))
            {
                return windowsPath;
            }

            // Check if it's a Windows absolute path (starts with drive letter)
            if (windowsPath.Length >= 3 && windowsPath[1] == ':' && windowsPath[2] == '\\')
            {
                char driveLetter = char.ToLower(windowsPath[0]);
                string pathWithoutDrive = windowsPath.Substring(3); // Remove "C:\"
                string unixPath = pathWithoutDrive.Replace('\\', '/');
                return $"/mnt/{driveLetter}/{unixPath}";
            }

            // If it's not a Windows absolute path, return as-is
            return windowsPath;
        }

        /// <summary>
        /// Gets the TypeScript server path with WSL2 conversion if needed for Claude Code.
        /// </summary>
        /// <param name="forClaudeCode">Whether this path is for Claude Code configuration</param>
        /// <returns>TypeScript server path, converted to WSL2 format if needed</returns>
        public static string GetTypeScriptServerPath(bool forClaudeCode = false)
        {
            string serverPath = GetTypeScriptServerPath();
            
            if (forClaudeCode && IsWSL2Environment())
            {
                return ConvertWindowsPathToWSL2(serverPath);
            }
            
            return serverPath;
        }
    }
} 