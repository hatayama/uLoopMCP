using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Linq;

#if ULOOPMCP_DEBUG
using System.Collections.Generic;
using System;
#endif

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// UI Toolkit version of Editor Window for controlling Unity MCP Server
    /// Design document: .kiro/specs/mcp-editor-ui-toolkit-migration/design.md
    /// Related classes:
    /// - McpEditorModel: Model layer for state management and business logic  
    /// - McpEditorWindowEventHandler: Event management helper
    /// - McpServerOperations: Server operations helper
    /// - McpConfigServiceFactory: Configuration services factory
    /// </summary>
    public class McpEditorWindow : EditorWindow
    {
        private VisualElement _rootElement;
        
        // Model and helper instances (same as OnGUI version)
        private McpConfigServiceFactory _configServiceFactory;
        private McpEditorModel _model;
        private McpEditorWindowEventHandler _eventHandler;
        private McpServerOperations _serverOperations;

        [MenuItem("Window/uLoopMCP")]
        public static void ShowWindow()
        {
            McpEditorWindow window = GetWindow<McpEditorWindow>(McpConstants.PROJECT_NAME);
            window.Show();
        }

        /// <summary>
        /// Unity 2022.3 CreateGUI method for UI Toolkit
        /// </summary>
        public void CreateGUI()
        {
            InitializeModel();
            InitializeConfigurationServices(); 
            InitializeEventHandler();
            InitializeServerOperations();
            LoadSavedSettings();
            RestoreSessionState();
            
            CreateUIElements();
            HandlePostCompileMode();
            SetupEventDrivenUpdates();
        }

        /// <summary>
        /// Create UI Toolkit elements
        /// </summary>
        private void CreateUIElements()
        {
            _rootElement = rootVisualElement;
            
            // Load main UXML template
            VisualTreeAsset visualTree = Resources.Load<VisualTreeAsset>("UI/Main/McpEditorWindow");
            if (visualTree == null)
            {
                Debug.LogError("Failed to load McpEditorWindow UXML template");
                CreateFallbackUI();
                return;
            }
            
            visualTree.CloneTree(_rootElement);
            
            // Initialize component bindings and event handlers
            SetupUIBindings();
        }

        /// <summary>
        /// Setup UI element bindings and event handlers
        /// </summary>
        private void SetupUIBindings()
        {
            // Server Controls
            SetupServerControlsBindings();
            
            // Connected Tools
            SetupConnectedToolsBindings();
            
            // Editor Config
            SetupEditorConfigBindings();
            
            // Security Settings
            SetupSecuritySettingsBindings();
            
            // Initial UI state update
            UpdateAllUIElements();
        }

        /// <summary>
        /// Setup server controls event handlers
        /// </summary>
        private void SetupServerControlsBindings()
        {
            // Port field
            IntegerField portField = _rootElement.Q<IntegerField>("port-field");
            if (portField != null)
            {
                portField.RegisterValueChangedCallback(evt => UpdateCustomPort(evt.newValue));
            }

            // Toggle server button
            Button toggleButton = _rootElement.Q<Button>("toggle-server-button");
            if (toggleButton != null)
            {
                toggleButton.clicked += ToggleServer;
            }

            // Auto start toggle
            Toggle autoStartToggle = _rootElement.Q<Toggle>("auto-start-toggle");
            if (autoStartToggle != null)
            {
                autoStartToggle.RegisterValueChangedCallback(evt => UpdateAutoStartServer(evt.newValue));
            }
        }

        /// <summary>
        /// Setup connected tools event handlers
        /// </summary>
        private void SetupConnectedToolsBindings()
        {
            Foldout connectedToolsFoldout = _rootElement.Q<Foldout>("connected-tools-foldout");
            if (connectedToolsFoldout != null)
            {
                connectedToolsFoldout.RegisterValueChangedCallback(evt => UpdateShowConnectedTools(evt.newValue));
            }
        }

        /// <summary>
        /// Setup editor config event handlers
        /// </summary>
        private void SetupEditorConfigBindings()
        {
            // Editor type enum
            EnumField editorTypeEnum = _rootElement.Q<EnumField>("editor-type-enum");
            if (editorTypeEnum != null)
            {
                editorTypeEnum.Init(McpEditorType.ClaudeCode);
                editorTypeEnum.RegisterValueChangedCallback(evt => UpdateSelectedEditorType((McpEditorType)evt.newValue));
            }

            // Configure button
            Button configureButton = _rootElement.Q<Button>("configure-button");
            if (configureButton != null)
            {
                configureButton.clicked += ConfigureEditor;
            }

            // Open settings button
            Button openSettingsButton = _rootElement.Q<Button>("open-settings-button");
            if (openSettingsButton != null)
            {
                openSettingsButton.clicked += OpenConfigurationFile;
            }

            // Editor config foldout
            Foldout editorConfigFoldout = _rootElement.Q<Foldout>("editor-config-foldout");
            if (editorConfigFoldout != null)
            {
                editorConfigFoldout.RegisterValueChangedCallback(evt => UpdateShowLLMToolSettings(evt.newValue));
            }
        }

        /// <summary>
        /// Setup security settings event handlers
        /// </summary>
        private void SetupSecuritySettingsBindings()
        {
            // Security settings foldout
            Foldout securityFoldout = _rootElement.Q<Foldout>("security-settings-foldout");
            if (securityFoldout != null)
            {
                securityFoldout.RegisterValueChangedCallback(evt => UpdateShowSecuritySettings(evt.newValue));
            }

            // Enable tests toggle
            Toggle enableTestsToggle = _rootElement.Q<Toggle>("enable-tests-toggle");
            if (enableTestsToggle != null)
            {
                enableTestsToggle.RegisterValueChangedCallback(evt => UpdateEnableTestsExecution(evt.newValue));
            }

            // Allow menu toggle
            Toggle allowMenuToggle = _rootElement.Q<Toggle>("allow-menu-toggle");
            if (allowMenuToggle != null)
            {
                allowMenuToggle.RegisterValueChangedCallback(evt => UpdateAllowMenuItemExecution(evt.newValue));
            }

            // Allow third party toggle
            Toggle allowThirdPartyToggle = _rootElement.Q<Toggle>("allow-third-party-toggle");
            if (allowThirdPartyToggle != null)
            {
                allowThirdPartyToggle.RegisterValueChangedCallback(evt => UpdateAllowThirdPartyTools(evt.newValue));
            }
        }

        /// <summary>
        /// Create fallback UI when UXML fails to load
        /// </summary>
        private void CreateFallbackUI()
        {
            Label fallbackLabel = new Label("UI Toolkit implementation in progress...")
            {
                style = 
                {
                    fontSize = 14,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    paddingTop = 20,
                    paddingBottom = 20
                }
            };
            _rootElement.Add(fallbackLabel);
        }

        private void OnEnable()
        {
            // UI Toolkit uses CreateGUI instead of OnEnable for UI creation
        }

        private void OnDisable()
        {
            CleanupEventHandler();
            SaveSessionState();
        }

        /// <summary>
        /// Initialize model layer
        /// </summary>
        private void InitializeModel()
        {
            _model = new McpEditorModel();
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

        /// <summary>
        /// Cleanup event handler
        /// </summary>
        private void CleanupEventHandler()
        {
            _eventHandler?.Cleanup();
            
            // Unsubscribe from EditorApplication events
            EditorApplication.update -= ForceBackgroundUIUpdate;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.projectChanged -= OnProjectChanged;
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
            // UI Toolkit equivalent to Repaint - updates will be handled by data binding
            UpdateAllUIElements();
        }

        /// <summary>
        /// Public method for external event handlers to trigger UI updates
        /// Called by McpEditorWindowEventHandler when server state changes occur
        /// </summary>
        public void RefreshUI()
        {
            // Update all UI elements when server events occur
            UpdateAllUIElements();
            
            // Set flag for background update to ensure update even when not focused
            _needsUIUpdate = true;
            
            // Force UI Toolkit element-level repaint and window-level repaint
            if (_rootElement != null)
            {
                _rootElement.MarkDirtyRepaint();
            }
            Repaint();
        }

        /// <summary>
        /// Setup event-driven UI updates for server and client connection changes
        /// </summary>
        private void SetupEventDrivenUpdates()
        {
            // Event-driven updates will be handled by McpEditorWindowEventHandler
            // which already listens to server state changes and client connections
            
            // Subscribe to multiple EditorApplication events for background updates
            // This ensures UI updates even when window is not focused
            EditorApplication.update += ForceBackgroundUIUpdate;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.projectChanged += OnProjectChanged;
        }

        /// <summary>
        /// Flag to track if UI needs update to avoid excessive repaints
        /// </summary>
        private bool _needsUIUpdate = false;
        
        /// <summary>
        /// Force background UI update when window is not focused
        /// Called by EditorApplication.update to ensure UI updates during domain reload
        /// </summary>
        private void ForceBackgroundUIUpdate()
        {
            // Only update if needed and window exists
            if (_needsUIUpdate && this != null && _rootElement != null)
            {
                _needsUIUpdate = false;
                
                // Update UI elements first
                UpdateAllUIElements();
                
                // Force UI Toolkit to mark dirty and repaint - this is the key!
                _rootElement.MarkDirtyRepaint();
                Repaint();
            }
        }
        
        /// <summary>
        /// Force UI update when hierarchy changes
        /// </summary>
        private void OnHierarchyChanged()
        {
            _needsUIUpdate = true;
        }
        
        /// <summary>
        /// Force UI update when project changes
        /// </summary>
        private void OnProjectChanged()
        {
            _needsUIUpdate = true;
        }

        // =============================================================================
        // UI Update Methods (migrated from OnGUI version)
        // =============================================================================

        /// <summary>
        /// Update all UI elements with current model state
        /// </summary>
        private void UpdateAllUIElements()
        {
            UpdateServerStatusUI();
            UpdateServerControlsUI();
            UpdateConnectedToolsUI();
            UpdateEditorConfigUI();
            UpdateSecuritySettingsUI();
        }

        /// <summary>
        /// Update server status display
        /// </summary>
        private void UpdateServerStatusUI()
        {
            Label statusLabel = _rootElement.Q<Label>("server-status__value");
            if (statusLabel != null)
            {
                bool isRunning = McpServerController.IsServerRunning;
                statusLabel.text = isRunning ? "Running" : "Stopped";
                
                // Apply CSS classes for styling
                statusLabel.RemoveFromClassList("server-status__value--running");
                statusLabel.RemoveFromClassList("server-status__value--stopped");
                statusLabel.AddToClassList(isRunning ? "server-status__value--running" : "server-status__value--stopped");
            }
        }

        /// <summary>
        /// Update server controls UI elements
        /// </summary>
        private void UpdateServerControlsUI()
        {
            bool isRunning = McpServerController.IsServerRunning;

            // Port field
            IntegerField portField = _rootElement.Q<IntegerField>("port-field");
            if (portField != null)
            {
                portField.value = _model.UI.CustomPort;
                portField.SetEnabled(!isRunning);
            }

            // Toggle button
            Button toggleButton = _rootElement.Q<Button>("toggle-server-button");
            if (toggleButton != null)
            {
                toggleButton.text = isRunning ? "Stop Server" : "Start Server";
                
                // Apply CSS classes for button styling
                toggleButton.RemoveFromClassList("server-controls__toggle-button--start");
                toggleButton.RemoveFromClassList("server-controls__toggle-button--stop");
                toggleButton.AddToClassList(isRunning ? "server-controls__toggle-button--stop" : "server-controls__toggle-button--start");
            }

            // Auto start toggle
            Toggle autoStartToggle = _rootElement.Q<Toggle>("auto-start-toggle");
            if (autoStartToggle != null)
            {
                autoStartToggle.value = _model.UI.AutoStartServer;
            }

            // Port warning
            UpdatePortWarningUI();
        }

        /// <summary>
        /// Update port warning display
        /// </summary>
        private void UpdatePortWarningUI()
        {
            VisualElement portWarning = _rootElement.Q<VisualElement>("port-warning");
            Label warningMessage = _rootElement.Q<Label>("port-warning__message");
            
            if (portWarning != null && warningMessage != null)
            {
                bool isRunning = McpServerController.IsServerRunning;
                bool hasWarning = false;
                string warningText = "";

                if (!isRunning)
                {
                    int requestedPort = _model.UI.CustomPort;
                    if (NetworkUtility.IsPortInUse(requestedPort))
                    {
                        hasWarning = true;
                        warningText = $"Port {requestedPort} is already in use. Server will automatically find an available port when started.";
                    }
                }

                portWarning.style.display = hasWarning ? DisplayStyle.Flex : DisplayStyle.None;
                warningMessage.text = warningText;
            }
        }

        /// <summary>
        /// Update connected tools UI
        /// </summary>
        private void UpdateConnectedToolsUI()
        {
            Foldout foldout = _rootElement.Q<Foldout>("connected-tools-foldout");
            if (foldout != null)
            {
                foldout.value = _model.UI.ShowConnectedTools;
            }

            UpdateConnectionStatus();
        }

        /// <summary>
        /// Update connection status and client list (migrated from OnGUI version)
        /// </summary>
        private void UpdateConnectionStatus()
        {
            bool isServerRunning = McpServerController.IsServerRunning;
            var connectedClients = McpServerController.CurrentServer?.GetConnectedClients();

            // Check reconnecting UI flags from McpSessionManager
            bool showReconnectingUIFlag = McpSessionManager.instance.ShowReconnectingUI;
            bool showPostCompileUIFlag = McpSessionManager.instance.ShowPostCompileReconnectingUI;

            // Only count clients with proper names (not Unknown Client) as "connected"
            bool hasNamedClients = connectedClients != null &&
                                   connectedClients.Any(client => client.ClientName != McpConstants.UNKNOWN_CLIENT_NAME);

            // Show reconnecting if either flag is true and no named clients are connected
            bool showReconnectingUI = (showReconnectingUIFlag || showPostCompileUIFlag) && !hasNamedClients;

            // Clear post-compile flag when named clients are connected
            if (hasNamedClients && showPostCompileUIFlag)
            {
                McpSessionManager.instance.ClearPostCompileReconnectingUI();
            }

            // Get UI elements
            Label statusMessage = _rootElement.Q<Label>("status-message");
            VisualElement clientList = _rootElement.Q<VisualElement>("client-list");

            if (statusMessage == null || clientList == null) return;

            // Clear previous client list
            clientList.Clear();

            if (!isServerRunning)
            {
                statusMessage.text = "Server is not running. Start the server to see connected tools.";
                statusMessage.RemoveFromClassList("connected-tools__status-message--info");
                statusMessage.AddToClassList("connected-tools__status-message--warning");
                statusMessage.style.display = DisplayStyle.Flex;
                return;
            }

            if (showReconnectingUI)
            {
                statusMessage.text = McpUIConstants.RECONNECTING_MESSAGE;
                statusMessage.RemoveFromClassList("connected-tools__status-message--warning");
                statusMessage.AddToClassList("connected-tools__status-message--info");
                statusMessage.style.display = DisplayStyle.Flex;
                return;
            }

            if (connectedClients != null && connectedClients.Count > 0)
            {
                // Filter out clients with default or unknown names
                var validClients = connectedClients.Where(client => IsValidClientName(client.ClientName)).ToList();

                if (validClients.Count > 0)
                {
                    // Hide status message and show client list
                    statusMessage.style.display = DisplayStyle.None;

                    foreach (ConnectedClient client in validClients)
                    {
                        CreateConnectedClientItem(client, clientList);
                    }
                    return;
                }
            }

            // No valid clients found
            statusMessage.text = "No connected tools found.";
            statusMessage.RemoveFromClassList("connected-tools__status-message--warning");
            statusMessage.AddToClassList("connected-tools__status-message--info");
            statusMessage.style.display = DisplayStyle.Flex;
        }

        /// <summary>
        /// Create a connected client item UI element (migrated from OnGUI version)
        /// </summary>
        private void CreateConnectedClientItem(ConnectedClient client, VisualElement parentContainer)
        {
            VisualElement clientItem = new VisualElement();
            clientItem.AddToClassList("connected-tools__client-item");

            // Client name with icon
            Label clientName = new Label(McpUIConstants.CLIENT_ICON + client.ClientName);
            clientName.AddToClassList("connected-tools__client-name");
            clientName.AddToClassList("connected-tools__client-icon");
            clientItem.Add(clientName);

            // Endpoint information
            Label endpoint = new Label(McpUIConstants.ENDPOINT_ARROW + client.Endpoint);
            endpoint.AddToClassList("connected-tools__client-endpoint");
            endpoint.AddToClassList("connected-tools__endpoint-arrow");
            clientItem.Add(endpoint);

            parentContainer.Add(clientItem);
        }

        /// <summary>
        /// Check if client name is valid for display (migrated from OnGUI version)
        /// </summary>
        private bool IsValidClientName(string clientName)
        {
            // Only show clients with properly set names
            // Filter out empty names and the default "Unknown Client" placeholder
            return !string.IsNullOrEmpty(clientName) && clientName != McpConstants.UNKNOWN_CLIENT_NAME;
        }

        /// <summary>
        /// Update editor config UI
        /// </summary>
        private void UpdateEditorConfigUI()
        {
            // Foldout state
            Foldout foldout = _rootElement.Q<Foldout>("editor-config-foldout");
            if (foldout != null)
            {
                foldout.value = _model.UI.ShowLLMToolSettings;
            }

            // Editor type enum
            EnumField editorTypeEnum = _rootElement.Q<EnumField>("editor-type-enum");
            if (editorTypeEnum != null)
            {
                editorTypeEnum.value = _model.UI.SelectedEditorType;
            }

            // Configure button text and styling
            UpdateConfigureButtonUI();
        }

        /// <summary>
        /// Update configure button text and styling
        /// </summary>
        private void UpdateConfigureButtonUI()
        {
            Button configureButton = _rootElement.Q<Button>("configure-button");
            VisualElement configError = _rootElement.Q<VisualElement>("config-error");
            Label configErrorMessage = _rootElement.Q<Label>("config-error__message");
            
            if (configureButton == null) return;

            bool isServerRunning = McpServerController.IsServerRunning;
            int currentPort = McpServerController.ServerPort;
            string editorName = GetEditorDisplayName(_model.UI.SelectedEditorType);

            // Check configuration status with error handling
            bool isConfigured = false;
            bool hasPortMismatch = false;
            bool needsUpdate = false;
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

                // Determine if configuration needs update
                needsUpdate = !isConfigured || hasPortMismatch;
            }
            catch (System.Exception ex)
            {
                configurationError = ex.Message;
                needsUpdate = true; // If there's an error, allow user to try configuring
            }

            // Display configuration error if any
            if (configError != null && configErrorMessage != null)
            {
                if (!string.IsNullOrEmpty(configurationError))
                {
                    configError.style.display = DisplayStyle.Flex;
                    configErrorMessage.text = $"Error loading {editorName} configuration: {configurationError}";
                }
                else
                {
                    configError.style.display = DisplayStyle.None;
                }
            }

            // Update button text and state based on configuration status
            string buttonText;
            bool buttonEnabled = needsUpdate;

            if (isConfigured)
            {
                if (hasPortMismatch)
                {
                    buttonText = isServerRunning ? 
                        $"Update {editorName} Settings\n(Port mismatch - Server: {currentPort})" : 
                        $"Update {editorName} Settings\n(Port mismatch)";
                }
                else
                {
                    // Configuration is up to date - show appropriate message
                    buttonText = isServerRunning ? 
                        $"{editorName} Settings\n(Up to date - Port {currentPort})" : 
                        $"{editorName} Settings\n(Up to date)";
                }
            }
            else
            {
                buttonText = $"Settings not found. \nConfigure {editorName}";
            }

            configureButton.text = buttonText;

            // Apply appropriate styling based on configuration status
            configureButton.RemoveFromClassList("editor-config__configure-button--warning");
            configureButton.RemoveFromClassList("editor-config__configure-button--normal");
            
            if (needsUpdate)
            {
                if (!isConfigured || hasPortMismatch)
                {
                    // Warning state: not configured or port mismatch - use warning yellow
                    configureButton.AddToClassList("editor-config__configure-button--warning");
                }
                // Button is enabled and clickable
                configureButton.SetEnabled(true);
            }
            else
            {
                // Configuration is up to date - disable button and use normal styling
                configureButton.SetEnabled(false);
                // Don't add any special classes - let it use Unity's default disabled button appearance
            }
            
            // Debug log for troubleshooting
            Debug.Log($"Configure button: isConfigured={isConfigured}, hasPortMismatch={hasPortMismatch}, needsUpdate={needsUpdate}, enabled={buttonEnabled}, text='{buttonText}'");
        }

        /// <summary>
        /// Update security settings UI
        /// </summary>
        private void UpdateSecuritySettingsUI()
        {
            // Foldout state
            Foldout foldout = _rootElement.Q<Foldout>("security-settings-foldout");
            if (foldout != null)
            {
                foldout.value = _model.UI.ShowSecuritySettings;
            }

            // Toggle states
            Toggle enableTestsToggle = _rootElement.Q<Toggle>("enable-tests-toggle");
            if (enableTestsToggle != null)
            {
                enableTestsToggle.value = McpEditorSettings.GetEnableTestsExecution();
            }

            Toggle allowMenuToggle = _rootElement.Q<Toggle>("allow-menu-toggle");
            if (allowMenuToggle != null)
            {
                allowMenuToggle.value = McpEditorSettings.GetAllowMenuItemExecution();
            }

            Toggle allowThirdPartyToggle = _rootElement.Q<Toggle>("allow-third-party-toggle");
            if (allowThirdPartyToggle != null)
            {
                allowThirdPartyToggle.value = McpEditorSettings.GetAllowThirdPartyTools();
            }
        }

        // =============================================================================
        // Helper Methods (migrated from OnGUI version)
        // =============================================================================

        /// <summary>
        /// Get display name for editor type
        /// </summary>
        private string GetEditorDisplayName(McpEditorType editorType)
        {
            return editorType switch
            {
                McpEditorType.Cursor => "Cursor",
                McpEditorType.ClaudeCode => "Claude Code",
                McpEditorType.VSCode => "VSCode",
                McpEditorType.GeminiCLI => "Gemini CLI",
                McpEditorType.McpInspector => "MCP Inspector",
                _ => editorType.ToString()
            };
        }

        /// <summary>
        /// Get configuration service for editor type
        /// </summary>
        private McpConfigService GetConfigService(McpEditorType editorType)
        {
            return _configServiceFactory.GetConfigService(editorType);
        }

        // =============================================================================
        // Action Methods (migrated from OnGUI version)
        // =============================================================================

        /// <summary>
        /// Toggle server state
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

        /// <summary>
        /// Start server
        /// </summary>
        private void StartServer()
        {
            if (_serverOperations.StartServer())
            {
                UpdateAllUIElements();
            }
        }

        /// <summary>
        /// Stop server
        /// </summary>
        private void StopServer()
        {
            _serverOperations.StopServer();
            UpdateAllUIElements();
        }

        /// <summary>
        /// Configure editor settings
        /// </summary>
        private void ConfigureEditor()
        {
            Debug.Log("ConfigureEditor called!"); // デバッグログ追加
            
            McpConfigService configService = GetConfigService(_model.UI.SelectedEditorType);
            bool isServerRunning = McpServerController.IsServerRunning;
            int portToUse = isServerRunning ? McpServerController.ServerPort : _model.UI.CustomPort;

            configService.AutoConfigure(portToUse);
            UpdateAllUIElements();
        }

        /// <summary>
        /// Open configuration file
        /// </summary>
        private void OpenConfigurationFile()
        {
            McpEditorType editorType = _model.UI.SelectedEditorType;
            string editorName = GetEditorDisplayName(editorType);
            
            string configPath = UnityMcpPathResolver.GetConfigPath(editorType);
            if (System.IO.File.Exists(configPath))
            {
                UnityEditor.EditorUtility.OpenWithDefaultApp(configPath);
            }
            else
            {
                UnityEditor.EditorUtility.DisplayDialog(
                    "Configuration File Not Found",
                    $"Configuration file for {editorName} not found at:\n{configPath}\n\nPlease run 'Configure {editorName}' first to create the configuration file.",
                    "OK");
            }
        }

        // =============================================================================
        // Model Update Methods (migrated from OnGUI version)
        // =============================================================================

        /// <summary>
        /// Update custom port setting
        /// </summary>
        private void UpdateCustomPort(int port)
        {
            _model.UpdateCustomPort(port);
            UpdateServerControlsUI();
        }

        /// <summary>
        /// Update auto start server setting
        /// </summary>
        private void UpdateAutoStartServer(bool autoStart)
        {
            _model.UpdateAutoStartServer(autoStart);
        }

        /// <summary>
        /// Update show connected tools setting
        /// </summary>
        private void UpdateShowConnectedTools(bool show)
        {
            _model.UpdateShowConnectedTools(show);
        }

        /// <summary>
        /// Update show LLM tool settings
        /// </summary>
        private void UpdateShowLLMToolSettings(bool show)
        {
            _model.UpdateShowLLMToolSettings(show);
        }

        /// <summary>
        /// Update selected editor type
        /// </summary>
        private void UpdateSelectedEditorType(McpEditorType type)
        {
            _model.UpdateSelectedEditorType(type);
            UpdateEditorConfigUI();
        }

        /// <summary>
        /// Update show security settings
        /// </summary>
        private void UpdateShowSecuritySettings(bool show)
        {
            _model.UpdateShowSecuritySettings(show);
        }

        /// <summary>
        /// Update enable tests execution setting
        /// </summary>
        private void UpdateEnableTestsExecution(bool enable)
        {
            _model.UpdateEnableTestsExecution(enable);
        }

        /// <summary>
        /// Update allow menu item execution setting
        /// </summary>
        private void UpdateAllowMenuItemExecution(bool allow)
        {
            _model.UpdateAllowMenuItemExecution(allow);
        }

        /// <summary>
        /// Update allow third party tools setting
        /// </summary>
        private void UpdateAllowThirdPartyTools(bool allow)
        {
            _model.UpdateAllowThirdPartyTools(allow);
        }
    }
}