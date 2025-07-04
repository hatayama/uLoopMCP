using System.Collections.Generic;

namespace io.github.hatayama.uMCP
{
    /// <summary>
    /// Factory class for creating MCP server configuration objects.
    /// Single Responsibility Principle: Only responsible for object creation.
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
            };

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
            // Desktop editors (Cursor, VSCode, Windsurf) require absolute path for proper connection
            if (editorType == McpEditorType.Cursor || 
                editorType == McpEditorType.VSCode || 
                editorType == McpEditorType.Windsurf)
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
            string projectRoot = UnityMcpPathResolver.GetProjectRoot();
            
            // Check if the path is within the project
            if (absolutePath.StartsWith(projectRoot))
            {
                // Remove project root and convert to forward slashes
                string relativePath = absolutePath.Substring(projectRoot.Length);
                if (relativePath.StartsWith("\\") || relativePath.StartsWith("/"))
                {
                    relativePath = relativePath.Substring(1);
                }
                
                // Convert backslashes to forward slashes for better compatibility
                return relativePath.Replace('\\', '/');
            }
            
            // If not within project, return as-is
            return absolutePath;
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
            };
            
            // Add NODE_ENV for development mode (simplified approach)
            if (developmentMode)
            {
                env[McpConstants.ENV_KEY_NODE_ENV] = McpConstants.ENV_VALUE_DEVELOPMENT;
            }
            // For production mode, simply don't set NODE_ENV (default behavior)

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