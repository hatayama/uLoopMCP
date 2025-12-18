using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

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
        }

        private void OnDestroy()
        {
            _view?.Dispose();
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

            bool gitRootDiffers = UnityMcpPathResolver.GitRootDiffersFromProjectRoot();
            _model.UpdateSupportsRepositoryRootToggle(gitRootDiffers);
            _model.UpdateShowRepositoryRootToggle(gitRootDiffers);

            if (!gitRootDiffers && _model.UI.AddRepositoryRoot)
            {
                _model.UpdateAddRepositoryRoot(false);
            }
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
        private async void HandlePostCompileMode()
        {
            // Enable post-compile mode after domain reload
            _model.EnablePostCompileMode();

            // Clear reconnecting UI flag on domain reload to ensure proper state
            McpEditorSettings.SetShowReconnectingUI(false);

            // Wait for any ongoing recovery to complete before making auto-start decisions
            // This prevents race conditions between McpServerController and McpEditorWindow
            Task recoveryTask = McpServerController.RecoveryTask;
            if (recoveryTask != null && !recoveryTask.IsCompleted)
            {
                await recoveryTask;
            }

            // Check if after compilation
            bool isAfterCompile = McpEditorSettings.GetIsAfterCompile();

            // Grace period is already started in OnEnable() if needed

            // After compile, rely on centralized recovery (McpServerController.StartRecoveryIfNeededAsync).
            // EditorWindow does not auto-start the server; it only updates UI state and port display.
            if (isAfterCompile)
            {
                McpEditorSettings.ClearAfterCompileFlag();

                // Use saved port number for UI only
                int savedPort = McpEditorSettings.GetServerPort();
                bool portNeedsUpdate = savedPort != _model.UI.CustomPort;

                if (portNeedsUpdate)
                {
                    _model.UpdateCustomPort(savedPort);
                }

                return;
            }

            // Determine if server should be started automatically (normal auto-start, not after-compile)
            // Skip auto-start if recovery is in progress to avoid conflicting with StartRecoveryIfNeededAsync
            // Skip auto-start on first launch so users can see initial settings before server starts
            bool shouldStartAutomatically = _model.UI.AutoStartServer;
            bool serverNotRunning = !McpServerController.IsServerRunning;
            bool isRecoveryInProgress = McpServerController.IsStartupProtectionActive();
            bool isFirstLaunch = McpEditorSettings.IsFirstLaunch;
            bool shouldStartServer = shouldStartAutomatically && serverNotRunning && !isRecoveryInProgress && !isFirstLaunch;

            if (shouldStartServer)
            {
                _serverOperations.StartServerInternal();
            }
        }

        private void OnDisable()
        {
            CleanupEventHandler();
            SaveSessionState();
            _view?.Dispose();
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
                foldoutCallback: UpdateShowLLMToolSettings,
                repositoryRootToggleCallback: UpdateAddRepositoryRoot);

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
                    portWarningMessage = $"Port {requestedPort} is already in use. Please choose a different port or stop the other process using this port.";
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
                IMcpConfigService configService = GetConfigService(_model.UI.SelectedEditorType);
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

            return new EditorConfigData(
                _model.UI.SelectedEditorType,
                _model.UI.ShowLLMToolSettings,
                isServerRunning,
                currentPort,
                isConfigured,
                hasPortMismatch,
                configurationError,
                isUpdateNeeded,
                _model.UI.AddRepositoryRoot,
                _model.UI.SupportsRepositoryRootToggle,
                _model.UI.ShowRepositoryRootToggle);
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
            IMcpConfigService configService = GetConfigService(_model.UI.SelectedEditorType);
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
        private IMcpConfigService GetConfigService(McpEditorType editorType)
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
        /// Update AddRepositoryRoot setting
        /// </summary>
        private void UpdateAddRepositoryRoot(bool addRepositoryRoot)
        {
            _model.UpdateAddRepositoryRoot(addRepositoryRoot);
            Repaint();
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