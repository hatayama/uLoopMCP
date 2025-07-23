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
        // DEPRECATED: UI now reads directly from SessionData.yaml
        // private List<ConnectedLLMToolData> _connectedTools = new();

        // Model layer (MVP pattern)
        private McpEditorModel _model;

        // Event handler (MVP pattern helper)
        private McpEditorWindowEventHandler _eventHandler;

        // Server operations handler (MVP pattern helper)
        private McpServerOperations _serverOperations;
        
        // Cache for stored tools to avoid repeated calls
        // TEMPORARILY COMMENTED OUT: Caching mechanism that might interfere with SessionRecovery
        // private IEnumerable<ConnectedClient> _cachedStoredTools;
        // private float _lastStoredToolsUpdateTime;

        /// <summary>
        /// Get current instance for external access
        /// </summary>
        public static McpEditorWindow Instance => _instance;
        
        // Backup storage for server restart
        // DEPRECATED: UI now reads directly from SessionData.yaml
        // private List<ConnectedLLMToolData> _toolsBackup;

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
        /// DEPRECATED: UI now reads directly from SessionData.yaml
        /// </summary>
        public void AddConnectedTool(ConnectedClient client)
        {
            // No longer used - UI reads directly from SessionData.yaml
            return;
            // Implementation removed - method body commented out
        }

        /// <summary>
        /// Remove a connected LLM tool
        /// DEPRECATED: UI now reads directly from SessionData.yaml
        /// </summary>
        public void RemoveConnectedTool(string toolName)
        {
            // No longer used - UI reads directly from SessionData.yaml
            // Removal is handled by ClientDisconnectionUseCase updating SessionData.yaml
        }

        /// <summary>
        /// Clear all connected LLM tools
        /// DEPRECATED: UI now reads directly from SessionData.yaml
        /// </summary>
        public void ClearConnectedTools()
        {
            // No longer used - UI reads directly from SessionData.yaml
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
        }
        
        /// <summary>
        /// Handle server stopping event - backup and clear connected tools
        /// </summary>
        private void OnServerStopping()
        {
            // No longer needed - data is persisted in SessionData.yaml
            // Just request a repaint to update the UI
            Repaint();
        }
        
        /// <summary>
        /// Handle server started event - restore connected tools
        /// </summary>
        private void OnServerStarted()
        {
            // No longer needed - UI reads directly from SessionData.yaml
            // Just request a repaint to update the UI
            Repaint();
        }
        
        /// <summary>
        /// Handle tool connected event - add tool to connected list
        /// </summary>
        private void OnToolConnected(ConnectedClient client)
        {
            // No longer needed - UI reads directly from SessionData.yaml
            // Just request a repaint to update the UI
            Repaint();
        }
        
        /// <summary>
        /// Handle tool disconnected event - remove tool from connected list
        /// </summary>
        private void OnToolDisconnected(string toolName)
        {
            // No longer needed - UI reads directly from SessionData.yaml
            // Just request a repaint to update the UI
            Repaint();
        }
        
        
        /// <summary>
        /// Backup current connected tools for server restart (legacy method for compatibility)
        /// DEPRECATED: UI now reads directly from SessionData.yaml
        /// </summary>
        public List<ConnectedLLMToolData> BackupConnectedTools()
        {
            // No longer needed - data is persisted in SessionData.yaml
            return new List<ConnectedLLMToolData>();
        }

        /// <summary>
        /// Restore connected tools from backup after server restart
        /// DEPRECATED: UI now reads directly from SessionData.yaml
        /// </summary>
        public void RestoreConnectedTools(List<ConnectedLLMToolData> backup)
        {
            // No longer needed - UI reads directly from SessionData.yaml
            Repaint();
        }

        /// <summary>
        /// Clean up disconnected tools after a delay
        /// DEPRECATED: UI now reads directly from SessionData.yaml
        /// </summary>
        private async Task DelayedCleanupAsync()
        {
            // No longer needed - UI reads directly from SessionData.yaml
            await Task.CompletedTask;
            return;
            // Implementation removed - method body commented out

        }

        /// <summary>
        /// Check if an endpoint is a mock endpoint created by SessionRecovery
        /// Mock endpoints contain "unknown_port_" pattern
        /// </summary>
        private bool IsMockEndpoint(string endpoint)
        {
            return !string.IsNullOrEmpty(endpoint) && endpoint.Contains("unknown_port_");
        }

        /// <summary>
        /// Get connected tools as ConnectedClient objects for UI display, sorted by name
        /// Always reads from SessionData.yaml to ensure UI is synchronized with actual state
        /// </summary>
        public IEnumerable<ConnectedClient> GetConnectedToolsAsClients()
        {
            // Read directly from SessionData.yaml instead of using _connectedTools
            McpSessionManager sessionManager = McpSessionManager.instance;
            if (sessionManager == null)
            {
                return Enumerable.Empty<ConnectedClient>();
            }
            
            List<McpSessionManager.ClientEndpointPair> endpoints = sessionManager.GetPushServerEndpoints();
            if (endpoints == null || endpoints.Count == 0)
            {
                return Enumerable.Empty<ConnectedClient>();
            }
            
            // Convert endpoints to ConnectedClient objects and sort by name
            return endpoints
                .Select(endpoint => new ConnectedClient(endpoint.clientEndpoint, null, endpoint.clientName))
                .OrderBy(client => client.ClientName);
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
        /// Restore session state from Unity SessionState and recover UI tools from yaml
        /// </summary>
        private void RestoreSessionState()
        {
            _model.LoadFromSessionState();
            
            // Recover UI tools from stored session data using SessionRecoveryUseCase
            RecoverUIToolsFromSession();
        }

        /// <summary>
        /// Recover UI tools from stored session data using UseCase pattern
        /// </summary>
        private void RecoverUIToolsFromSession()
        {
            // No longer needed - UI reads directly from SessionData.yaml
            // SessionRecoveryUseCase is still executed for other recovery tasks
            
            // Create UseCase instance (single-use pattern)
            SessionRecoveryUseCase useCase = new(recoveredTools =>
            {
                // No need to update _connectedTools - UI reads from SessionData.yaml
                // Just request repaint to show recovered tools immediately
                Repaint();
            });
            
            // Execute recovery process with temporal cohesion
            SessionRecoveryResult result = useCase.Execute();
            
            // Log result for debugging (result details already logged by UseCase)
            if (result.IsSuccess && result.RecoveredToolsCount > 0)
            {
                Debug.Log($"[uLoopMCP] Session recovery completed: {result.RecoveredToolsCount} tools recovered");
            }
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
                // Check if requested port is valid and available
                int requestedPort = _model.UI.CustomPort;
                
                // First check if port is valid
                if (!McpPortValidator.ValidatePort(requestedPort))
                {
                    hasPortWarning = true;
                    portWarningMessage = $"Port {requestedPort} is invalid. Port must be 1024 or higher and not a reserved system port.";
                }
                // Then check if valid port is available
                else if (NetworkUtility.IsPortInUse(requestedPort))
                {
                    hasPortWarning = true;
                    portWarningMessage = $"Port {requestedPort} is already in use. Server will automatically find an available port when started.";
                }
            }
            
            return new ServerControlsData(_model.UI.CustomPort, _model.UI.AutoStartServer, isRunning, !isRunning, hasPortWarning, portWarningMessage);
        }

        /// <summary>
        /// Get stored tools with caching to avoid repeated calls
        /// TEMPORARILY MODIFIED: Direct call instead of caching during debugging
        /// </summary>
        private IEnumerable<ConnectedClient> GetCachedStoredTools()
        {
            // Direct call instead of caching during debugging
            return GetConnectedToolsAsClients();
            
            // COMMENTED OUT: Original caching logic that might interfere with SessionRecovery
            /*
            const float cacheDuration = 0.1f; // 100ms cache
            float currentTime = Time.realtimeSinceStartup;
            
            if (_cachedStoredTools == null || (currentTime - _lastStoredToolsUpdateTime) > cacheDuration)
            {
                _cachedStoredTools = GetConnectedToolsAsClients();
                _lastStoredToolsUpdateTime = currentTime;
            }
            
            return _cachedStoredTools;
            */
        }

        /// <summary>
        /// Invalidate cached stored tools (call when tools change)
        /// TEMPORARILY MODIFIED: No-op during debugging
        /// </summary>
        private void InvalidateStoredToolsCache()
        {
            // No-op during debugging, direct calls don't need cache invalidation
            // _cachedStoredTools = null;
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
            bool isUpdateNeeded = true;
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

                // Check if update is needed
                int portToCheck = isServerRunning ? currentPort : _model.UI.CustomPort;
                isUpdateNeeded = configService.IsUpdateNeeded(portToCheck);
            }
            catch (System.Exception ex)
            {
                configurationError = ex.Message;
                isUpdateNeeded = true; // If error occurs, assume update is needed
            }

            return new EditorConfigData(_model.UI.SelectedEditorType, _model.UI.ShowLLMToolSettings, isServerRunning, currentPort, isConfigured, hasPortMismatch, configurationError, isUpdateNeeded);
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