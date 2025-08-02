using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System;
using UnityEngine.UIElements;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Editor Window for controlling Unity MCP Server - Presenter layer in MVP architecture
    /// Coordinates between Model, View, and helper classes for server management
    /// Related classes:
    /// - McpEditorModel: Model layer for state management and business logic
    /// - McpEditorWindowUITView: UI Toolkit view layer for UI rendering
    /// - ServerControlsView, ConnectedToolsView, EditorConfigView, SecuritySettingsView: UI Toolkit view components
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
        // UI Toolkit View
        private McpEditorWindowUITView _uitView;

        // Background update scheduler
        private IVisualElementScheduledItem _updateScheduler;

        // Configuration services factory
        private McpConfigServiceFactory _configServiceFactory;


        // State fields (simplified from previous Model layer)
        private int _customPort;
        private bool _autoStartServer;
        private bool _showLLMToolSettings;
        private bool _showConnectedTools;
        private McpEditorType _selectedEditorType;
        private Vector2 _mainScrollPosition;
        private bool _showSecuritySettings;
        
        // Runtime state
        private bool _isPostCompileMode;
        private bool _needsRepaint;

        // Public properties for state access
        public int CustomPort => _customPort;

        /// <summary>
        /// Request UI repaint
        /// </summary>
        public void RequestRepaint()
        {
            _needsRepaint = true;
        }

        /// <summary>
        /// Check if repaint is needed
        /// </summary>
        public bool NeedsRepaint()
        {
            if (_isPostCompileMode || _needsRepaint)
            {
                _needsRepaint = false;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Disable post-compile mode
        /// </summary>
        public void DisablePostCompileMode()
        {
            if (_isPostCompileMode)
            {
                _isPostCompileMode = false;
            }
        }

        // Event handler (MVP pattern helper)
        private McpEditorWindowEventHandler _eventHandler;

        // Server operations handler (MVP pattern helper)
        private McpServerOperations _serverOperations;

        // Cache for stored tools to avoid repeated calls
        private IEnumerable<ConnectedClient> _cachedStoredTools;
        private float _lastStoredToolsUpdateTime;

        [MenuItem("Window/uLoopMCP")]
        public static void ShowWindow()
        {
            McpEditorWindow window = GetWindow<McpEditorWindow>(McpConstants.PROJECT_NAME);
            window.Show();
        }

        private void OnEnable()
        {
            InitializeAll();
            StartBackgroundUpdates();
        }

        private void OnDestroy()
        {
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
            // Initialize state with defaults
            _customPort = McpServerConfig.DEFAULT_PORT;
            _autoStartServer = false;
            _showLLMToolSettings = true;
            _showConnectedTools = true;
            _selectedEditorType = McpEditorType.Cursor;
            _mainScrollPosition = default;
            _showSecuritySettings = false;
            _isPostCompileMode = false;
            _needsRepaint = false;
        }

        /// <summary>
        /// Initialize view layer
        /// </summary>
        private void InitializeView()
        {
            _uitView = new McpEditorWindowUITView();
            _uitView.Initialize();

            if (_uitView.Root != null)
            {
                rootVisualElement.Add(_uitView.Root);
            }
        }


        /// <summary>
        /// Get connected tools as ConnectedClient objects for UI display, sorted by name
        /// </summary>
        public IEnumerable<ConnectedClient> GetConnectedToolsAsClients()
        {
            return ConnectedToolsMonitoringService.GetConnectedToolsAsClients();
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
            _eventHandler = new McpEditorWindowEventHandler(this);
            _eventHandler.Initialize();
        }

        /// <summary>
        /// Initialize server operations handler
        /// </summary>
        private void InitializeServerOperations()
        {
            _serverOperations = new McpServerOperations(this, _eventHandler);
        }

        /// <summary>
        /// Load saved settings from preferences
        /// </summary>
        private void LoadSavedSettings()
        {
            McpEditorSettingsData settings = McpEditorSettings.GetSettings();
            _customPort = settings.customPort;
            _autoStartServer = settings.autoStartServer;
            _showSecuritySettings = settings.showSecuritySettings;
        }

        /// <summary>
        /// Restore session state from Unity SessionState
        /// </summary>
        private void RestoreSessionState()
        {
            _selectedEditorType = McpEditorSettings.GetSelectedEditorType();
        }


        /// <summary>
        /// Handle post-compile mode initialization and auto-start logic
        /// </summary>
        private void HandlePostCompileMode()
        {
            // Enable post-compile mode after domain reload
            _isPostCompileMode = true;
            _needsRepaint = true;

            // Clear reconnecting UI flag on domain reload to ensure proper state
            McpEditorSettings.SetShowReconnectingUI(false);

            // Check if after compilation
            bool isAfterCompile = McpEditorSettings.GetIsAfterCompile();

            // Grace period is already started in OnEnable() if needed

            // Determine if server should be started automatically
            bool shouldStartAutomatically = isAfterCompile || _autoStartServer;
            bool serverNotRunning = !McpServerController.IsServerRunning;
            bool shouldStartServer = shouldStartAutomatically && serverNotRunning;

            if (shouldStartServer)
            {
                if (isAfterCompile)
                {
                    McpEditorSettings.ClearAfterCompileFlag();

                    // Use saved port number
                    int savedPort = McpEditorSettings.GetServerPort();
                    bool portNeedsUpdate = savedPort != _customPort;

                    if (portNeedsUpdate)
                    {
                        UpdateCustomPort(savedPort);
                    }
                }

                _serverOperations.StartServerInternal();
            }
        }

        private void OnDisable()
        {
            StopBackgroundUpdates();
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
            McpEditorSettings.SetSelectedEditorType(_selectedEditorType);
        }

        /// <summary>
        /// Called when the window gets focus - update UI to reflect current state
        /// </summary>
        private void OnFocus()
        {
            // Refresh UI when window gains focus to reflect any state changes
            Repaint();
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
                bool portMismatch = _customPort != actualServerPort;

                if (portMismatch)
                {
                    UpdateCustomPort(actualServerPort);
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
                int requestedPort = _customPort;

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

            return new ServerControlsData(_customPort, _autoStartServer, isRunning, !isRunning, hasPortWarning, portWarningMessage);
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
            bool showReconnectingUIFlag = McpEditorSettings.GetShowReconnectingUI();
            bool showPostCompileUIFlag = McpEditorSettings.GetShowPostCompileReconnectingUI();

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
                McpEditorSettings.ClearPostCompileReconnectingUI();
            }

            return new ConnectedToolsData(connectedClients, _showConnectedTools, isServerRunning, showReconnectingUI);
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
                McpConfigService configService = GetConfigService(_selectedEditorType);
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
                        hasPortMismatch = _customPort != configuredPort;
                    }
                }

                // Check if update is needed
                int portToCheck = isServerRunning ? currentPort : _customPort;
                isUpdateNeeded = configService.IsUpdateNeeded(portToCheck);
            }
            catch (Exception ex)
            {
                configurationError = ex.Message;
                isUpdateNeeded = true; // If error occurs, assume update is needed
            }

            return new EditorConfigData(_selectedEditorType, _showLLMToolSettings, isServerRunning, currentPort, isConfigured, hasPortMismatch, configurationError, isUpdateNeeded);
        }

        /// <summary>
        /// Create security settings data for view rendering
        /// </summary>
        private SecuritySettingsData CreateSecuritySettingsData()
        {
            return new SecuritySettingsData(
                _showSecuritySettings,
                McpEditorSettings.GetEnableTestsExecution(),
                McpEditorSettings.GetAllowMenuItemExecution(),
                McpEditorSettings.GetAllowThirdPartyTools());
        }

        /// <summary>
        /// Configure editor settings
        /// </summary>
        private void ConfigureEditor()
        {
            McpConfigService configService = GetConfigService(_selectedEditorType);
            bool isServerRunning = McpServerController.IsServerRunning;
            int portToUse = isServerRunning ? McpServerController.ServerPort : _customPort;

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
            _autoStartServer = autoStart;
            McpEditorSettings.SetAutoStartServer(autoStart);
        }

        /// <summary>
        /// Update CustomPort setting with persistence
        /// </summary>
        private void UpdateCustomPort(int port)
        {
            _customPort = port;
            McpEditorSettings.SetCustomPort(port);
            
            // Automatically update all configured MCP editor settings with new port
            McpPortChangeUpdater.UpdateAllConfigurationsForPortChange(port, "UI port change");
        }

        /// <summary>
        /// Update ShowConnectedTools setting
        /// </summary>
        private void UpdateShowConnectedTools(bool show)
        {
            _showConnectedTools = show;
        }

        /// <summary>
        /// Update ShowLLMToolSettings setting
        /// </summary>
        private void UpdateShowLLMToolSettings(bool show)
        {
            _showLLMToolSettings = show;
        }

        /// <summary>
        /// Update SelectedEditorType setting with persistence
        /// </summary>
        private void UpdateSelectedEditorType(McpEditorType type)
        {
            _selectedEditorType = type;
            McpEditorSettings.SetSelectedEditorType(type);
        }

        /// <summary>
        /// Update MainScrollPosition setting
        /// </summary>
        private void UpdateMainScrollPosition(Vector2 position)
        {
            _mainScrollPosition = position;
        }

        /// <summary>
        /// Update ShowSecuritySettings setting
        /// </summary>
        private void UpdateShowSecuritySettings(bool show)
        {
            _showSecuritySettings = show;
            McpEditorSettings.SetShowSecuritySettings(show);
        }

        /// <summary>
        /// Update EnableTestsExecution setting with persistence
        /// </summary>
        private void UpdateEnableTestsExecution(bool enable)
        {
            McpEditorSettings.SetEnableTestsExecution(enable);
        }

        /// <summary>
        /// Update AllowMenuItemExecution setting with persistence
        /// </summary>
        private void UpdateAllowMenuItemExecution(bool allow)
        {
            McpEditorSettings.SetAllowMenuItemExecution(allow);
        }

        /// <summary>
        /// Update AllowThirdPartyTools setting with persistence
        /// </summary>
        private void UpdateAllowThirdPartyTools(bool allow)
        {
            McpEditorSettings.SetAllowThirdPartyTools(allow);
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

        #region UI Toolkit Support

        /// <summary>
        /// Start background updates for UI Toolkit
        /// </summary>
        private void StartBackgroundUpdates()
        {
            // Schedule regular updates
            _updateScheduler = rootVisualElement.schedule.Execute(UpdateUIToolkit);
            OnEditorFocusChanged(true);

            // Subscribe to focus change events
            EditorApplication.focusChanged += OnEditorFocusChanged;

            // Do an immediate update
            UpdateUIToolkit();
        }

        /// <summary>
        /// Stop background updates for UI Toolkit
        /// </summary>
        private void StopBackgroundUpdates()
        {
            // Pause scheduled updates
            _updateScheduler?.Pause();

            // Unsubscribe from events
            EditorApplication.focusChanged -= OnEditorFocusChanged;
        }

        /// <summary>
        /// Handle editor focus changes
        /// </summary>
        private void OnEditorFocusChanged(bool hasFocus)
        {
            if (_updateScheduler == null) return;

            // Adjust update frequency based on focus state
            if (hasFocus)
            {
                _updateScheduler.Every(McpUIToolkitCommonConstants.UPDATE_INTERVAL_FOCUSED);
            }
            else
            {
                _updateScheduler.Every(McpUIToolkitCommonConstants.UPDATE_INTERVAL_DEFAULT);
            }
        }

        /// <summary>
        /// Update UI Toolkit view
        /// </summary>
        private void UpdateUIToolkit()
        {
            if (_uitView == null) return;

            // Synchronize server port and UI settings
            SyncPortSettings();

            // Update scroll position
            Vector2 currentScrollPosition = _uitView.GetScrollPosition();
            if (currentScrollPosition != _mainScrollPosition)
            {
                UpdateMainScrollPosition(currentScrollPosition);
            }

            // Update server controls (now includes status)
            ServerControlsData controlsData = CreateServerControlsData();
            _uitView.UpdateServerControls(controlsData, ToggleServer,
                UpdateAutoStartServer, UpdateCustomPort);

            // Update connected tools
            ConnectedToolsData toolsData = CreateConnectedToolsData();
            _uitView.UpdateConnectedTools(toolsData, UpdateShowConnectedTools);

            // Update editor config
            EditorConfigData configData = CreateEditorConfigData();
            _uitView.UpdateEditorConfig(configData, UpdateSelectedEditorType,
                (editor) => ConfigureEditor(), UpdateShowLLMToolSettings);

            // Update security settings
            SecuritySettingsData securityData = CreateSecuritySettingsData();
            _uitView.UpdateSecuritySettings(securityData, UpdateShowSecuritySettings,
                UpdateEnableTestsExecution, UpdateAllowMenuItemExecution,
                UpdateAllowThirdPartyTools);
        }

        #endregion
    }
}