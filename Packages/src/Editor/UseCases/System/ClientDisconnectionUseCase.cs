using System;
using System.Linq;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Handles client disconnection with proper cleanup of settings and display.
    /// Follows Martin Fowler's Transaction Script pattern.
    /// 
    /// Design principles:
    /// - Single Responsibility: Only handles client disconnection
    /// - High Cohesion: All disconnection logic in one place
    /// - Clean Architecture: UseCase layer manages business logic
    /// 
    /// Related classes:
    /// - McpEditorSettings: Persistent storage for connected tools
    /// - McpEditorWindow: Display management for connected tools
    /// - McpBridgeServer: TCP server that detects disconnections
    /// </summary>
    public class ClientDisconnectionUseCase
    {
        /// <summary>
        /// Execute client disconnection with cleanup
        /// </summary>
        public void Execute(string clientEndpoint)
        {
            if (string.IsNullOrEmpty(clientEndpoint))
            {
                VibeLogger.LogWarning(
                    "client_disconnection_invalid_endpoint",
                    "Invalid endpoint for disconnection",
                    new { }
                );
                return;
            }

            VibeLogger.LogInfo(
                "client_disconnection_start",
                $"Processing client disconnection: {clientEndpoint}",
                new { clientEndpoint }
            );

            // Step 1: Get client info before removal
            ConnectedLLMToolData[] tools = McpEditorSettings.GetConnectedLLMTools();
            ConnectedLLMToolData toolToRemove = tools?.FirstOrDefault(t => t.Endpoint == clientEndpoint);
            
            if (toolToRemove == null)
            {
                VibeLogger.LogWarning(
                    "client_disconnection_not_found",
                    $"Client not found in settings: {clientEndpoint}",
                    new { clientEndpoint }
                );
                return;
            }

            // Step 2: Remove from persistent storage
            McpEditorSettings.RemoveConnectedLLMToolByEndpoint(clientEndpoint);
            
            VibeLogger.LogInfo(
                "client_disconnection_removed_from_settings",
                $"Removed client from settings: {toolToRemove.Name}",
                new { 
                    clientName = toolToRemove.Name,
                    endpoint = clientEndpoint,
                    notificationPort = toolToRemove.NotificationPort
                }
            );

            // Step 3: Update display
            if (toolToRemove.NotificationPort > 0)
            {
                McpEditorWindow.Instance?.RemoveDisplayTool(toolToRemove.NotificationPort);
                
                VibeLogger.LogInfo(
                    "client_disconnection_display_updated",
                    "Display updated after disconnection",
                    new { notificationPort = toolToRemove.NotificationPort }
                );
            }

            // Step 4: Verify cleanup
            ConnectedLLMToolData[] remainingTools = McpEditorSettings.GetConnectedLLMTools();
            bool cleanupSuccess = !remainingTools.Any(t => t.Endpoint == clientEndpoint);
            
            if (!cleanupSuccess)
            {
                VibeLogger.LogError(
                    "client_disconnection_cleanup_failed",
                    "Failed to remove client from settings",
                    new { clientEndpoint }
                );
            }
            else
            {
                VibeLogger.LogInfo(
                    "client_disconnection_complete",
                    $"Client disconnection completed: {toolToRemove.Name}",
                    new { 
                        clientName = toolToRemove.Name,
                        remainingCount = remainingTools.Length 
                    }
                );
            }
        }
    }
}