using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Factory class for creating MCP server configuration objects.
    /// Single Responsibility Principle: Only responsible for object creation.
    /// 
    /// Design document reference: Packages/src/Editor/ARCHITECTURE.md
    /// 
    /// Related classes:
    /// - McpConfigService: Uses this factory to create server configurations
    /// - McpServerConfigData: Value object that this factory creates
    /// - UnityMcpPathResolver: Provides TypeScript server paths and project root
    /// - McpConstants: Contains command constants (NODE_COMMAND) and environment keys
    /// - McpEditorType: Enum defining supported editor types
    /// 
    /// Key features:
    /// - Creates server configuration with editor-specific path formats (absolute/relative)
    /// - Generates server keys with or without port numbers based on editor type
    /// - Environment variables are minimal (only UNITY_TCP_PORT, no MCP_CLIENT_NAME)
    /// </summary>
    public static class McpServerConfigFactory
    {
        /// <summary>
        /// Creates Unity MCP server configuration.
        /// </summary>
        /// <param name="port">The port number to use.</param>
        /// <param name="serverPath">The path to the TypeScript server.</param>
        /// <param name="editorType">The editor type for client name.</param>
        /// <returns>Settings data for Unity MCP.</returns>
        public static McpServerConfigData CreateUnityMcpConfig(int port, string serverPath, McpEditorType editorType)
        {
            // Convert server path to appropriate format based on editor type
            string finalServerPath = GetServerPathForEditor(serverPath, editorType);

            Dictionary<string, string> env = new Dictionary<string, string>
            {
                { McpConstants.UNITY_TCP_PORT_ENV_KEY, port.ToString() }
                // MCP_CLIENT_NAME removed - now using clientInfo.name from MCP protocol
#if ULOOPMCP_DEBUG
                , { McpConstants.ENV_KEY_MCP_DEBUG, McpConstants.ENV_VALUE_TRUE }
                , { McpConstants.ENV_KEY_NODE_OPTIONS, McpConstants.NODE_OPTIONS_ENABLE_SOURCE_MAPS }
#endif
            };

            if (!NodePathResolver.IsNodeAvailable())
            {
                throw new System.InvalidOperationException(
                    "Node.js is not available. Ensure Node.js is installed and accessible via PATH.");
            }

            return new McpServerConfigData(
                command: McpConstants.NODE_COMMAND,
                args: new[] { finalServerPath },
                env: env
            );
        }

        /// <summary>
        /// Get appropriate server path based on editor type
        /// </summary>
        /// <param name="serverPath">The original server path</param>
        /// <param name="editorType">The editor type</param>
        /// <returns>The processed server path</returns>
        private static string GetServerPathForEditor(string serverPath, McpEditorType editorType)
        {
            // Desktop editors (Cursor, VSCode, Windsurf, Codex) require absolute path for proper connection
            if (editorType == McpEditorType.Cursor || 
                editorType == McpEditorType.VSCode || 
                editorType == McpEditorType.Windsurf ||
                editorType == McpEditorType.Codex)
            {
                return serverPath;
            }
            
            // CLI-based editors (Claude Code, Gemini CLI) use relative path for better portability
            return ConvertToRelativePath(serverPath);
        }

        /// <summary>
        /// Convert absolute path to relative path for all editors
        /// </summary>
        /// <param name="absolutePath">The absolute path to convert</param>
        /// <returns>The relative path from project root</returns>
        private static string ConvertToRelativePath(string absolutePath)
        {
            return UnityMcpPathResolver.MakeRelativeToConfigurationRoot(absolutePath);
        }

        /// <summary>
        /// Creates Unity MCP server configuration (legacy overload for backward compatibility).
        /// </summary>
        /// <param name="port">The port number to use.</param>
        /// <param name="serverPath">The path to the TypeScript server.</param>
        /// <returns>Settings data for Unity MCP.</returns>
        public static McpServerConfigData CreateUnityMcpConfig(int port, string serverPath)
        {
            return CreateUnityMcpConfig(port, serverPath, McpEditorType.ClaudeCode);
        }

        /// <summary>
        /// Creates Unity MCP server configuration with development mode support.
        /// </summary>
        /// <param name="port">The port number.</param>
        /// <param name="serverPath">The path to the TypeScript server.</param>
        /// <param name="developmentMode">Whether to enable development mode.</param>
        /// <returns>Settings data for Unity MCP.</returns>
        public static McpServerConfigData CreateUnityMcpConfigWithDevelopmentMode(int port, string serverPath, bool developmentMode)
        {
            Dictionary<string, string> env = new Dictionary<string, string>
            {
                { McpConstants.UNITY_TCP_PORT_ENV_KEY, port.ToString() }
#if ULOOPMCP_DEBUG
                , { McpConstants.ENV_KEY_MCP_DEBUG, McpConstants.ENV_VALUE_TRUE }
#endif
            };
            
            if (!NodePathResolver.IsNodeAvailable())
            {
                throw new System.InvalidOperationException(
                    "Node.js is not available. Ensure Node.js is installed and accessible via PATH.");
            }

            return new McpServerConfigData(
                command: McpConstants.NODE_COMMAND,
                args: new[] { serverPath },
                env: env
            );
        }

        /// <summary>
        /// Creates Unity MCP server key.
        /// </summary>
        /// <param name="port">The port number.</param>
        /// <returns>The server key.</returns>
        public static string CreateUnityMcpServerKey(int port)
        {
            return $"{McpConstants.PROJECT_NAME}-{port}";
        }

        /// <summary>
        /// Creates Unity MCP server key with editor type consideration.
        /// </summary>
        /// <param name="port">The port number.</param>
        /// <param name="editorType">The editor type.</param>
        /// <returns>The server key.</returns>
        public static string CreateUnityMcpServerKey(int port, McpEditorType editorType)
        {
            // Windsurf keeps the port number in the key
            if (editorType == McpEditorType.Windsurf)
            {
                return $"{McpConstants.PROJECT_NAME}-{port}";
            }
            // Other editors use simple key without port
            return McpConstants.PROJECT_NAME;
        }

    }
} 