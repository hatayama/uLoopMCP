using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;


#if ULOOPMCP_DEBUG
using System;
#endif

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Editor Window for controlling Unity MCP Server - Presenter layer in MVP architecture
    /// Coordinates between Model, View, and helper classes for server management
    /// Related classes:
    /// - McpEditorModel: Model layer for state management and business logic
    /// - McpEditorWindowView: View layer for UI rendering
    /// - McpEditorWindowEventHandler: Event management helper (Unity/Server events)
    /// - McpServerOperations: Server operations helper (start/stop/validation)
    /// - McpEditorWindowState: State objects (UIState, RuntimeState, DebugState)
    /// - McpConfigServiceFactory: Configuration services factory for different IDEs
    /// - McpServerController: Core server lifecycle management
    /// - McpBridgeServer: The actual TCP server implementation
    /// - McpEditorSettings: Persistent settings storage
    /// </summary>
    public class McpEditorWindow : EditorWindow
    {
        // Singleton instance for external access
        private static McpEditorWindow _instance;
        
        // Configuration services factory
        private McpConfigServiceFactory _configServiceFactory;

        // View layer
        private McpEditorWindowView _view;
        
        // Connected LLM Tools management (persisted across domain reload)
        private List<ConnectedLLMToolData> _connectedTools = new();

        // Model layer (MVP pattern)
        private McpEditorModel _model;

        // Event handler (MVP pattern helper)
        private McpEditorWindowEventHandler _eventHandler;

        // Server operations handler (MVP pattern helper)
        private McpServerOperations _serverOperations;
        
        // Cache for stored tools to avoid repeated calls
        private IEnumerable<ConnectedClient> _cachedStoredTools;
        private float _lastStoredToolsUpdateTime;

        /// <summary>
        /// Get current instance for external access
        /// </summary>
        public static McpEditorWindow Instance => _instance;
        
        // Backup storage for server restart
        private List<ConnectedLLMToolData> _toolsBackup;

        [MenuItem("Window/uLoopMCP")]
        public static void ShowWindow()
        {
            McpEditorWindow window = GetWindow<McpEditorWindow>(McpConstants.PROJECT_NAME);
            window.Show();
        }

        private void OnEnable()
        {
            _instance = this;
            InitializeAll();
            SubscribeToServerEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromServerEvents();
            if (_instance == this)
            {
                _instance = null;
            }
        }

        
        private void InitializeAll()
        {
            InitializeModel();
            InitializeView();
            InitializeConfigurationServices();
            InitializeEventHandler();
            InitializeServerOperations();
            LoadSavedSettings();
            RestoreSessionState();
            
            HandlePostCompileMode();
        }

        /// <summary>
        /// Initialize model layer
        /// </summary>
        private void InitializeModel()
        {
            _model = new McpEditorModel();
        }

        /// <summary>
        /// Initialize view layer
        /// </summary>
        private void InitializeView()
        {
            _view = new McpEditorWindowView();
        }

        /// <summary>
        /// Add a connected LLM tool
        /// </summary>
        public void AddConnectedTool(ConnectedClient client)
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
                client.ConnectedAt
            );
            _connectedTools.Add(toolData);
        }

        /// <summary>
        /// Remove a connected LLM tool
        /// </summary>
        public void RemoveConnectedTool(string toolName)
        {
            _connectedTools.RemoveAll(tool => tool.Name == toolName);
        }

        /// <summary>
        /// Clear all connected LLM tools
        /// </summary>
        public void ClearConnectedTools()
        {
            _connectedTools.Clear();
        }

        /// <summary>
        /// Subscribe to server lifecycle events
        /// </summary>
        private void SubscribeToServerEvents()
        {
            McpBridgeServer.OnServerStopping += OnServerStopping;
            McpBridgeServer.OnServerStarted += OnServerStarted;
            McpBridgeServer.OnToolConnected += OnToolConnected;
            McpBridgeServer.OnToolDisconnected += OnToolDisconnected;
            McpBridgeServer.OnAllToolsCleared += OnAllToolsCleared;
        }
        
        /// <summary>
        /// Unsubscribe from server lifecycle events
        /// </summary>
        private void UnsubscribeFromServerEvents()
        {
            McpBridgeServer.OnServerStopping -= OnServerStopping;
            McpBridgeServer.OnServerStarted -= OnServerStarted;
            McpBridgeServer.OnToolConnected -= OnToolConnected;
            McpBridgeServer.OnToolDisconnected -= OnToolDisconnected;
            McpBridgeServer.OnAllToolsCleared -= OnAllToolsCleared;
        }
        
        /// <summary>
        /// Handle server stopping event - backup connected tools
        /// </summary>
        private void OnServerStopping()
        {
            _toolsBackup = _connectedTools
                .Where(tool => tool.Name != McpConstants.UNKNOWN_CLIENT_NAME)
                .ToList();
        }
        
        /// <summary>
        /// Handle server started event - restore connected tools
        /// </summary>
        private void OnServerStarted()
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
        private void OnToolConnected(ConnectedClient client)
        {
            AddConnectedTool(client);
        }
        
        /// <summary>
        /// Handle tool disconnected event - remove tool from connected list
        /// </summary>
        private void OnToolDisconnected(string toolName)
        {
            RemoveConnectedTool(toolName);
        }
        
        /// <summary>
        /// Handle all tools cleared event - clear all connected tools
        /// </summary>
        private void OnAllToolsCleared()
        {
            ClearConnectedTools();
        }
        
        /// <summary>
        /// Backup current connected tools for server restart (legacy method for compatibility)
        /// </summary>
        public List<ConnectedLLMToolData> BackupConnectedTools()
        {
            List<ConnectedLLMToolData> backup = _connectedTools
                .Where(tool => tool.Name != McpConstants.UNKNOWN_CLIENT_NAME)
                .ToList();
            return backup;
        }

        /// <summary>
        /// Restore connected tools from backup after server restart
        /// First restore all tools immediately, then cleanup disconnected ones after a delay
        /// </summary>
        public void RestoreConnectedTools(List<ConnectedLLMToolData> backup)
        {
            if (backup == null || backup.Count == 0)
            {
                return;
            }

            // Immediately restore all tools to prevent "No connected tools found" flash
            foreach (ConnectedLLMToolData toolData in backup)
            {
                ConnectedClient restoredClient = new(toolData.Endpoint, null, toolData.Name);
                AddConnectedTool(restoredClient);
            }

            // Schedule cleanup after a short delay to remove actually disconnected tools
            _ = DelayedCleanupAsync();
        }

        /// <summary>
        /// Clean up disconnected tools after a delay
        /// </summary>
        private async Task DelayedCleanupAsync()
        {
            // Wait 1 second for clients to reconnect
            await TimerDelay.Wait(2000);

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

            // Force UI update if any tools were removed
            if (toolsToRemove.Count > 0)
            {
                Repaint();
            }

        }


        /// <summary>
        /// Get connected tools as ConnectedClient objects for UI display, sorted by name
        /// </summary>
        public IEnumerable<ConnectedClient> GetConnectedToolsAsClients()
        {
            return _connectedTools.OrderBy(tool => tool.Name).Select(tool => ConvertToConnectedClient(tool));
        }

        /// <summary>
        /// Convert stored tool data to ConnectedClient for UI display
        /// </summary>
        private ConnectedClient ConvertToConnectedClient(ConnectedLLMToolData toolData)
        {
            return new ConnectedClient(toolData.Endpoint, null, toolData.Name);
        }

        /// <summary>
        /// Initialize configuration services factory
        /// </summary>
        private void InitializeConfigurationServices()
        {
            _configServiceFactory = new McpConfigServiceFactory();
        }

        /// <summary>
        /// Initialize event handler
        /// </summary>
        private void InitializeEventHandler()
        {
            _eventHandler = new McpEditorWindowEventHandler(_model, this);
            _eventHandler.Initialize();
        }

        /// <summary>
        /// Initialize server operations handler
        /// </summary>
        private void InitializeServerOperations()
        {
            _serverOperations = new McpServerOperations(_model, _eventHandler);
        }

        /// <summary>
        /// Load saved settings from preferences
        /// </summary>
        private void LoadSavedSettings()
        {
            _model.LoadFromSettings();
        }

        /// <summary>
        /// Restore session state from Unity SessionState
        /// </summary>
        private void RestoreSessionState()
        {
            _model.LoadFromSessionState();
        }

        /// <summary>
        /// Handle post-compile mode initialization and auto-start logic
        /// </summary>
        private void HandlePostCompileMode()
        {
            // Enable post-compile mode after domain reload
            _model.EnablePostCompileMode();

            // Clear reconnecting UI flag on domain reload to ensure proper state
            McpSessionManager.instance.ShowReconnectingUI = false;

            // Check if after compilation
            bool isAfterCompile = McpSessionManager.instance.IsAfterCompile;

            // Grace period is already started in OnEnable() if needed

            // Determine if server should be started automatically
            bool shouldStartAutomatically = isAfterCompile || _model.UI.AutoStartServer;
            bool serverNotRunning = !McpServerController.IsServerRunning;
            bool shouldStartServer = shouldStartAutomatically && serverNotRunning;

            if (shouldStartServer)
            {
                if (isAfterCompile)
                {
                    McpSessionManager.instance.ClearAfterCompileFlag();

                    // Use saved port number
                    int savedPort = McpSessionManager.instance.ServerPort;
                    bool portNeedsUpdate = savedPort != _model.UI.CustomPort;

                    if (portNeedsUpdate)
                    {
                        _model.UpdateCustomPort(savedPort);
                    }
                }

                _serverOperations.StartServerInternal();
            }
        }

        private void OnDisable()
        {
            CleanupEventHandler();
            SaveSessionState();
        }

        /// <summary>
        /// Cleanup event handler
        /// </summary>
        private void CleanupEventHandler()
        {
            _eventHandler?.Cleanup();
        }

        /// <summary>
        /// Save current state to Unity SessionState
        /// </summary>
        private void SaveSessionState()
        {
            _model.SaveToSessionState();
        }

        /// <summary>
        /// Called when the window gets focus - update UI to reflect current state
        /// </summary>
        private void OnFocus()
        {
            // Refresh UI when window gains focus to reflect any state changes
            Repaint();
        }

        private void OnGUI()
        {
            // Draw debug background if ULOOPMCP_DEBUG is defined
            _view.DrawDebugBackground(position);
            
            // Synchronize server port and UI settings
            SyncPortSettings();

            // Make entire window scrollable
            Vector2 newScrollPosition = EditorGUILayout.BeginScrollView(_model.UI.MainScrollPosition);
            if (newScrollPosition != _model.UI.MainScrollPosition)
            {
                UpdateMainScrollPosition(newScrollPosition);
            }

            // Use view layer for rendering
            ServerStatusData statusData = CreateServerStatusData();
            _view.DrawServerStatus(statusData);

            ServerControlsData controlsData = CreateServerControlsData();
            _view.DrawServerControls(
                data: controlsData,
                toggleServerCallback: ToggleServer,
                autoStartCallback: UpdateAutoStartServer,
                portChangeCallback: UpdateCustomPort);

            ConnectedToolsData toolsData = CreateConnectedToolsData();
            _view.DrawConnectedToolsSection(
                data: toolsData,
                toggleFoldoutCallback: UpdateShowConnectedTools);

            EditorConfigData configData = CreateEditorConfigData();
            _view.DrawEditorConfigSection(
                data: configData,
                editorChangeCallback: UpdateSelectedEditorType,
                configureCallback: (editor) => ConfigureEditor(),
                foldoutCallback: UpdateShowLLMToolSettings);

            SecuritySettingsData securityData = CreateSecuritySettingsData();
            _view.DrawSecuritySettings(
                data: securityData,
                foldoutCallback: UpdateShowSecuritySettings,
                enableTestsCallback: UpdateEnableTestsExecution,
                allowMenuCallback: UpdateAllowMenuItemExecution,
                allowThirdPartyCallback: UpdateAllowThirdPartyTools);


            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Synchronize server port and UI settings
        /// </summary>
        private void SyncPortSettings()
        {
            // Synchronize if server is running and UI port setting differs from actual server port
            bool serverIsRunning = McpServerController.IsServerRunning;

            if (serverIsRunning)
            {
                int actualServerPort = McpServerController.ServerPort;
                bool portMismatch = _model.UI.CustomPort != actualServerPort;

                if (portMismatch)
                {
                    _model.UpdateCustomPort(actualServerPort);
                }
            }
        }

        /// <summary>
        /// Create server status data for view rendering
        /// </summary>
        private ServerStatusData CreateServerStatusData()
        {
            (bool isRunning, int port, bool _) = McpServerController.GetServerStatus();
            string status = isRunning ? "Running" : "Stopped";
            Color statusColor = isRunning ? Color.green : Color.red;

            return new ServerStatusData(isRunning, port, status, statusColor);
        }

        /// <summary>
        /// Create server controls data for view rendering
        /// </summary>
        private ServerControlsData CreateServerControlsData()
        {
            bool isRunning = McpServerController.IsServerRunning;
            
            // Check for port mismatch warnings
            bool hasPortWarning = false;
            string portWarningMessage = null;
            
            if (!isRunning)
            {
                // Check if requested port is available
                int requestedPort = _model.UI.CustomPort;
                if (NetworkUtility.IsPortInUse(requestedPort))
                {
                    hasPortWarning = true;
                    portWarningMessage = $"Port {requestedPort} is already in use. Server will automatically find an available port when started.";
                }
            }
            
            return new ServerControlsData(_model.UI.CustomPort, _model.UI.AutoStartServer, isRunning, !isRunning, hasPortWarning, portWarningMessage);
        }

        /// <summary>
        /// Get stored tools with caching to avoid repeated calls
        /// </summary>
        private IEnumerable<ConnectedClient> GetCachedStoredTools()
        {
            const float cacheDuration = 0.1f; // 100ms cache
            float currentTime = Time.realtimeSinceStartup;
            
            if (_cachedStoredTools == null || (currentTime - _lastStoredToolsUpdateTime) > cacheDuration)
            {
                _cachedStoredTools = GetConnectedToolsAsClients();
                _lastStoredToolsUpdateTime = currentTime;
            }
            
            return _cachedStoredTools;
        }

        /// <summary>
        /// Invalidate cached stored tools (call when tools change)
        /// </summary>
        private void InvalidateStoredToolsCache()
        {
            _cachedStoredTools = null;
        }

        /// <summary>
        /// Create connected tools data for view rendering
        /// </summary>
        private ConnectedToolsData CreateConnectedToolsData()
        {
            bool isServerRunning = McpServerController.IsServerRunning;
            IReadOnlyCollection<ConnectedClient> connectedClients = McpServerController.CurrentServer?.GetConnectedClients();

            // Check reconnecting UI flags from McpSessionManager
            bool showReconnectingUIFlag = McpSessionManager.instance.ShowReconnectingUI;
            bool showPostCompileUIFlag = McpSessionManager.instance.ShowPostCompileReconnectingUI;

            // Only count clients with proper names (not Unknown Client) as "connected"
            bool hasNamedClients = connectedClients != null &&
                                   connectedClients.Any(client => client.ClientName != McpConstants.UNKNOWN_CLIENT_NAME);

            // Check if we have stored tools available (with caching)
            IEnumerable<ConnectedClient> storedTools = GetCachedStoredTools();
            bool hasStoredTools = storedTools.Any();
            

            // If we have stored tools, show them (prioritize stored tools over server clients)
            if (hasStoredTools)
            {
                connectedClients = storedTools.ToList();
                hasNamedClients = true;
            }

            // Show reconnecting UI only if no stored tools and no real clients
            bool showReconnectingUI = !hasStoredTools && 
                                      (showReconnectingUIFlag || showPostCompileUIFlag) && 
                                      !hasNamedClients;


            // Clear post-compile flag when named clients are connected
            if (hasNamedClients && showPostCompileUIFlag)
            {
                McpSessionManager.instance.ClearPostCompileReconnectingUI();
            }

            return new ConnectedToolsData(connectedClients, _model.UI.ShowConnectedTools, isServerRunning, showReconnectingUI);
        }

        /// <summary>
        /// Create editor config data for view rendering
        /// </summary>
        private EditorConfigData CreateEditorConfigData()
        {
            bool isServerRunning = McpServerController.IsServerRunning;
            int currentPort = McpServerController.ServerPort;

            // Check configuration status
            bool isConfigured = false;
            bool hasPortMismatch = false;
            string configurationError = null;

            try
            {
                McpConfigService configService = GetConfigService(_model.UI.SelectedEditorType);
                isConfigured = configService.IsConfigured();

                // Check for port mismatch if configured
                if (isConfigured)
                {
                    // Get configured port from the settings file
                    int configuredPort = configService.GetConfiguredPort();
                    
                    // Check mismatch between server port and configured port
                    if (isServerRunning)
                    {
                        hasPortMismatch = currentPort != configuredPort;
                    }
                    else
                    {
                        // When server is not running, check if UI port matches configured port
                        hasPortMismatch = _model.UI.CustomPort != configuredPort;
                    }
                }
            }
            catch (System.Exception ex)
            {
                configurationError = ex.Message;
            }

            return new EditorConfigData(_model.UI.SelectedEditorType, _model.UI.ShowLLMToolSettings, isServerRunning, currentPort, isConfigured, hasPortMismatch, configurationError);
        }

        /// <summary>
        /// Create security settings data for view rendering
        /// </summary>
        private SecuritySettingsData CreateSecuritySettingsData()
        {
            return new SecuritySettingsData(
                _model.UI.ShowSecuritySettings,
                McpEditorSettings.GetEnableTestsExecution(),
                McpEditorSettings.GetAllowMenuItemExecution(),
                McpEditorSettings.GetAllowThirdPartyTools());
        }

        /// <summary>
        /// Configure editor settings
        /// </summary>
        private void ConfigureEditor()
        {
            McpConfigService configService = GetConfigService(_model.UI.SelectedEditorType);
            bool isServerRunning = McpServerController.IsServerRunning;
            int portToUse = isServerRunning ? McpServerController.ServerPort : _model.UI.CustomPort;

            configService.AutoConfigure(portToUse);
            Repaint();
        }

        /// <summary>
        /// Start server (for user operations)
        /// </summary>
        private void StartServer()
        {
            if (_serverOperations.StartServer())
            {
                Repaint();
            }
        }

        /// <summary>
        /// Stop server
        /// </summary>
        private void StopServer()
        {
            _serverOperations.StopServer();
            Repaint();
        }

        /// <summary>
        /// Get corresponding configuration service from editor type
        /// </summary>
        private McpConfigService GetConfigService(McpEditorType editorType)
        {
            return _configServiceFactory.GetConfigService(editorType);
        }

        // UIState update helper methods for callback unification

        /// <summary>
        /// Update AutoStartServer setting with persistence
        /// </summary>
        private void UpdateAutoStartServer(bool autoStart)
        {
            _model.UpdateAutoStartServer(autoStart);
        }

        /// <summary>
        /// Update CustomPort setting with persistence
        /// </summary>
        private void UpdateCustomPort(int port)
        {
            _model.UpdateCustomPort(port);
        }

        /// <summary>
        /// Update ShowConnectedTools setting
        /// </summary>
        private void UpdateShowConnectedTools(bool show)
        {
            _model.UpdateShowConnectedTools(show);
        }

        /// <summary>
        /// Update ShowLLMToolSettings setting
        /// </summary>
        private void UpdateShowLLMToolSettings(bool show)
        {
            _model.UpdateShowLLMToolSettings(show);
        }

        /// <summary>
        /// Update SelectedEditorType setting with persistence
        /// </summary>
        private void UpdateSelectedEditorType(McpEditorType type)
        {
            _model.UpdateSelectedEditorType(type);
        }

        /// <summary>
        /// Update MainScrollPosition setting
        /// </summary>
        private void UpdateMainScrollPosition(Vector2 position)
        {
            _model.UpdateMainScrollPosition(position);
        }

        /// <summary>
        /// Update ShowSecuritySettings setting
        /// </summary>
        private void UpdateShowSecuritySettings(bool show)
        {
            _model.UpdateShowSecuritySettings(show);
        }

        /// <summary>
        /// Update EnableTestsExecution setting with persistence
        /// </summary>
        private void UpdateEnableTestsExecution(bool enable)
        {
            _model.UpdateEnableTestsExecution(enable);
        }

        /// <summary>
        /// Update AllowMenuItemExecution setting with persistence
        /// </summary>
        private void UpdateAllowMenuItemExecution(bool allow)
        {
            _model.UpdateAllowMenuItemExecution(allow);
        }

        /// <summary>
        /// Update AllowThirdPartyTools setting with persistence
        /// </summary>
        private void UpdateAllowThirdPartyTools(bool allow)
        {
            _model.UpdateAllowThirdPartyTools(allow);
        }


        /// <summary>
        /// Toggle server state (start if stopped, stop if running)
        /// </summary>
        private void ToggleServer()
        {
            if (McpServerController.IsServerRunning)
            {
                StopServer();
            }
            else
            {
                StartServer();
            }
        }

    }
}