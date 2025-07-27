using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Application service responsible for monitoring connected LLM tools
    /// Single responsibility: Track connected/disconnected tools and persist state
    /// Related classes: McpBridgeServer, McpEditorSettings, ConnectedLLMToolData
    /// Design reference: @Packages/docs/ARCHITECTURE_Unity.md - Application Service Layer (Single Function Implementation)
    /// </summary>
    [InitializeOnLoad]
    public static class ConnectedToolsMonitoringService
    {
        private static List<ConnectedLLMToolData> _connectedTools = new();

        // Events for UI notification
        public static event System.Action OnConnectedToolsChanged;

        static ConnectedToolsMonitoringService()
        {
            Initialize();
        }

        /// <summary>
        /// Initialize the monitoring service
        /// </summary>
        private static void Initialize()
        {
            SubscribeToServerEvents();
            RestoreConnectedToolsFromSettings();
        }

        /// <summary>
        /// Subscribe to server lifecycle events
        /// </summary>
        private static void SubscribeToServerEvents()
        {
            McpBridgeServer.OnServerStopping += OnServerStopping;
            McpBridgeServer.OnServerStarted += OnServerStarted;
            McpBridgeServer.OnToolConnected += OnToolConnected;
            McpBridgeServer.OnToolDisconnected += OnToolDisconnected;
        }

        /// <summary>
        /// Handle server stopping event - backup connected tools
        /// </summary>
        private static void OnServerStopping()
        {
            // Ensure settings are up to date
            SyncConnectedToolsToSettings();
        }

        /// <summary>
        /// Handle server started event - restore connected tools
        /// </summary>
        private static void OnServerStarted()
        {
            // Send restart notification to existing clients (similar to domain reload)
            SendServerRestartNotification();
        }

        /// <summary>
        /// Handle tool connected event - add tool to connected list
        /// </summary>
        private static void OnToolConnected(ConnectedClient client)
        {
            AddConnectedTool(client);
        }

        /// <summary>
        /// Handle tool disconnected event - remove tool from connected list
        /// </summary>
        private static void OnToolDisconnected(string toolName)
        {
            RemoveConnectedTool(toolName);
        }


        /// <summary>
        /// Add a connected LLM tool
        /// </summary>
        public static void AddConnectedTool(ConnectedClient client)
        {
            if (client.ClientName == McpConstants.UNKNOWN_CLIENT_NAME)
            {
                return;
            }

            AddOrUpdateTool(client.ClientName, client.Endpoint, 0, client.ConnectedAt);
        }

        /// <summary>
        /// Add or update a connected tool with notification port
        /// Unified method for all tool management operations
        /// </summary>
        public static void AddOrUpdateTool(string clientName, string endpoint, int notificationPort, DateTime? connectedAt = null)
        {
            if (string.IsNullOrEmpty(clientName) || string.IsNullOrEmpty(endpoint))
            {
                return;
            }

            // Remove existing tool with same endpoint if present
            _connectedTools.RemoveAll(tool => tool.Endpoint == endpoint);

            // Remove existing tool with same notification port if present (port conflict prevention)
            if (notificationPort > 0)
            {
                ConnectedLLMToolData conflictingTool = _connectedTools.Find(tool => tool.NotificationPort == notificationPort);
                if (conflictingTool != null)
                {
                    VibeLogger.LogWarning(
                        "notification_port_conflict_detected",
                        "Removing tool with conflicting notification port",
                        new { 
                            conflicting_endpoint = conflictingTool.Endpoint,
                            conflicting_client = conflictingTool.Name,
                            new_endpoint = endpoint,
                            new_client = clientName,
                            notification_port = notificationPort 
                        }
                    );
                    _connectedTools.RemoveAll(tool => tool.NotificationPort == notificationPort);
                }
            }

            // Check if there's already a notification port if not provided
            if (notificationPort == 0)
            {
                int? existingNotificationPort = McpEditorSettings.GetClientNotificationPort(endpoint);
                notificationPort = existingNotificationPort ?? 0;
            }

            ConnectedLLMToolData toolData = new(
                clientName,
                endpoint,
                connectedAt ?? DateTime.Now,
                notificationPort
            );
            _connectedTools.Add(toolData);
            
            // Persist to settings
            McpEditorSettings.AddConnectedLLMTool(toolData);
            
            VibeLogger.LogInfo(
                "connected_tool_added_or_updated",
                "Connected tool added or updated",
                new { clientName, endpoint, notificationPort }
            );
            
            // Notify UI
            OnConnectedToolsChanged?.Invoke();
        }

        /// <summary>
        /// Update notification port for a connected tool
        /// </summary>
        public static void UpdateNotificationPort(string endpoint, int notificationPort)
        {
            if (string.IsNullOrEmpty(endpoint))
            {
                return;
            }

            // Find existing tool
            ConnectedLLMToolData existingTool = _connectedTools.Find(tool => tool.Endpoint == endpoint);
            if (existingTool != null)
            {
                // Update existing tool
                AddOrUpdateTool(existingTool.Name, endpoint, notificationPort);
            }
            else
            {
                // Create new tool entry with unknown name (will be updated when client connects)
                AddOrUpdateTool("unknown", endpoint, notificationPort);
            }
        }

        /// <summary>
        /// Remove a connected LLM tool
        /// </summary>
        public static void RemoveConnectedTool(string toolName)
        {
            _connectedTools.RemoveAll(tool => tool.Name == toolName);
            
            // Persist to settings
            McpEditorSettings.RemoveConnectedLLMTool(toolName);
            
            // Notify UI
            OnConnectedToolsChanged?.Invoke();
        }

        /// <summary>
        /// Clear all connected LLM tools
        /// </summary>
        public static void ClearConnectedTools()
        {
            _connectedTools.Clear();
            
            // Persist to settings
            McpEditorSettings.ClearConnectedLLMTools();
            
            // Notify UI
            OnConnectedToolsChanged?.Invoke();
        }

        /// <summary>
        /// Get connected tools as ConnectedClient objects for UI display, sorted by name
        /// </summary>
        public static IEnumerable<ConnectedClient> GetConnectedToolsAsClients()
        {
            return _connectedTools.OrderBy(tool => tool.Name).Select(tool => ConvertToConnectedClient(tool));
        }

        /// <summary>
        /// Convert stored tool data to ConnectedClient for UI display
        /// </summary>
        private static ConnectedClient ConvertToConnectedClient(ConnectedLLMToolData toolData)
        {
            return new ConnectedClient(toolData.Endpoint, null, toolData.Name, toolData.NotificationPort);
        }


        /// <summary>
        /// Restore connected tools from persistent settings
        /// </summary>
        private static void RestoreConnectedToolsFromSettings()
        {
            ConnectedLLMToolData[] savedTools = McpEditorSettings.GetConnectedLLMTools();
            if (savedTools != null && savedTools.Length > 0)
            {
                _connectedTools.Clear();
                foreach (ConnectedLLMToolData toolData in savedTools)
                {
                    if (toolData != null && !string.IsNullOrEmpty(toolData.Name))
                    {
                        _connectedTools.Add(toolData);
                    }
                }
                
                // Notify UI
                OnConnectedToolsChanged?.Invoke();
            }
        }

        /// <summary>
        /// Sync current connected tools to settings
        /// </summary>
        private static void SyncConnectedToolsToSettings()
        {
            ConnectedLLMToolData[] toolsArray = _connectedTools
                .Where(tool => tool != null && !string.IsNullOrEmpty(tool.Name) && tool.Name != McpConstants.UNKNOWN_CLIENT_NAME)
                .ToArray();
            McpEditorSettings.SetConnectedLLMTools(toolsArray);
        }

        /// <summary>
        /// Send server restart notification to existing clients and clear connected tools
        /// Similar to domain reload notification mechanism
        /// </summary>
        private static async void SendServerRestartNotification()
        {
            try
            {
                using NotificationClient client = new();
                await client.SendServerRestartCompleteAsync();
                
                // Clear connected tools after sending notification
                // Living clients will re-register via setClientName
                _connectedTools.Clear();
                SyncConnectedToolsToSettings();
                OnConnectedToolsChanged?.Invoke();
                
                VibeLogger.LogInfo(
                    "server_restart_notification_complete",
                    "Server restart notification sent and connected tools cleared",
                    new { timestamp = DateTime.UtcNow }
                );
            }
            catch (Exception ex)
            {
                VibeLogger.LogError(
                    "server_restart_notification_error",
                    "Failed to send server restart notification",
                    new { error = ex.Message, type = ex.GetType().Name }
                );
            }
        }
    }
}