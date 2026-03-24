using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

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
            SynchronizeConnectedToolsWithCurrentServer();
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
        /// Handle server stopping event - clear live-only connected tools state
        /// </summary>
        private static void OnServerStopping()
        {
            ClearConnectedTools();
        }

        /// <summary>
        /// Handle server started event - rebuild connected tools from the current live server state
        /// </summary>
        private static void OnServerStarted()
        {
            SynchronizeConnectedToolsWithCurrentServer();
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
        /// Rebuild connected tools from the current live server state.
        /// The connected tools UI must reflect live MCP connections only.
        /// </summary>
        private static void SynchronizeConnectedToolsWithCurrentServer()
        {
            IReadOnlyCollection<ConnectedClient> liveClients = GetLiveConnectedClients();
            ReplaceConnectedTools(liveClients);
        }

        /// <summary>
        /// Get live clients from the current server, ignoring persisted settings.
        /// </summary>
        private static IReadOnlyCollection<ConnectedClient> GetLiveConnectedClients()
        {
            if (!McpServerController.IsServerRunning)
            {
                return Array.Empty<ConnectedClient>();
            }

            IReadOnlyCollection<ConnectedClient> connectedClients = McpServerController.CurrentServer?.GetConnectedClients();
            return connectedClients ?? Array.Empty<ConnectedClient>();
        }

        /// <summary>
        /// Replace the in-memory and persisted connected tools state with the current live clients.
        /// </summary>
        private static void ReplaceConnectedTools(IEnumerable<ConnectedClient> connectedClients)
        {
            List<ConnectedLLMToolData> tools = connectedClients
                .Where(client => client != null && client.ClientName != McpConstants.UNKNOWN_CLIENT_NAME)
                .Select(client => new ConnectedLLMToolData(
                    client.ClientName,
                    client.Endpoint,
                    client.Port,
                    client.ConnectedAt
                ))
                .GroupBy(tool => tool.Name)
                .Select(group => group.Last())
                .OrderBy(tool => tool.Name)
                .ToList();

            _connectedTools = tools;

            ConnectedLLMToolData[] toolsArray = _connectedTools
                .Where(tool => tool != null && !string.IsNullOrEmpty(tool.Name))
                .ToArray();
            McpEditorSettings.SetConnectedLLMTools(toolsArray);

            OnConnectedToolsChanged?.Invoke();
        }

        internal static void ReplaceConnectedToolsForTests(IReadOnlyCollection<ConnectedClient> connectedClients)
        {
            ReplaceConnectedTools(connectedClients);
        }

        internal static void ResetStateForTests()
        {
            _connectedTools = new List<ConnectedLLMToolData>();
        }
    }
}
