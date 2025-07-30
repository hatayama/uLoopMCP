using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System;

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
        // Singleton instance
        private static McpEditorWindow _instance;

        // Configuration services factory
        private McpConfigServiceFactory _configServiceFactory;

        // View layer
        private McpEditorWindowView _view;

        // Model layer (MVP pattern)
        private McpEditorModel _model;

        // Event handler (MVP pattern helper)
        private McpEditorWindowEventHandler _eventHandler;

        // Server operations handler (MVP pattern helper)
        private McpServerOperations _serverOperations;

        // Display-specific data for Connected Tools (separate from actual connected clients)
        private List<ConnectedClient> _displayToolsData = new();
        private readonly object _displayDataLock = new object();

        // Server running state (managed by external usecase)
        private bool _serverRunningState = false;

        /// <summary>
        /// Get the singleton instance of McpEditorWindow
        /// </summary>
        public static McpEditorWindow Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = GetWindow<McpEditorWindow>(McpConstants.PROJECT_NAME, false);
                }
                return _instance;
            }
        }

        [MenuItem("Window/uLoopMCP")]
        public static void ShowWindow()
        {
            McpEditorWindow window = Instance;
            window.Show();
        }

        private void OnEnable()
        {
            // Set singleton instance
            _instance = this;
            
            InitializeAll();
            
            // NOTE: Data restoration is now handled by external usecase
        }

        private void OnDestroy()
        {
            // NOTE: Data saving is now handled by external usecase
            
            // Clear singleton instance
            if (_instance == this)
            {
                _instance = null;
            }
            
            // Clear display data
            lock (_displayDataLock)
            {
                _displayToolsData.Clear();
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
            
            // Initialize server running state
            _serverRunningState = McpServerController.IsServerRunning;

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






        // NOTE: GetConnectedToolsAsClients and OnConnectedToolsChanged removed with ConnectedToolsMonitoringService
        // UI updates now handled via public APIs


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
            
            // Load display data from settings
            LoadDisplayDataFromSettings();
        }
        
        /// <summary>
        /// Load display data from McpEditorSettings
        /// </summary>
        private void LoadDisplayDataFromSettings()
        {
            lock (_displayDataLock)
            {
                _displayToolsData.Clear();
                
                // Load from settings
                ConnectedLLMToolData[] tools = McpEditorSettings.GetConnectedLLMTools();
                
                VibeLogger.LogInfo(
                    "load_display_data_from_settings",
                    $"Loading {tools?.Length ?? 0} tools from settings",
                    new { toolCount = tools?.Length ?? 0 }
                );
                
                foreach (ConnectedLLMToolData tool in tools)
                {
                    _displayToolsData.Add(new ConnectedClient(
                        tool.Endpoint,
                        null, // NetworkStream is not needed for display
                        tool.Name,
                        tool.NotificationPort
                    ));
                }
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
            McpEditorSettings.SetShowReconnectingUI(false);

            // Check if after compilation
            bool isAfterCompile = McpEditorSettings.GetIsAfterCompile();

            // Grace period is already started in OnEnable() if needed

            // Determine if server should be started automatically
            bool shouldStartAutomatically = isAfterCompile || _model.UI.AutoStartServer;
            bool serverNotRunning = !McpServerController.IsServerRunning;
            bool shouldStartServer = shouldStartAutomatically && serverNotRunning;

            if (shouldStartServer)
            {
                if (isAfterCompile)
                {
                    McpEditorSettings.ClearAfterCompileFlag();

                    // Use saved port number
                    int savedPort = McpEditorSettings.GetServerPort();
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
            
            // NOTE: ConnectedToolsMonitoringService removed
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

            // Create simple data structure for Connected Tools display
            IReadOnlyCollection<ConnectedClient> displayClients;
            lock (_displayDataLock)
            {
                displayClients = _displayToolsData.ToList();
            }
            
            // Debug logging
            if (displayClients.Count == 0)
            {
                VibeLogger.LogWarning(
                    "no_display_clients",
                    "No clients in display data",
                    new { settingsCount = McpEditorSettings.GetConnectedLLMTools()?.Length ?? 0 }
                );
            }

            ConnectedToolsData toolsData = new(
                displayClients,
                _model.UI.ShowConnectedTools,
                _serverRunningState,  // Use locally managed state
                false  // showReconnectingUI is always false (controlled externally)
            );

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

        // NOTE: GetCachedStoredTools and InvalidateStoredToolsCache removed with ConnectedToolsMonitoringService

        // NOTE: CreateConnectedToolsData removed - display data now managed via public APIs

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
            catch (Exception ex)
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

        #region Public Display APIs

        /// <summary>
        /// Add or update a tool in the display list by notification port
        /// Thread-safe operation with automatic UI refresh
        /// </summary>
        public void AddDisplayTool(string clientName, string endpoint, int notificationPort)
        {
            if (notificationPort <= 0) return; // Invalid port
            
            lock (_displayDataLock)
            {
                // Remove existing entry with same notification port
                _displayToolsData.RemoveAll(c => c.NotificationPort == notificationPort);
                
                // Add new entry
                ConnectedClient displayClient = new(endpoint, null, clientName, notificationPort);
                _displayToolsData.Add(displayClient);
                
                // Sort by name
                _displayToolsData.Sort((a, b) => string.Compare(a.ClientName, b.ClientName, StringComparison.Ordinal));
            }
            
            // Trigger UI refresh on main thread
            EditorApplication.delayCall += () => Repaint();
            
            VibeLogger.LogInfo(
                "display_tool_added",
                "Tool added to display list via public API",
                new { clientName, endpoint, notificationPort }
            );
        }

        /// <summary>
        /// Remove a tool from the display list by notification port
        /// Thread-safe operation with automatic UI refresh
        /// </summary>
        public void RemoveDisplayTool(int notificationPort)
        {
            if (notificationPort <= 0) return; // Invalid port
            
            lock (_displayDataLock)
            {
                _displayToolsData.RemoveAll(c => c.NotificationPort == notificationPort);
            }
            
            // Trigger UI refresh on main thread
            EditorApplication.delayCall += () => Repaint();
            
            VibeLogger.LogInfo(
                "display_tool_removed",
                "Tool removed from display list via public API",
                new { notificationPort }
            );
        }

        /// <summary>
        /// Clear all tools from the display list
        /// Thread-safe operation with automatic UI refresh
        /// </summary>
        public void ClearDisplayTools()
        {
            lock (_displayDataLock)
            {
                _displayToolsData.Clear();
            }
            
            // Trigger UI refresh on main thread
            EditorApplication.delayCall += () => Repaint();
            
            VibeLogger.LogInfo(
                "display_tools_cleared",
                "All tools cleared from display list via public API",
                new { }
            );
        }

        /// <summary>
        /// Get current display tools (read-only)
        /// Thread-safe operation
        /// </summary>
        public IReadOnlyList<ConnectedClient> GetDisplayTools()
        {
            lock (_displayDataLock)
            {
                return _displayToolsData.ToList();
            }
        }

        /// <summary>
        /// Update server running state from external usecase
        /// </summary>
        public void UpdateServerRunningState(bool isRunning)
        {
            _serverRunningState = isRunning;
            
            // Trigger UI refresh
            EditorApplication.delayCall += () => Repaint();
        }
        
        /// <summary>
        /// Reload display data from settings
        /// </summary>
        public void ReloadDisplayData()
        {
            LoadDisplayDataFromSettings();
            
            // Trigger UI refresh
            EditorApplication.delayCall += () => Repaint();
        }

        #endregion
    }
}