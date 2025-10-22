using System;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Provides centralized configuration management for different MCP editors using the Visitor pattern.
    /// 
    /// This class eliminates switch statements scattered across the codebase by centralizing
    /// all editor-specific configuration logic in one place. When adding a new editor type,
    /// you only need to:
    /// 1. Add the new enum value to McpEditorType
    /// 2. Add corresponding Visit method to IEditorConfigVisitor interface
    /// 3. Implement the new Visit method in this class
    /// 
    /// Related classes:
    /// - McpEditorType: Enum that defines supported editor types
    /// - EditorConfig: Value object that stores configuration information
    /// - IEditorConfigVisitor: Visitor interface for type-safe operations
    /// - UnityMcpPathResolver: Path resolution methods (to be refactored to use this class)
    /// - McpConstants: Client name constants (to be refactored to use this class)
    /// </summary>
    public static class EditorConfigProvider
    {
        /// <summary>
        /// Gets the configuration for the specified editor type using the Visitor pattern.
        /// </summary>
        /// <param name="editorType">The editor type to get configuration for</param>
        /// <returns>EditorConfig containing client name, config path, and config directory</returns>
        /// <exception cref="ArgumentException">Thrown when the editor type is not supported</exception>
        public static EditorConfig GetConfig(McpEditorType editorType)
        {
            var visitor = new ConfigurationVisitor();
            return editorType.Accept(visitor);
        }

        /// <summary>
        /// Gets the client name for the specified editor type.
        /// </summary>
        /// <param name="editorType">The editor type to get client name for</param>
        /// <returns>The client name string</returns>
        public static string GetClientName(McpEditorType editorType)
        {
            return GetConfig(editorType).ClientName;
        }

        /// <summary>
        /// Gets the configuration file path for the specified editor type.
        /// </summary>
        /// <param name="editorType">The editor type to get config path for</param>
        /// <returns>The absolute path to the configuration file</returns>
        public static string GetConfigPath(McpEditorType editorType)
        {
            return GetConfig(editorType).ConfigPath;
        }

        /// <summary>
        /// Gets the configuration directory path for the specified editor type.
        /// </summary>
        /// <param name="editorType">The editor type to get config directory for</param>
        /// <returns>The absolute path to the configuration directory, or null if not applicable</returns>
        public static string GetConfigDirectory(McpEditorType editorType)
        {
            return GetConfig(editorType).ConfigDirectory;
        }

        /// <summary>
        /// Concrete implementation of the Visitor pattern for editor configuration.
        /// </summary>
        private sealed class ConfigurationVisitor : IEditorConfigVisitor<EditorConfig>
        {
            public EditorConfig VisitCursor()
            {
                return new EditorConfig(
                    McpConstants.CLIENT_NAME_CURSOR,
                    UnityMcpPathResolver.GetMcpConfigPath(),
                    UnityMcpPathResolver.GetCursorConfigDirectory()
                );
            }

            public EditorConfig VisitClaudeCode()
            {
                return new EditorConfig(
                    McpConstants.CLIENT_NAME_CLAUDE_CODE,
                    UnityMcpPathResolver.GetClaudeCodeConfigPath(),
                    null // Claude Code config is in project root
                );
            }

            public EditorConfig VisitVSCode()
            {
                return new EditorConfig(
                    McpConstants.CLIENT_NAME_VSCODE,
                    UnityMcpPathResolver.GetVSCodeConfigPath(),
                    UnityMcpPathResolver.GetVSCodeConfigDirectory()
                );
            }

            public EditorConfig VisitGeminiCLI()
            {
                return new EditorConfig(
                    McpConstants.CLIENT_NAME_GEMINI_CLI,
                    UnityMcpPathResolver.GetGeminiCLIConfigPath(),
                    UnityMcpPathResolver.GetGeminiConfigDirectory()
                );
            }

            public EditorConfig VisitWindsurf()
            {
                return new EditorConfig(
                    McpConstants.CLIENT_NAME_WINDSURF,
                    UnityMcpPathResolver.GetWindsurfConfigPath(),
                    UnityMcpPathResolver.GetWindsurfConfigDirectory()
                );
            }

            public EditorConfig VisitCodex()
            {
                return new EditorConfig(
                    McpConstants.CLIENT_NAME_CODEX,
                    UnityMcpPathResolver.GetCodexConfigPath(),
                    UnityMcpPathResolver.GetCodexConfigDirectory()
                );
            }

            public EditorConfig VisitMcpInspector()
            {
                return new EditorConfig(
                    McpConstants.CLIENT_NAME_MCP_INSPECTOR,
                    UnityMcpPathResolver.GetMcpInspectorConfigPath(),
                    null // MCP Inspector config is in project root
                );
            }
        }
    }

    /// <summary>
    /// Extension methods for McpEditorType to support the Visitor pattern.
    /// </summary>
    public static class McpEditorTypeExtensions
    {
        /// <summary>
        /// Accepts a visitor and dispatches to the appropriate Visit method based on the editor type.
        /// This method eliminates the need for switch statements when working with McpEditorType.
        /// </summary>
        /// <typeparam name="T">The return type of the visitor operation</typeparam>
        /// <param name="editorType">The editor type to dispatch</param>
        /// <param name="visitor">The visitor to accept</param>
        /// <returns>The result of the visitor operation</returns>
        /// <exception cref="ArgumentException">Thrown when the editor type is not supported</exception>
        public static T Accept<T>(this McpEditorType editorType, IEditorConfigVisitor<T> visitor)
        {
            return editorType switch
            {
                McpEditorType.Cursor => visitor.VisitCursor(),
                McpEditorType.ClaudeCode => visitor.VisitClaudeCode(),
                McpEditorType.VSCode => visitor.VisitVSCode(),
                McpEditorType.GeminiCLI => visitor.VisitGeminiCLI(),
                McpEditorType.Windsurf => visitor.VisitWindsurf(),
                McpEditorType.Codex => visitor.VisitCodex(),
                McpEditorType.McpInspector => visitor.VisitMcpInspector(),
                _ => throw new ArgumentOutOfRangeException(nameof(editorType), editorType, null),
            };
        }
    }
}