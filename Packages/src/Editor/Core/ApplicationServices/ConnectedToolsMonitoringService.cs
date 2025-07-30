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
        private static List<ConnectedLLMToolData> _toolsBackup;

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
            McpBridgeServer.OnAllToolsCleared += OnAllToolsCleared;
        }

        /// <summary>
        /// Handle server stopping event - backup connected tools
        /// </summary>
        private static void OnServerStopping()
        {
            _toolsBackup = _connectedTools
                .Where(tool => tool.Name != McpConstants.UNKNOWN_CLIENT_NAME)
                .ToList();
                
            // Ensure settings are up to date
            SyncConnectedToolsToSettings();
        }

        /// <summary>
        /// Handle server started event - restore connected tools
        /// </summary>
        private static void OnServerStarted()
        {
            if (_toolsBackup != null && _toolsBackup.Count > 0)
            {
                RestoreConnectedTools(_toolsBackup);
                _toolsBackup = null;
            }
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
        /// Handle all tools cleared event - clear all connected tools
        /// </summary>
        private static void OnAllToolsCleared()
        {
            ClearConnectedTools();
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

            // Remove existing tool if present, then add
            _connectedTools.RemoveAll(tool => tool.Name == client.ClientName);

            ConnectedLLMToolData toolData = new(
                client.ClientName,
                client.Endpoint,
                client.Port,
                client.ConnectedAt
            );
            _connectedTools.Add(toolData);
            
            // Persist to settings
            McpEditorSettings.AddConnectedLLMTool(toolData);
            
            // Notify UI
            OnConnectedToolsChanged?.Invoke();
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
            return new ConnectedClient(toolData.Endpoint, null, toolData.Port, toolData.Name);
        }

        /// <summary>
        /// Restore connected tools from backup after server restart
        /// First restore all tools immediately, then cleanup disconnected ones after a delay
        /// </summary>
        public static void RestoreConnectedTools(List<ConnectedLLMToolData> backup)
        {
            if (backup == null || backup.Count == 0)
            {
                return;
            }

            // Immediately restore all tools to prevent "No connected tools found" flash
            foreach (ConnectedLLMToolData toolData in backup)
            {
                ConnectedClient restoredClient = new(toolData.Endpoint, null, toolData.Port, toolData.Name);
                AddConnectedTool(restoredClient);
            }

            // Schedule cleanup after a short delay to remove actually disconnected tools
            DelayedCleanupAsync().ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    EditorApplication.delayCall += () =>
                    {
                        Debug.LogError($"[uLoopMCP] Failed to perform delayed cleanup: {task.Exception?.GetBaseException().Message}");
                    };
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>
        /// Clean up disconnected tools after a delay
        /// </summary>
        private static async Task DelayedCleanupAsync()
        {
            // Wait 8 seconds for clients to reconnect
            await TimerDelay.Wait(8000);

            if (!McpServerController.IsServerRunning)
            {
                return;
            }

            // Get actually connected clients
            IReadOnlyCollection<ConnectedClient> actualConnectedClients = McpServerController.CurrentServer?.GetConnectedClients();
            if (actualConnectedClients == null)
            {
                return;
            }

            // Get list of actually connected client names
            HashSet<string> actualClientNames = new HashSet<string>(
                actualConnectedClients
                    .Where(client => client.ClientName != McpConstants.UNKNOWN_CLIENT_NAME)
                    .Select(client => client.ClientName)
            );

            // Remove tools that are no longer connected
            List<ConnectedLLMToolData> toolsToRemove = _connectedTools
                .Where(tool => !actualClientNames.Contains(tool.Name))
                .ToList();

            foreach (ConnectedLLMToolData tool in toolsToRemove)
            {
                RemoveConnectedTool(tool.Name);
            }
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
    }
}