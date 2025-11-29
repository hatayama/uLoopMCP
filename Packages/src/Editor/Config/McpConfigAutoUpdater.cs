using System.Collections.Generic;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Class responsible for auto-updating MCP configuration files.
    /// Automatically updates configuration files when paths change after package updates.
    /// Single Responsibility Principle: Only responsible for auto-updating configuration files.
    ///
    /// Related classes:
    /// - McpConfigServiceFactory: Provides ConfigService for each editor type
    /// - McpConfigService: Handles configuration file read/write and update detection
    /// - McpServerOperations: Calls this class during server startup
    /// </summary>
    public static class McpConfigAutoUpdater
    {
        /// <summary>
        /// Checks all configured editors and updates their configuration files if needed.
        /// Automatically updates configurations when paths have changed (e.g., after package updates).
        /// </summary>
        /// <param name="port">The port number to use</param>
        public static void UpdateAllConfiguredEditors(int port)
        {
            McpConfigServiceFactory factory = new McpConfigServiceFactory();
            IReadOnlyDictionary<McpEditorType, IMcpConfigService> allServices = factory.GetAllConfigServices();

            foreach (KeyValuePair<McpEditorType, IMcpConfigService> entry in allServices)
            {
                UpdateEditorConfigIfNeeded(entry.Key, entry.Value, port);
            }
        }

        /// <summary>
        /// Updates the specified editor's configuration if needed.
        /// </summary>
        /// <param name="editorType">The editor type</param>
        /// <param name="configService">The configuration service</param>
        /// <param name="port">The port number</param>
        private static void UpdateEditorConfigIfNeeded(McpEditorType editorType, IMcpConfigService configService, int port)
        {
            bool isConfigured = configService.IsConfigured();
            if (!isConfigured)
            {
                return;
            }

            bool isUpdateNeeded = configService.IsUpdateNeeded(port);
            if (!isUpdateNeeded)
            {
                return;
            }

            configService.AutoConfigure(port);
            string editorName = GetEditorDisplayName(editorType);
            Debug.Log($"[uLoopMCP] Auto-updated {editorName} configuration (path changed)");
        }

        /// <summary>
        /// Gets the display name for the editor type.
        /// </summary>
        /// <param name="editorType">The editor type</param>
        /// <returns>The display name</returns>
        private static string GetEditorDisplayName(McpEditorType editorType)
        {
            return editorType switch
            {
                McpEditorType.Cursor => "Cursor",
                McpEditorType.ClaudeCode => "Claude Code",
                McpEditorType.VSCode => "VSCode",
                McpEditorType.GeminiCLI => "Gemini CLI",
                McpEditorType.Windsurf => "Windsurf",
                McpEditorType.Codex => "Codex",
                McpEditorType.McpInspector => "MCP Inspector",
                _ => editorType.ToString()
            };
        }
    }
}
