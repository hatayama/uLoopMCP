using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    public class McpEditorWindow : EditorWindow
    {
        private McpConfigServiceFactory _configServiceFactory;
        private McpEditorWindowUI _view;
        private McpEditorModel _model;
        private McpEditorWindowEventHandler _eventHandler;
        private McpServerOperations _serverOperations;
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

        private void CreateGUI()
        {
            InitializeView();
            RefreshAllSections();
        }

        private void InitializeAll()
        {
            InitializeModel();
            InitializeConfigurationServices();
            InitializeEventHandler();
            InitializeServerOperations();
            LoadSavedSettings();
            RestoreSessionState();
            HandlePostCompileMode();
        }

        private void InitializeModel()
        {
            _model = new McpEditorModel();
        }

        private void InitializeView()
        {
            _view = new McpEditorWindowUI(rootVisualElement);
            SetupViewCallbacks();
        }

        private void SetupViewCallbacks()
        {
            _view.OnToggleServer += ToggleServer;
            _view.OnAutoStartChanged += UpdateAutoStartServer;
            _view.OnPortChanged += UpdateCustomPort;
            _view.OnConnectedToolsFoldoutChanged += UpdateShowConnectedTools;
            _view.OnEditorTypeChanged += UpdateSelectedEditorType;
            _view.OnLLMSettingsFoldoutChanged += UpdateShowLLMToolSettings;
            _view.OnRepositoryRootChanged += UpdateAddRepositoryRoot;
            _view.OnConfigureClicked += ConfigureEditor;
            _view.OnOpenSettingsClicked += OpenConfigurationFile;
            _view.OnSecurityFoldoutChanged += UpdateShowSecuritySettings;
            _view.OnEnableTestsChanged += UpdateEnableTestsExecution;
            _view.OnAllowMenuChanged += UpdateAllowMenuItemExecution;
            _view.OnAllowThirdPartyChanged += UpdateAllowThirdPartyTools;
            _view.OnSecurityLevelChanged += UpdateDynamicCodeSecurityLevel;
        }

        public IEnumerable<ConnectedClient> GetConnectedToolsAsClients()
        {
            return ConnectedToolsMonitoringService.GetConnectedToolsAsClients();
        }

        private void InitializeConfigurationServices()
        {
            _configServiceFactory = new McpConfigServiceFactory();
        }

        private void InitializeEventHandler()
        {
            _eventHandler = new McpEditorWindowEventHandler(_model, this);
            _eventHandler.Initialize();
        }

        private void InitializeServerOperations()
        {
            _serverOperations = new McpServerOperations(_model, _eventHandler);
        }

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

        private void RestoreSessionState()
        {
            _model.LoadFromSessionState();
        }

        private async void HandlePostCompileMode()
        {
            _model.EnablePostCompileMode();
            McpEditorSettings.SetShowReconnectingUI(false);

            Task recoveryTask = McpServerController.RecoveryTask;
            if (recoveryTask != null && !recoveryTask.IsCompleted)
            {
                await recoveryTask;
            }

            bool isAfterCompile = McpEditorSettings.GetIsAfterCompile();

            if (isAfterCompile)
            {
                McpEditorSettings.ClearAfterCompileFlag();

                int savedPort = McpEditorSettings.GetServerPort();
                bool portNeedsUpdate = savedPort != _model.UI.CustomPort;

                if (portNeedsUpdate)
                {
                    _model.UpdateCustomPort(savedPort);
                }

                return;
            }

            bool shouldStartAutomatically = _model.UI.AutoStartServer;
            bool serverNotRunning = !McpServerController.IsServerRunning;
            bool isRecoveryInProgress = McpServerController.IsStartupProtectionActive();
            bool hasCompletedFirstLaunch = McpEditorSettings.GetHasCompletedFirstLaunch();
            bool shouldStartServer = shouldStartAutomatically && serverNotRunning && !isRecoveryInProgress && hasCompletedFirstLaunch;

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

        private void CleanupEventHandler()
        {
            _eventHandler?.Cleanup();
        }

        private void SaveSessionState()
        {
            _model.SaveToSessionState();
        }

        private void OnFocus()
        {
            RefreshAllSections();
        }

        public void RefreshAllSections()
        {
            if (_view == null)
            {
                return;
            }

            SyncPortSettings();

            ServerStatusData statusData = CreateServerStatusData();
            _view.UpdateServerStatus(statusData);

            ServerControlsData controlsData = CreateServerControlsData();
            _view.UpdateServerControls(controlsData);

            ConnectedToolsData toolsData = CreateConnectedToolsData();
            _view.UpdateConnectedTools(toolsData);

            EditorConfigData configData = CreateEditorConfigData();
            _view.UpdateEditorConfig(configData);

            SecuritySettingsData securityData = CreateSecuritySettingsData();
            _view.UpdateSecuritySettings(securityData);
        }

        public void RefreshConnectedToolsSection()
        {
            if (_view == null)
            {
                return;
            }

            ConnectedToolsData toolsData = CreateConnectedToolsData();
            _view.UpdateConnectedTools(toolsData);
        }

        private void SyncPortSettings()
        {
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

        private ServerStatusData CreateServerStatusData()
        {
            (bool isRunning, int port, bool _) = McpServerController.GetServerStatus();
            string status = isRunning ? "Running" : "Stopped";
            Color statusColor = isRunning ? Color.green : Color.red;

            return new ServerStatusData(isRunning, port, status, statusColor);
        }

        private ServerControlsData CreateServerControlsData()
        {
            bool isRunning = McpServerController.IsServerRunning;

            bool hasPortWarning = false;
            string portWarningMessage = null;

            if (!isRunning)
            {
                int requestedPort = _model.UI.CustomPort;

                if (!McpPortValidator.ValidatePort(requestedPort))
                {
                    hasPortWarning = true;
                    portWarningMessage = $"Port {requestedPort} is invalid. Port must be 1024 or higher and not a reserved system port.";
                }
                else if (NetworkUtility.IsPortInUse(requestedPort))
                {
                    hasPortWarning = true;
                    portWarningMessage = $"Port {requestedPort} is already in use. Please choose a different port or stop the other process using this port.";
                }
            }

            return new ServerControlsData(_model.UI.CustomPort, _model.UI.AutoStartServer, isRunning, !isRunning, hasPortWarning, portWarningMessage);
        }

        private IEnumerable<ConnectedClient> GetCachedStoredTools()
        {
            const float cacheDuration = 0.1f;
            float currentTime = Time.realtimeSinceStartup;

            if (_cachedStoredTools == null || (currentTime - _lastStoredToolsUpdateTime) > cacheDuration)
            {
                _cachedStoredTools = GetConnectedToolsAsClients();
                _lastStoredToolsUpdateTime = currentTime;
            }

            return _cachedStoredTools;
        }

        private void InvalidateStoredToolsCache()
        {
            _cachedStoredTools = null;
        }

        private ConnectedToolsData CreateConnectedToolsData()
        {
            bool isServerRunning = McpServerController.IsServerRunning;
            IReadOnlyCollection<ConnectedClient> connectedClients = McpServerController.CurrentServer?.GetConnectedClients();

            bool showReconnectingUIFlag = McpEditorSettings.GetShowReconnectingUI();
            bool showPostCompileUIFlag = McpEditorSettings.GetShowPostCompileReconnectingUI();

            bool hasNamedClients = connectedClients != null &&
                                   connectedClients.Any(client => client.ClientName != McpConstants.UNKNOWN_CLIENT_NAME);

            IEnumerable<ConnectedClient> storedTools = GetCachedStoredTools();
            bool hasStoredTools = storedTools.Any();

            if (hasStoredTools)
            {
                connectedClients = storedTools.ToList();
                hasNamedClients = true;
            }

            bool showReconnectingUI = !hasStoredTools &&
                                      (showReconnectingUIFlag || showPostCompileUIFlag) &&
                                      !hasNamedClients;

            if (hasNamedClients && showPostCompileUIFlag)
            {
                McpEditorSettings.ClearPostCompileReconnectingUI();
            }

            return new ConnectedToolsData(connectedClients, _model.UI.ShowConnectedTools, isServerRunning, showReconnectingUI);
        }

        private EditorConfigData CreateEditorConfigData()
        {
            bool isServerRunning = McpServerController.IsServerRunning;
            int currentPort = McpServerController.ServerPort;

            bool isConfigured = false;
            bool hasPortMismatch = false;
            bool isUpdateNeeded = true;
            string configurationError = null;

            IMcpConfigService configService = GetConfigService(_model.UI.SelectedEditorType);
            isConfigured = configService.IsConfigured();

            if (isConfigured)
            {
                int configuredPort = configService.GetConfiguredPort();

                if (isServerRunning)
                {
                    hasPortMismatch = currentPort != configuredPort;
                }
                else
                {
                    hasPortMismatch = _model.UI.CustomPort != configuredPort;
                }
            }

            int portToCheck = isServerRunning ? currentPort : _model.UI.CustomPort;
            isUpdateNeeded = configService.IsUpdateNeeded(portToCheck);

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

        private SecuritySettingsData CreateSecuritySettingsData()
        {
            return new SecuritySettingsData(
                _model.UI.ShowSecuritySettings,
                McpEditorSettings.GetEnableTestsExecution(),
                McpEditorSettings.GetAllowMenuItemExecution(),
                McpEditorSettings.GetAllowThirdPartyTools());
        }

        private void ConfigureEditor()
        {
            IMcpConfigService configService = GetConfigService(_model.UI.SelectedEditorType);
            bool isServerRunning = McpServerController.IsServerRunning;
            int portToUse = isServerRunning ? McpServerController.ServerPort : _model.UI.CustomPort;

            configService.AutoConfigure(portToUse);
            RefreshAllSections();
        }

        private void OpenConfigurationFile()
        {
            string projectRoot = UnityMcpPathResolver.GetProjectRoot();
            string gitRoot = UnityMcpPathResolver.GetGitRepositoryRoot();
            string baseRoot = _model.UI.AddRepositoryRoot
                ? (gitRoot ?? projectRoot)
                : projectRoot;

            string configPath = UnityMcpPathResolver.GetConfigPathForRoot(_model.UI.SelectedEditorType, baseRoot);
            bool exists = System.IO.File.Exists(configPath);

            if (exists)
            {
                EditorUtility.OpenWithDefaultApp(configPath);
            }
            else
            {
                string editorName = GetEditorDisplayName(_model.UI.SelectedEditorType);
                EditorUtility.DisplayDialog(
                    "Configuration File Not Found",
                    $"Configuration file for {editorName} not found at:\n{configPath}\n\nPlease run 'Configure {editorName}' first to create the configuration file.",
                    "OK");
            }
        }

        private string GetEditorDisplayName(McpEditorType editorType)
        {
            return editorType switch
            {
                McpEditorType.Cursor => "Cursor",
                McpEditorType.ClaudeCode => "Claude Code",
                McpEditorType.VSCode => "VSCode",
                McpEditorType.GeminiCLI => "Gemini CLI",
                McpEditorType.Codex => "Codex",
                McpEditorType.McpInspector => "MCP Inspector",
                _ => editorType.ToString()
            };
        }

        private void StartServer()
        {
            if (_serverOperations.StartServer())
            {
                RefreshAllSections();
            }
        }

        private void StopServer()
        {
            _serverOperations.StopServer();
            RefreshAllSections();
        }

        private IMcpConfigService GetConfigService(McpEditorType editorType)
        {
            return _configServiceFactory.GetConfigService(editorType);
        }

        private void UpdateAutoStartServer(bool autoStart)
        {
            _model.UpdateAutoStartServer(autoStart);
        }

        private void UpdateCustomPort(int port)
        {
            _model.UpdateCustomPort(port);
            RefreshAllSections();
        }

        private void UpdateShowConnectedTools(bool show)
        {
            _model.UpdateShowConnectedTools(show);
        }

        private void UpdateShowLLMToolSettings(bool show)
        {
            _model.UpdateShowLLMToolSettings(show);
        }

        private void UpdateSelectedEditorType(McpEditorType type)
        {
            _model.UpdateSelectedEditorType(type);
            RefreshAllSections();
        }

        private void UpdateShowSecuritySettings(bool show)
        {
            _model.UpdateShowSecuritySettings(show);
        }

        private void UpdateEnableTestsExecution(bool enable)
        {
            _model.UpdateEnableTestsExecution(enable);
        }

        private void UpdateAllowMenuItemExecution(bool allow)
        {
            _model.UpdateAllowMenuItemExecution(allow);
        }

        private void UpdateAllowThirdPartyTools(bool allow)
        {
            _model.UpdateAllowThirdPartyTools(allow);
        }

        private void UpdateAddRepositoryRoot(bool addRepositoryRoot)
        {
            _model.UpdateAddRepositoryRoot(addRepositoryRoot);
            RefreshAllSections();
        }

        private void UpdateDynamicCodeSecurityLevel(DynamicCodeSecurityLevel level)
        {
            McpEditorSettings.SetDynamicCodeSecurityLevel(level);
        }

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
