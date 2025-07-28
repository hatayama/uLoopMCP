using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
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

        // New: Active TCP connection management (Fowler: Add Field refactoring)
        // This runs parallel to existing _connectedTools without affecting current functionality
        private static readonly ConcurrentDictionary<string, NetworkStream> ActiveConnections = new();

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
            var stackTrace = new System.Diagnostics.StackTrace(1, true);
            var callerInfo = stackTrace.GetFrames()?.Take(5)
                .Select(frame => new {
                    method = $"{frame.GetMethod()?.DeclaringType?.Name}.{frame.GetMethod()?.Name}",
                    file = System.IO.Path.GetFileName(frame.GetFileName()),
                    line = frame.GetFileLineNumber()
                })
                .Where(info => !string.IsNullOrEmpty(info.file))
                .ToArray();

            VibeLogger.LogInfo(
                "add_or_update_tool_called",
                "AddOrUpdateTool method called",
                new { clientName, endpoint, notificationPort, callerStack = callerInfo }
            );

            if (string.IsNullOrEmpty(clientName) || string.IsNullOrEmpty(endpoint))
            {
                VibeLogger.LogWarning(
                    "add_or_update_tool_skipped",
                    "AddOrUpdateTool skipped due to invalid parameters",
                    new { clientName, endpoint, notificationPort }
                );
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
            
            VibeLogger.LogInfo(
                "connected_tools_list_updated",
                "Connected tools list updated",
                new { 
                    clientName, 
                    endpoint, 
                    notificationPort, 
                    currentCount = _connectedTools.Count,
                    allTools = _connectedTools.Select(t => new { t.Name, t.Endpoint, t.NotificationPort }).ToArray()
                }
            );
            
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
                // Update existing tool with correct name
                AddOrUpdateTool(existingTool.Name, endpoint, notificationPort);
                
                VibeLogger.LogInfo(
                    "notification_port_updated_existing",
                    "Notification port updated for existing client",
                    new { clientName = existingTool.Name, endpoint, notificationPort }
                );
            }
            else
            {
                VibeLogger.LogWarning(
                    "notification_port_update_no_client",
                    "Cannot update notification port - client not found",
                    new { endpoint, notificationPort }
                );
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

            // NEW: Also clean up TCP connection (Fowler: Expand Interface)
            // This addition runs parallel to existing UI cleanup without affecting it
            RemoveActiveConnection(endpoint);
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
        public static List<ConnectedClient> GetConnectedToolsAsClients()
        {
            var result = _connectedTools.OrderBy(tool => tool.Name).Select(tool => ConvertToConnectedClient(tool)).ToList();
            
            VibeLogger.LogInfo(
                "get_connected_tools_as_clients_called",
                "GetConnectedToolsAsClients called",
                new { 
                    connectedToolsCount = _connectedTools.Count,
                    resultCount = result.Count,
                    allTools = _connectedTools.Select(t => new { t.Name, t.Endpoint, t.NotificationPort }).ToArray(),
                    resultTools = result.Select(r => new { r.ClientName, r.Endpoint, r.NotificationPort }).ToArray()
                }
            );
            
            return result;
        }

        /// <summary>
        /// Get connected tools for UI display with flash prevention
        /// Returns previous tools during delay period
        /// </summary>
        public static List<ConnectedClient> GetConnectedToolsForDisplay()
        {
            // TEMPORARY: Disable UI flash prevention to fix display issues
            // TODO: Investigate TimerDelay.Wait cancellation issues
            // Debug.Log($"[hatayama] _isDisplayDelayActive: {_isDisplayDelayActive} (UI flash prevention disabled)");
            
            // Always show current tools (bypassing flash prevention)
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

        // =============================================================================
        // NEW: Active TCP Connection Management (Fowler: Extract Method)
        // These methods run parallel to existing functionality without interference
        // =============================================================================

        /// <summary>
        /// Updates active TCP connection for an endpoint (Fowler: Extract Method)
        /// Safely manages NetworkStream lifecycle without affecting existing UI data
        /// </summary>
        /// <param name="endpoint">Client endpoint identifier</param>
        /// <param name="stream">Active NetworkStream for the connection</param>
        public static void UpdateActiveConnection(string endpoint, NetworkStream stream)
        {
            if (string.IsNullOrEmpty(endpoint) || stream == null)
            {
                VibeLogger.LogWarning(
                    "update_active_connection_invalid_params",
                    "UpdateActiveConnection called with invalid parameters",
                    new { endpoint, streamIsNull = stream == null }
                );
                return;
            }

            // Safely add or update connection with proper cleanup of old streams
            ActiveConnections.AddOrUpdate(endpoint, stream, (key, oldStream) =>
            {
                // Clean up old connection if it exists
                try
                {
                    if (oldStream?.CanWrite == true)
                    {
                        oldStream.Close();
                    }
                }
                catch (Exception ex)
                {
                    VibeLogger.LogWarning(
                        "old_connection_cleanup_failed", 
                        "Failed to cleanup old connection during update", 
                        new { endpoint, error = ex.Message }
                    );
                }
                return stream;
            });

            VibeLogger.LogInfo(
                "active_connection_updated",
                "Active TCP connection updated successfully",
                new { endpoint, streamCanWrite = stream.CanWrite }
            );
        }

        /// <summary>
        /// Gets active TCP connections for notification sending (Fowler: Extract Method)
        /// Returns only valid connections, automatically cleaning up invalid ones
        /// </summary>
        /// <returns>Read-only dictionary of valid endpoint-to-stream mappings</returns>
        public static IReadOnlyDictionary<string, NetworkStream> GetActiveConnections()
        {
            Dictionary<string, NetworkStream> validConnections = new();
            List<string> invalidEndpoints = new();

            // Check each connection for validity
            foreach (KeyValuePair<string, NetworkStream> connection in ActiveConnections)
            {
                if (connection.Value?.CanWrite == true)
                {
                    validConnections[connection.Key] = connection.Value;
                }
                else
                {
                    invalidEndpoints.Add(connection.Key);

                    VibeLogger.LogInfo(
                        "invalid_connection_detected",
                        "Invalid connection detected during GetActiveConnections",
                        new { endpoint = connection.Key, streamIsNull = connection.Value == null }
                    );
                }
            }

            // Clean up invalid connections
            foreach (string endpoint in invalidEndpoints)
            {
                RemoveActiveConnection(endpoint);
            }

            VibeLogger.LogInfo(
                "active_connections_retrieved",
                "Active connections retrieved with cleanup",
                new { validCount = validConnections.Count, removedCount = invalidEndpoints.Count }
            );

            return validConnections;
        }

        /// <summary>
        /// Removes active TCP connection and cleans up resources (Fowler: Extract Method)
        /// Private method for internal connection lifecycle management
        /// </summary>
        /// <param name="endpoint">Endpoint identifier to remove</param>
        private static void RemoveActiveConnection(string endpoint)
        {
            if (string.IsNullOrEmpty(endpoint))
            {
                return;
            }

            if (ActiveConnections.TryRemove(endpoint, out NetworkStream removedStream))
            {
                // Safe cleanup of NetworkStream
                try
                {
                    if (removedStream?.CanWrite == true)
                    {
                        removedStream.Close();
                    }
                }
                catch (Exception ex)
                {
                    VibeLogger.LogWarning(
                        "connection_cleanup_failed", 
                        "Failed to cleanup removed connection", 
                        new { endpoint, error = ex.Message }
                    );
                }

                VibeLogger.LogInfo(
                    "active_connection_removed", 
                    "Active TCP connection removed successfully", 
                    new { endpoint }
                );
            }
        }
    }
}