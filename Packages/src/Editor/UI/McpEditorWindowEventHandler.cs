using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace io.github.hatayama.uMCP
{
    /// <summary>
    /// Event handler for McpEditorWindow - Manages Unity and server events
    /// Helper class for Presenter layer in MVP architecture
    /// Related classes:
    /// - McpEditorWindow: Main presenter that owns this handler
    /// - McpEditorModel: Model layer for state management
    /// - McpBridgeServer: Server that provides events
    /// - McpServerController: Server lifecycle management
    /// </summary>
    internal class McpEditorWindowEventHandler
    {
        private readonly McpEditorModel _model;
        private readonly McpEditorWindow _window;

        public McpEditorWindowEventHandler(McpEditorModel model, McpEditorWindow window)
        {
            _model = model;
            _window = window;
        }

        /// <summary>
        /// Initialize all event subscriptions
        /// </summary>
        public void Initialize()
        {
            SubscribeToUnityEvents();
            SubscribeToServerEvents();
        }

        /// <summary>
        /// Cleanup all event subscriptions
        /// </summary>
        public void Cleanup()
        {
            UnsubscribeFromUnityEvents();
            UnsubscribeFromServerEvents();
        }

        /// <summary>
        /// Subscribe to Unity Editor events
        /// </summary>
        private void SubscribeToUnityEvents()
        {
            McpCommunicationLogger.OnLogUpdated += OnLogUpdated;
            EditorApplication.update += OnEditorUpdate;
        }

        /// <summary>
        /// Unsubscribe from Unity Editor events
        /// </summary>
        private void UnsubscribeFromUnityEvents()
        {
            McpCommunicationLogger.OnLogUpdated -= OnLogUpdated;
            EditorApplication.update -= OnEditorUpdate;
        }

        /// <summary>
        /// Subscribe to server events for immediate UI updates
        /// </summary>
        private void SubscribeToServerEvents()
        {
            // Unsubscribe first to avoid duplicate subscriptions
            UnsubscribeFromServerEvents();

            McpBridgeServer currentServer = McpServerController.CurrentServer;
            if (currentServer != null)
            {
                currentServer.OnClientConnected += OnClientConnected;
                currentServer.OnClientDisconnected += OnClientDisconnected;
            }
        }

        /// <summary>
        /// Unsubscribe from server events
        /// </summary>
        private void UnsubscribeFromServerEvents()
        {
            McpBridgeServer currentServer = McpServerController.CurrentServer;
            if (currentServer != null)
            {
                currentServer.OnClientConnected -= OnClientConnected;
                currentServer.OnClientDisconnected -= OnClientDisconnected;
            }
        }

        /// <summary>
        /// Handle log update event - trigger UI repaint
        /// </summary>
        private void OnLogUpdated()
        {
            _window.Repaint();
        }

        /// <summary>
        /// Handle client connection event - force UI repaint for immediate update
        /// </summary>
        private void OnClientConnected(string clientEndpoint)
        {
            // Enhanced logging for debugging client connection
            var connectedClients = McpServerController.CurrentServer?.GetConnectedClients();
            int totalCount = connectedClients?.Count ?? 0;
            
            McpLogger.LogInfo($"[CLIENT_CONNECT] Client connected: {clientEndpoint}");
            McpLogger.LogInfo($"[CLIENT_CONNECT] Total clients: {totalCount}");
            
            if (connectedClients != null && connectedClients.Count > 0)
            {
                foreach (var client in connectedClients)
                {
                    McpLogger.LogInfo($"[CLIENT_CONNECT] Connected: {client.ClientName} ({client.Endpoint}, PID: {client.ProcessId})");
                }
            }
            
            // Clear reconnecting flags when client connects
            McpServerController.ClearReconnectingFlag();
            
            // Mark that repaint is needed since events are called from background thread
            _model.RequestRepaint();

            // Exit post-compile mode when client connects
            if (_model.Runtime.IsPostCompileMode)
            {
                _model.DisablePostCompileMode();
            }
        }

        /// <summary>
        /// Handle client disconnection event - force UI repaint for immediate update
        /// </summary>
        private void OnClientDisconnected(string clientEndpoint)
        {
            // Enhanced logging for debugging client disconnection issues
            var connectedClients = McpServerController.CurrentServer?.GetConnectedClients();
            int remainingCount = connectedClients?.Count ?? 0;
            
            McpLogger.LogInfo($"[CLIENT_DISCONNECT] Client disconnected: {clientEndpoint}");
            McpLogger.LogInfo($"[CLIENT_DISCONNECT] Remaining clients: {remainingCount}");
            
            if (connectedClients != null && connectedClients.Count > 0)
            {
                foreach (var client in connectedClients)
                {
                    McpLogger.LogInfo($"[CLIENT_DISCONNECT] Still connected: {client.ClientName} ({client.Endpoint}, PID: {client.ProcessId})");
                }
            }
            
            // Mark that repaint is needed since events are called from background thread
            _model.RequestRepaint();
        }

        /// <summary>
        /// Called from EditorApplication.update - handles UI refresh even when Unity is not focused
        /// </summary>
        private void OnEditorUpdate()
        {
            // Always check for server state changes
            CheckServerStateChanges();

            // In post-compile mode, always repaint for immediate updates
            if (_model.Runtime.IsPostCompileMode)
            {
                _window.Repaint();
                return;
            }

            // Normal mode: repaint only when needed
            if (_model.Runtime.NeedsRepaint)
            {
                _model.ClearRepaintRequest();
                _window.Repaint();
            }
        }

        /// <summary>
        /// Check if server state has changed and mark repaint if needed
        /// </summary>
        private void CheckServerStateChanges()
        {
            (bool isRunning, int port, bool wasRestored) = McpServerController.GetServerStatus();
            var connectedClients = McpServerController.CurrentServer?.GetConnectedClients();
            int connectedCount = connectedClients?.Count ?? 0;

            // Generate hash of client information to detect changes in client names
            string clientsInfoHash = GenerateClientsInfoHash(connectedClients);

            // Check if any server state has changed
            if (isRunning != _model.Runtime.LastServerRunning ||
                port != _model.Runtime.LastServerPort ||
                connectedCount != _model.Runtime.LastConnectedClientsCount ||
                clientsInfoHash != _model.Runtime.LastClientsInfoHash)
            {
                _model.UpdateServerStateTracking(isRunning, port, connectedCount, clientsInfoHash);
                _model.RequestRepaint();
            }
        }

        /// <summary>
        /// Generate hash string from client information to detect changes
        /// </summary>
        private string GenerateClientsInfoHash(IReadOnlyCollection<ConnectedClient> clients)
        {
            if (clients == null || clients.Count == 0)
            {
                return "empty";
            }

            // Create a hash based on endpoint, client name, and process ID for unique identification
            var info = clients.Select(c => $"{c.Endpoint}:{c.ClientName}:{c.ProcessId}").OrderBy(s => s);
            return string.Join("|", info);
        }

        /// <summary>
        /// Re-subscribe to server events (called after server start)
        /// </summary>
        public void RefreshServerEventSubscriptions()
        {
            SubscribeToServerEvents();
        }
    }
} 