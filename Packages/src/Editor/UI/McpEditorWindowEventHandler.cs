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
        private readonly McpEditorModel _model;
        private readonly McpEditorWindow _window;
        private McpBridgeServer _currentSubscribedServer;

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
            
            // Subscribe to server instance changes
            McpServerController.OnServerInstanceChanged += OnServerInstanceChanged;
        }

        /// <summary>
        /// Cleanup all event subscriptions
        /// </summary>
        public void Cleanup()
        {
            UnsubscribeFromUnityEvents();
            UnsubscribeFromServerEvents();
            
            // Unsubscribe from server instance changes
            McpServerController.OnServerInstanceChanged -= OnServerInstanceChanged;
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
                _currentSubscribedServer = currentServer;
                UnityEngine.Debug.Log($"[uLoopMCP] Successfully subscribed to OnClientDisconnected event on server instance: {currentServer.GetHashCode()}");
            }
            else
            {
                _currentSubscribedServer = null;
                UnityEngine.Debug.LogWarning("[uLoopMCP] Cannot subscribe to events: CurrentServer is null");
            }
        }

        /// <summary>
        /// Unsubscribe from server events
        /// </summary>
        private void UnsubscribeFromServerEvents()
        {
            // Unsubscribe from the server we actually subscribed to
            if (_currentSubscribedServer != null)
            {
                _currentSubscribedServer.OnClientConnected -= OnClientConnected;
                _currentSubscribedServer.OnClientDisconnected -= OnClientDisconnected;
                UnityEngine.Debug.Log($"[uLoopMCP] Unsubscribed from server instance: {_currentSubscribedServer.GetHashCode()}");
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
            _model.RequestRepaint();

            // Exit post-compile mode when client connects
            if (_model.Runtime.IsPostCompileMode)
            {
                _model.DisablePostCompileMode();
            }
        }

        /// <summary>
        /// Handle client disconnection event using ClientDisconnectionUseCase pattern
        /// </summary>
        private void OnClientDisconnected(string clientEndpoint)
        {
            // Execute disconnection handling using UseCase pattern on main thread
            // This ensures proper temporal cohesion and error handling
            ClientDisconnectionUseCase.ExecuteOnMainThread(
                clientEndpoint, 
                () => _model.RequestRepaint() // UI repaint callback
            );
        }

        /// <summary>
        /// Handle server instance change event - resubscribe to new server events
        /// </summary>
        private void OnServerInstanceChanged(McpBridgeServer newServer)
        {
            UnityEngine.Debug.Log($"[uLoopMCP] Server instance changed to: {newServer?.GetHashCode() ?? 0}");
            
            // Resubscribe to events on the new server
            SubscribeToServerEvents();
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
            (bool isRunning, int port, bool _) = McpServerController.GetServerStatus();
            McpBridgeServer currentServer = McpServerController.CurrentServer;
            var connectedClients = currentServer?.GetConnectedClients();
            int connectedCount = connectedClients?.Count ?? 0;
            
            // Server instance change is now handled by OnServerInstanceChanged event

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