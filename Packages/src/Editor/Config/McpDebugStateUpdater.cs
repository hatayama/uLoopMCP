using System;
using System.Linq;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Automatically updates MCP configuration files when ULOOPMCP_DEBUG scripting define symbol changes
    /// 
    /// Design document reference: Packages/src/Editor/ARCHITECTURE.md
    /// 
    /// Related classes:
    /// - McpServerController: Calls this class during OnAfterAssemblyReload
    /// - McpConfigService: Used to update individual editor configurations
    /// - ULoopMCPDebugToggle: Provides the debug symbol toggle functionality
    /// - McpConfigServiceFactory: Creates configuration services for different editors
    /// </summary>
    public static class McpDebugStateUpdater
    {

        /// <summary>
        /// Updates all configured MCP editor settings to match current ULOOPMCP_DEBUG state
        /// Called during domain reload to ensure configurations stay in sync
        /// </summary>
        public static void UpdateAllConfigurationsForDebugState()
        {
            bool isDebugMode = IsDebugModeEnabled();
            string correlationId = VibeLogger.GenerateCorrelationId();
            
            VibeLogger.LogInfo(
                "debug_state_update_start",
                $"Starting MCP configuration update for debug state: {isDebugMode}",
                new { debug_mode = isDebugMode },
                correlationId,
                "Automatic update triggered by domain reload",
                "Monitor which editor configurations are updated"
            );

            int updatedCount = 0;
            McpConfigServiceFactory factory = new();

            // Check all editor types for existing configurations
            foreach (McpEditorType editorType in Enum.GetValues(typeof(McpEditorType)))
            {
                McpConfigService configService = factory.GetConfigService(editorType);
                
                // Only update if configuration already exists
                if (configService.IsConfigured())
                {
                    // Get the current port from existing configuration
                    int currentPort = configService.GetConfiguredPort();
                    
                    // Update the configuration to match debug state
                    configService.UpdateDevelopmentSettings(currentPort, false, isDebugMode);
                    updatedCount++;
                    
                    VibeLogger.LogInfo(
                        "debug_state_editor_updated",
                        $"Updated {editorType} configuration for debug mode: {isDebugMode}",
                        new { editor_type = editorType.ToString(), port = currentPort, debug_mode = isDebugMode },
                        correlationId,
                        $"{editorType} settings synchronized with debug state",
                        "Verify that MCP_DEBUG environment variable is correctly set"
                    );
                }
            }

            VibeLogger.LogInfo(
                "debug_state_update_complete",
                $"Completed MCP configuration update. Updated {updatedCount} editor configurations.",
                new { debug_mode = isDebugMode, updated_count = updatedCount },
                correlationId,
                "Automatic debug state synchronization completed",
                "All configured editors should now have consistent debug settings"
            );
        }

        /// <summary>
        /// Check if ULOOPMCP_DEBUG scripting define symbol is currently defined
        /// </summary>
        /// <returns>True if debug mode is enabled, false otherwise</returns>
        private static bool IsDebugModeEnabled()
        {
            bool isULoopMcpDebug;
#if ULOOPMCP_DEBUG
            isULoopMcpDebug = true;
#else
            isULoopMcpDebug = false;
#endif
            return isULoopMcpDebug;
        }
    }
}