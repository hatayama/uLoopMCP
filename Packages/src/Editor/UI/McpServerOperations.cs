using System;
using UnityEditor;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Server operations handler for McpEditorWindow - Manages server lifecycle operations
    /// Helper class for Presenter layer in MVP architecture
    /// Related classes:
    /// - McpEditorWindow: Main presenter that owns this handler
    /// - McpEditorModel: Model layer for state management
    /// - McpServerController: Core server lifecycle management
    /// - McpBridgeServer: The actual TCP server implementation
    /// </summary>
    internal class McpServerOperations
    {
        private readonly McpEditorModel _model;
        private readonly McpEditorWindowEventHandler _eventHandler;

        public McpServerOperations(McpEditorModel model, McpEditorWindowEventHandler eventHandler)
        {
            _model = model;
            _eventHandler = eventHandler;
        }

        /// <summary>
        /// Start server with user interaction (shows dialogs on error)
        /// </summary>
        /// <returns>True if successful</returns>
        public bool StartServer()
        {
            return ValidatePortAndStartServer(showErrorDialogs: true);
        }

        /// <summary>
        /// Start server for internal processing (no error dialogs)
        /// </summary>
        /// <returns>True if successful</returns>
        public bool StartServerInternal()
        {
            return ValidatePortAndStartServer(showErrorDialogs: false);
        }

        /// <summary>
        /// Stop the running server
        /// </summary>
        public void StopServer()
        {
            McpServerController.StopServer();
        }

        /// <summary>
        /// Validate port and start server
        /// </summary>
        /// <param name="showErrorDialogs">Whether to show error dialogs to user</param>
        /// <returns>True if successful</returns>
        private bool ValidatePortAndStartServer(bool showErrorDialogs)
        {
            int currentPort = _model.UI.CustomPort;
            
            // Validate port range
            bool portInValidRange = currentPort >= McpServerConfig.MIN_PORT_NUMBER && 
                                   currentPort <= McpServerConfig.MAX_PORT_NUMBER;
            
            if (!portInValidRange)
            {
                if (showErrorDialogs)
                {
                    EditorUtility.DisplayDialog("Port Error", 
                        $"Port must be between {McpServerConfig.MIN_PORT_NUMBER} and {McpServerConfig.MAX_PORT_NUMBER}", 
                        "OK");
                }
                return false;
            }

            // Check if our own server is already running on the same port
            bool ownServerRunningOnSamePort = McpServerController.IsServerRunning && 
                                             McpServerController.ServerPort == currentPort;
            
            if (ownServerRunningOnSamePort)
            {
                // MCP Server is already running
                return true; // Already running, treat as success
            }

            // Note: Port conflict check is now handled by McpServerController.StartServer
            // which automatically finds an available port and logs appropriate warnings

            try
            {
                // Auto-update configuration files before starting server
                // This ensures paths are updated after package updates
                McpConfigAutoUpdater.UpdateAllConfiguredEditors(currentPort);

                McpServerController.StartServer(currentPort);

                // Refresh server event subscriptions after successful start
                _eventHandler.RefreshServerEventSubscriptions();

                return true;
            }
            catch (InvalidOperationException ex)
            {
                // In case of port in use error
                if (showErrorDialogs)
                {
                    EditorUtility.DisplayDialog("Server Start Error", ex.Message, "OK");
                }
                return false;
            }
            catch (Exception ex)
            {
                // Other errors
                if (showErrorDialogs)
                {
                    EditorUtility.DisplayDialog("Server Start Error",
                        $"Failed to start server: {ex.Message}",
                        "OK");
                }
                return false;
            }
        }
    }
} 