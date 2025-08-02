using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
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
        private readonly McpEditorWindow _window;
        
        // Runtime state tracking
        private bool _lastServerRunning;
        private int _lastServerPort;
        private int _lastConnectedClientsCount;
        private string _lastClientsInfoHash = "";

        public McpEditorWindowEventHandler(McpEditorWindow window)
        {
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
            EditorApplication.update += OnEditorUpdate;
        }

        /// <summary>
        /// Unsubscribe from Unity Editor events
        /// </summary>
        private void UnsubscribeFromUnityEvents()
        {
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
        /// Handle client connection event - force UI repaint for immediate update
        /// </summary>
        private void OnClientConnected(string clientEndpoint)
        {
            // Enhanced logging for debugging client connection
            // Count check for debugging purposes only
            
            
            // Clear reconnecting flags when client connects
            McpServerController.ClearReconnectingFlag();
            
            // Mark that repaint is needed since events are called from background thread
            _window.RequestRepaint();

            // Exit post-compile mode when client connects
            _window.DisablePostCompileMode();
        }

        /// <summary>
        /// Handle client disconnection event - force UI repaint for immediate update
        /// </summary>
        private void OnClientDisconnected(string clientEndpoint)
        {
            // Enhanced logging for debugging client disconnection issues
            // Count check for debugging purposes only
            
            
            
            // Mark that repaint is needed since events are called from background thread
            _window.RequestRepaint();
        }

        /// <summary>
        /// Called from EditorApplication.update - handles UI refresh even when Unity is not focused
        /// </summary>
        private void OnEditorUpdate()
        {
            // Always check for server state changes
            CheckServerStateChanges();

            // Always repaint if window requests it
            if (_window.NeedsRepaint())
            {
                _window.Repaint();
            }
        }

        /// <summary>
        /// Check if server state has changed and mark repaint if needed
        /// </summary>
        private void CheckServerStateChanges()
        {
            (bool isRunning, int port, bool _) = McpServerController.GetServerStatus();
            var connectedClients = McpServerController.CurrentServer?.GetConnectedClients();
            int connectedCount = connectedClients?.Count ?? 0;

            // Generate hash of client information to detect changes in client names
            string clientsInfoHash = GenerateClientsInfoHash(connectedClients);

            // Check if any server state has changed
            if (isRunning != _lastServerRunning ||
                port != _lastServerPort ||
                connectedCount != _lastConnectedClientsCount ||
                clientsInfoHash != _lastClientsInfoHash)
            {
                _lastServerRunning = isRunning;
                _lastServerPort = port;
                _lastConnectedClientsCount = connectedCount;
                _lastClientsInfoHash = clientsInfoHash;
                _window.RequestRepaint();
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

            // Create a hash based on endpoint and client name for unique identification
            var info = clients.Select(c => $"{c.Endpoint}:{c.ClientName}").OrderBy(s => s);
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