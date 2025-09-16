using System;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Automatically updates MCP configuration files when port number changes
    /// 
    /// Design document reference: Packages/src/Editor/ARCHITECTURE.md
    /// 
    /// Related classes:
    /// - McpEditorModel: Calls this class when UI port changes
    /// - McpServerController: Calls this class when server port changes due to conflicts
    /// - McpConfigService: Used to update individual editor configurations
    /// - McpConfigServiceFactory: Creates configuration services for different editors
    /// </summary>
    public static class McpPortChangeUpdater
    {
        /// <summary>
        /// Updates all configured MCP editor settings with new port number
        /// Called when port number changes in UI or due to server port conflicts
        /// </summary>
        /// <param name="newPort">The new port number to update to</param>
        /// <param name="reason">Reason for the port change (for logging)</param>
        public static void UpdateAllConfigurationsForPortChange(int newPort, string reason)
        {
            string correlationId = VibeLogger.GenerateCorrelationId();
            
            VibeLogger.LogInfo(
                "port_change_update_start",
                $"Starting MCP configuration update for port change: {newPort}",
                new { new_port = newPort, reason = reason },
                correlationId,
                $"Port change update triggered: {reason}",
                "Monitor which editor configurations are updated with new port"
            );

            int updatedCount = 0;
            McpConfigServiceFactory factory = new();

            // Check current debug mode
            bool isDebugMode;
#if ULOOPMCP_DEBUG
            isDebugMode = true;
#else
            isDebugMode = false;
#endif

            // Check all editor types for existing configurations
            foreach (McpEditorType editorType in Enum.GetValues(typeof(McpEditorType)))
            {
                IMcpConfigService configService = factory.GetConfigService(editorType);
                
                // Only update if configuration already exists
                if (configService.IsConfigured())
                {
                    // Update the configuration with new port and current debug settings
                    configService.UpdateDevelopmentSettings(newPort, false, isDebugMode);
                    updatedCount++;
                    
                    VibeLogger.LogInfo(
                        "port_change_editor_updated",
                        $"Updated {editorType} configuration with new port: {newPort}",
                        new { editor_type = editorType.ToString(), new_port = newPort, debug_mode = isDebugMode, reason = reason },
                        correlationId,
                        $"{editorType} settings updated with new port",
                        "Verify that port number is correctly updated in configuration file"
                    );
                }
            }

            VibeLogger.LogInfo(
                "port_change_update_complete",
                $"Completed MCP configuration port update. Updated {updatedCount} editor configurations with port {newPort}.",
                new { new_port = newPort, updated_count = updatedCount, reason = reason },
                correlationId,
                "Port change synchronization completed",
                "All configured editors should now have the updated port number"
            );
        }
    }
}