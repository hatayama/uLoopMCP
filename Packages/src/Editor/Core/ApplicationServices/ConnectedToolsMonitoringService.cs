using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        private static List<ConnectedLLMToolData> _previousToolsForDisplay = new();
        private static bool _isDisplayDelayActive = false;
        private static CancellationTokenSource _displayDelayCancellation;

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
        /// Uses endpoint-based removal for more reliable disconnection handling
        /// 
        /// NOTE: Parameter is endpoint, not clientName. Do not rely on clientName 
        /// for deletion as it may be "Unknown Client" during disconnection.
        /// </summary>
        private static void OnToolDisconnected(string endpoint)
        {
            RemoveConnectedToolByEndpoint(endpoint);
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

            AddOrUpdateTool(client.ClientName, client.Endpoint, client.NotificationPort, client.ConnectedAt);
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
            VibeLogger.LogInfo(
                "ui_notification_triggered",
                "Triggering UI update for connected tools change",
                new { clientName, endpoint, notificationPort }
            );
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
        /// Remove a connected LLM tool by name
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
        /// Remove a connected LLM tool by endpoint
        /// Used when client name is unknown or unreliable during disconnection
        /// 
        /// IMPORTANT: Do not rely on clientName for deletion during disconnection.
        /// Client names may be "Unknown Client" or unreliable when connections are lost.
        /// Always use endpoint-based deletion for reliable removal.
        /// </summary>
        public static void RemoveConnectedToolByEndpoint(string endpoint)
        {
            if (string.IsNullOrEmpty(endpoint))
            {
                return;
            }

            ConnectedLLMToolData toolToRemove = _connectedTools.Find(tool => tool.Endpoint == endpoint);
            if (toolToRemove != null)
            {
                _connectedTools.RemoveAll(tool => tool.Endpoint == endpoint);
                
                // Persist to settings using endpoint-based removal
                McpEditorSettings.RemoveConnectedLLMToolByEndpoint(endpoint);
                
                VibeLogger.LogInfo(
                    "connected_tool_removed_by_endpoint",
                    "Connected tool removed by endpoint during disconnection",
                    new { endpoint, removedToolName = toolToRemove.Name }
                );
                
                // Notify UI
                OnConnectedToolsChanged?.Invoke();
            }
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
        /// Get connected tools for UI display with flash prevention
        /// Returns previous tools during delay period
        /// </summary>
        public static IEnumerable<ConnectedClient> GetConnectedToolsForDisplay()
        {
            // If delay is active, show previous tools
            if (_isDisplayDelayActive)
            {
                return _previousToolsForDisplay.OrderBy(tool => tool.Name)
                    .Select(tool => ConvertToConnectedClient(tool));
            }
            
            // Normal display
            return GetConnectedToolsAsClients();
        }

        /// <summary>
        /// Backup current tools for display continuity before clearing
        /// Uses TimerDelay for 1-second delay with cancellation support
        /// </summary>
        private static async void BackupToolsForDisplay()
        {
            // Cancel any existing delay operation
            _displayDelayCancellation?.Cancel();
            _displayDelayCancellation?.Dispose();
            _displayDelayCancellation = new();
            
            _previousToolsForDisplay = _connectedTools
                .Where(tool => tool.Name != McpConstants.UNKNOWN_CLIENT_NAME)
                .ToList();
            
            _isDisplayDelayActive = true;
            
            VibeLogger.LogInfo(
                "ui_flash_prevention_activated",
                "UI flash prevention activated - showing previous tools for 1 second",
                new { previousToolCount = _previousToolsForDisplay.Count }
            );
            
            try
            {
                // Wait 1 second using TimerDelay with cancellation
                await TimerDelay.Wait(McpConstants.UI.FLASH_PREVENTION_DELAY_MS, _displayDelayCancellation.Token);
                
                // Only update if not cancelled
                if (_displayDelayCancellation != null && !_displayDelayCancellation.Token.IsCancellationRequested)
                {
                    _isDisplayDelayActive = false;
                    OnConnectedToolsChanged?.Invoke();
                    
                    VibeLogger.LogInfo(
                        "ui_flash_prevention_deactivated",
                        "UI flash prevention deactivated - showing current tools",
                        new { }
                    );
                }
            }
            catch (OperationCanceledException)
            {
                VibeLogger.LogInfo(
                    "ui_flash_prevention_cancelled",
                    "UI flash prevention cancelled by new operation",
                    new { }
                );
            }
            finally
            {
                _displayDelayCancellation?.Dispose();
                _displayDelayCancellation = null;
            }
        }

        /// <summary>
        /// Convert stored tool data to ConnectedClient for UI display
        /// </summary>
        private static ConnectedClient ConvertToConnectedClient(ConnectedLLMToolData toolData)
        {
            VibeLogger.LogInfo(
                "convert_to_connected_client",
                "Converting tool data to ConnectedClient for UI display",
                new { 
                    name = toolData.Name, 
                    endpoint = toolData.Endpoint, 
                    notificationPort = toolData.NotificationPort 
                }
            );
            
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
                // Backup tools for display before clearing
                BackupToolsForDisplay();
                
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