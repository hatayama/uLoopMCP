using UnityEngine;
using UnityEditor;
using System;
using System.Linq;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// View layer for McpEditorWindow - handles only UI rendering
    /// Related classes: McpEditorWindow (Presenter+Model), McpEditorWindowViewData
    /// Design document: ARCHITECTURE.md - UI layer separation pattern
    /// </summary>
    public class McpEditorWindowView : IDisposable
    {
        private GUIStyle _sectionBoxStyle;
        private GUIStyle _clientItemBoxStyle;
        private Texture2D _sectionBackgroundTexture;
        private Texture2D _clientItemBackgroundTexture;

        private GUIStyle CreateSectionBoxStyle()
        {
            if (_sectionBoxStyle != null)
            {
                return _sectionBoxStyle;
            }

            _sectionBoxStyle = new GUIStyle(EditorStyles.helpBox);
            _sectionBackgroundTexture = new Texture2D(1, 1);
            _sectionBackgroundTexture.SetPixel(0, 0, McpUIConstants.SECTION_BACKGROUND_COLOR);
            _sectionBackgroundTexture.Apply();
            _sectionBoxStyle.normal.background = _sectionBackgroundTexture;
            _sectionBoxStyle.border = new RectOffset(4, 4, 4, 4);
            _sectionBoxStyle.padding = new RectOffset(8, 8, 8, 8);
            return _sectionBoxStyle;
        }

        private GUIStyle CreateClientItemBoxStyle()
        {
            if (_clientItemBoxStyle != null)
            {
                return _clientItemBoxStyle;
            }

            _clientItemBoxStyle = new GUIStyle(EditorStyles.helpBox);
            _clientItemBackgroundTexture = new Texture2D(1, 1);
            _clientItemBackgroundTexture.SetPixel(0, 0, McpUIConstants.CLIENT_ITEM_BACKGROUND_COLOR);
            _clientItemBackgroundTexture.Apply();
            _clientItemBoxStyle.normal.background = _clientItemBackgroundTexture;
            _clientItemBoxStyle.border = new RectOffset(4, 4, 4, 4);
            _clientItemBoxStyle.padding = new RectOffset(8, 8, 8, 8);
            return _clientItemBoxStyle;
        }
        /// <summary>
        /// Draw debug background if ULOOPMCP_DEBUG is defined
        /// </summary>
        public void DrawDebugBackground(Rect windowPosition)
        {
#if ULOOPMCP_DEBUG
            if (Event.current.type == EventType.Repaint)
            {
                // Draw background covering entire window
                EditorGUI.DrawRect(new Rect(0, 0, windowPosition.width, windowPosition.height), new Color(1f, 1f, 0.7f, 0.2f)); // Light yellow background
            }
#endif
        }
        public void DrawServerStatus(ServerStatusData data)
        {
            GUIStyle statusStyle = new (EditorStyles.label)
            {
                normal = { textColor = data.StatusColor },
                fontStyle = FontStyle.Bold
            };
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Status:", EditorStyles.boldLabel, GUILayout.Width(50f));
            EditorGUILayout.LabelField($"{data.Status}", statusStyle, GUILayout.MaxWidth(50f));
            EditorGUILayout.EndHorizontal();
        }

        public void DrawServerControls(ServerControlsData data, Action toggleServerCallback, Action<bool> autoStartCallback, Action<int> portChangeCallback)
        {
            EditorGUILayout.BeginVertical(CreateSectionBoxStyle());
            
            // Port settings
            EditorGUI.BeginDisabledGroup(data.IsServerRunning);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Port:", GUILayout.Width(30f));
            GUIStyle rightAlignedTextField = new (EditorStyles.textField)
            {
                alignment = TextAnchor.MiddleRight,
                contentOffset = new Vector2(0, 0)
            };
            int newPort = EditorGUILayout.IntField(data.CustomPort, rightAlignedTextField, GUILayout.Width(50f));
            EditorGUILayout.EndHorizontal();
            if (newPort != data.CustomPort)
            {
                try
                {
                    portChangeCallback?.Invoke(newPort);
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogError($"Port change failed: {ex.Message}");
                }
            }
            EditorGUI.EndDisabledGroup();
            
            // Port warning message
            if (data.HasPortWarning && !string.IsNullOrEmpty(data.PortWarningMessage))
            {
                EditorGUILayout.HelpBox(data.PortWarningMessage, MessageType.Warning);
            }
            
            EditorGUILayout.Space();
            
            // Toggle Server button
            string buttonText = data.IsServerRunning ? "Stop Server" : "Start Server";
            Color originalColor = GUI.backgroundColor;
            
            // Change button color based on server state
            if (data.IsServerRunning)
            {
                GUI.backgroundColor = new Color(1f, 0.7f, 0.7f); // Light red for stop
            }
            else
            {
                GUI.backgroundColor = new Color(0.7f, 1f, 0.7f); // Light green for start
            }
            
            if (GUILayout.Button(buttonText, GUILayout.Height(McpUIConstants.BUTTON_HEIGHT_LARGE)))
            {
                toggleServerCallback?.Invoke();
            }
            
            // Restore original color
            GUI.backgroundColor = originalColor;
            
            EditorGUILayout.Space();
            
            // Auto start checkbox
            EditorGUILayout.BeginHorizontal();
            bool newAutoStart = EditorGUILayout.Toggle(data.AutoStartServer, GUILayout.Width(20));
            if (GUILayout.Button("Auto Start Server", EditorStyles.label, GUILayout.MinWidth(100f), GUILayout.ExpandWidth(true)))
            {
                newAutoStart = !data.AutoStartServer;
            }
            
            if (newAutoStart != data.AutoStartServer)
            {
                autoStartCallback?.Invoke(newAutoStart);
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        public void DrawConnectedToolsSection(ConnectedToolsData data, Action<bool> toggleFoldoutCallback)
        {
            EditorGUILayout.BeginVertical(CreateSectionBoxStyle());
            
            bool newShowFoldout = EditorGUILayout.Foldout(data.ShowFoldout, McpUIConstants.CONNECTED_TOOLS_FOLDOUT_TEXT, true);
            if (newShowFoldout != data.ShowFoldout)
            {
                toggleFoldoutCallback?.Invoke(newShowFoldout);
            }
            
            if (data.ShowFoldout)
            {
                EditorGUILayout.Space();
                DrawConnectionStatus(data);
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        public void DrawEditorConfigSection(EditorConfigData data, Action<McpEditorType> editorChangeCallback, Action<string> configureCallback, Action<bool> foldoutCallback, Action<bool> repositoryRootToggleCallback)
        {
            EditorGUILayout.BeginVertical(CreateSectionBoxStyle());
            
            bool showFoldout = EditorGUILayout.Foldout(data.ShowFoldout, "LLM Tool Settings", true);
            if (showFoldout != data.ShowFoldout)
            {
                foldoutCallback?.Invoke(showFoldout);
            }
            
            if (showFoldout)
            {
                EditorGUILayout.Space();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Target:", GUILayout.Width(50f));
                bool hasSupportedEditors = TryDrawEditorSelectionFilteringCodexForNonMac(data.SelectedEditor, editorChangeCallback);
                EditorGUILayout.EndHorizontal();

                if (!hasSupportedEditors)
                {
                    EditorGUILayout.EndVertical();
                    return;
                }
                
                EditorGUILayout.Space();

                if (data.SupportsRepositoryRootToggle && data.ShowRepositoryRootToggle)
                {
                    EditorGUILayout.BeginHorizontal();
                    bool newAddRepositoryRoot = EditorGUILayout.Toggle(data.AddRepositoryRoot, GUILayout.Width(20));
                    GUIContent toggleLabel = new GUIContent("Use Repository Root", "ON にすると Git リポジトリルートで設定ファイルを生成します");
                    if (GUILayout.Button(toggleLabel, EditorStyles.label, GUILayout.MinWidth(150f), GUILayout.ExpandWidth(true)))
                    {
                        newAddRepositoryRoot = !data.AddRepositoryRoot;
                    }

                    if (newAddRepositoryRoot != data.AddRepositoryRoot)
                    {
                        repositoryRootToggleCallback?.Invoke(newAddRepositoryRoot);
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space();
                }
                
                string editorName = GetEditorDisplayName(data.SelectedEditor);
                
                // Display configuration error if any
                if (!string.IsNullOrEmpty(data.ConfigurationError))
                {
                    EditorGUILayout.HelpBox($"Error loading {editorName} configuration: {data.ConfigurationError}", MessageType.Error);
                }
                
                EditorGUILayout.Space();
                
                // Determine button text and state based on configuration status
                string buttonText;
                bool buttonEnabled = true;
                
                if (data.IsConfigured)
                {
                    if (data.IsUpdateNeeded)
                    {
                        if (data.HasPortMismatch)
                        {
                            buttonText = data.IsServerRunning ? 
                                $"Update {editorName} Settings\n(Port mismatch - Server: {data.CurrentPort})" : 
                                $"Update {editorName} Settings\n(Port mismatch)";
                        }
                        else
                        {
                            buttonText = data.IsServerRunning ? $"Update {editorName} Settings\n(Port {data.CurrentPort})" : $"Update {editorName} Settings";
                        }
                        buttonEnabled = true;
                    }
                    else
                    {
                        // Settings are already up to date
                        buttonText = data.IsServerRunning ? 
                            $"Settings Already Configured\n(Port {data.CurrentPort})" : 
                            $"Settings Already Configured\n(Port {data.CurrentPort})";
                        buttonEnabled = false;
                    }
                }
                else
                {
                    buttonText = $"Settings not found. \nConfigure {editorName}";
                    buttonEnabled = true;
                }
                
                // Apply colors based on button state
                Color originalColor = GUI.backgroundColor;
                if (!buttonEnabled)
                {
                    GUI.backgroundColor = new Color(0.8f, 0.8f, 0.8f); // Gray for disabled/up-to-date
                }
                else if (!data.IsConfigured)
                {
                    GUI.backgroundColor = new Color(1f, 0.9f, 0.4f); // Warning yellow for unconfigured
                }
                else if (data.HasPortMismatch)
                {
                    GUI.backgroundColor = new Color(1f, 0.9f, 0.4f); // Warning yellow for port mismatch
                }
                else if (data.IsUpdateNeeded)
                {
                    GUI.backgroundColor = new Color(0.7f, 0.9f, 1f); // Light blue for update needed
                }
                
                EditorGUI.BeginDisabledGroup(!buttonEnabled);
                // Calculate button height based on text content (2 lines need more height)
                float buttonHeight = buttonText.Contains('\n') ? 40f : 25f;
                if (GUILayout.Button(buttonText, GUILayout.Height(buttonHeight)))
                {
                    configureCallback?.Invoke(editorName);
                }
                EditorGUI.EndDisabledGroup();
                
                // Restore original color
                GUI.backgroundColor = originalColor;
                
                EditorGUILayout.Space();
                
                // Open settings file button
                if (GUILayout.Button($"Open {editorName} Settings File"))
                {
                    OpenConfigurationFile(data.SelectedEditor, data.AddRepositoryRoot);
                }
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        /// <summary>
        /// Draws the editor selection popup and filters Codex to macOS only.
        /// </summary>
        private bool TryDrawEditorSelectionFilteringCodexForNonMac(McpEditorType currentEditor, Action<McpEditorType> editorChangeCallback)
        {
            McpEditorType[] availableEditors = Enum.GetValues(typeof(McpEditorType)).Cast<McpEditorType>().ToArray();
            if (Application.platform != RuntimePlatform.OSXEditor)
            {
                availableEditors = availableEditors.Where(editor => editor != McpEditorType.Codex).ToArray();
            }

            if (availableEditors.Length == 0)
            {
                EditorGUILayout.LabelField("No supported editors", GUILayout.ExpandWidth(true));
                return false;
            }

            string[] optionLabels = availableEditors.Select(GetEditorDisplayName).ToArray();
            int currentIndex = Array.IndexOf(availableEditors, currentEditor);
            bool selectionAdjusted = false;
            if (currentIndex < 0)
            {
                currentIndex = 0;
                selectionAdjusted = true;
            }

            int selectedIndex = EditorGUILayout.Popup(currentIndex, optionLabels, GUILayout.ExpandWidth(true));
            McpEditorType selectedEditor = availableEditors[selectedIndex];

            if (selectionAdjusted || selectedEditor != currentEditor)
            {
                editorChangeCallback?.Invoke(selectedEditor);
            }

            return true;
        }

        private void DrawConnectionStatus(ConnectedToolsData data)
        {
            if (!data.IsServerRunning)
            {
                EditorGUILayout.HelpBox("Server is not running. Start the server to see connected tools.", MessageType.Warning);
                return;
            }
            
            if (data.Clients != null && data.Clients.Count > 0)
            {
                // Filter out clients with default or unknown names
                var validClients = data.Clients.Where(client => IsValidClientName(client.ClientName)).ToList();
                
                if (validClients.Count > 0)
                {
                    foreach (ConnectedClient client in validClients)
                    {
                        DrawConnectedClientItem(client);
                    }
                }
                else
                {
                    // All clients have invalid names, show as if no clients
                    EditorGUILayout.HelpBox("No connected tools found.", MessageType.Info);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No connected tools found.", MessageType.Info);
            }
        }

        private void DrawConnectedClientItem(ConnectedClient client)
        {
            EditorGUILayout.BeginVertical(CreateClientItemBoxStyle());
            
            EditorGUILayout.BeginHorizontal();
            
            string displayText = $"{McpUIConstants.CLIENT_ICON}<b>{client.ClientName}</b>  <color=#888888>:{client.Port}</color>";
            
            GUIStyle richTextStyle = new (EditorStyles.label);
            richTextStyle.richText = true;
            EditorGUILayout.LabelField(displayText, richTextStyle, GUILayout.ExpandWidth(true));
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(McpUIConstants.CLIENT_ITEM_SPACING);
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

        /// <summary>
        /// Open configuration file for the specified editor type
        /// </summary>
        private void OpenConfigurationFile(McpEditorType editorType, bool addRepositoryRoot)
        {
            try
            {
                bool useRepositoryRoot = McpEditorSettings.GetAddRepositoryRoot();
                string projectRoot = UnityMcpPathResolver.GetProjectRoot();
                string gitRoot = UnityMcpPathResolver.GetGitRepositoryRoot();
                string baseRoot = useRepositoryRoot
                    ? (gitRoot ?? projectRoot)
                    : projectRoot;

                string configPath = UnityMcpPathResolver.GetConfigPathForRoot(editorType, baseRoot);
                bool exists = System.IO.File.Exists(configPath);

                if (exists)
                {
                    EditorUtility.OpenWithDefaultApp(configPath);
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "Configuration File Not Found",
                        $"Configuration file for {GetEditorDisplayName(editorType)} not found at:\n{configPath}\n\nPlease run 'Configure {GetEditorDisplayName(editorType)}' first to create the configuration file.",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog(
                    "Error Opening Configuration File",
                    $"Failed to open configuration file: {ex.Message}",
                    "OK");
            }
        }

        /// <summary>
        /// Draw security settings section
        /// </summary>
        public void DrawSecuritySettings(SecuritySettingsData data, Action<bool> foldoutCallback, Action<bool> enableTestsCallback, Action<bool> allowMenuCallback, Action<bool> allowThirdPartyCallback)
        {
            EditorGUILayout.BeginVertical(CreateSectionBoxStyle());
            
            bool showFoldout = EditorGUILayout.Foldout(data.ShowSecuritySettings, "Security Settings", true);
            if (showFoldout != data.ShowSecuritySettings)
            {
                foldoutCallback?.Invoke(showFoldout);
            }
            
            if (showFoldout)
            {
                EditorGUILayout.Space();
                
                // Security warning using Unity's HelpBox with error type for red text
                EditorGUILayout.HelpBox("These settings control dangerous MCP operations. Only enable if you trust the AI system.\n\nFor safer operation, consider using sandbox environments or containers.\n\nChanges take effect immediately - no server restart required.", MessageType.Error);
                
                EditorGUILayout.Space();
                
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Additional Security Options", EditorStyles.boldLabel);
                
                // Create red label style for dangerous options
                GUIStyle redLabelStyle = new GUIStyle(EditorStyles.label);
                redLabelStyle.normal.textColor = Color.red;
                
                // Enable Tests Execution
                EditorGUILayout.BeginHorizontal();
                bool newEnableTests = EditorGUILayout.Toggle(data.EnableTestsExecution, GUILayout.Width(20));
                if (GUILayout.Button("Allow Tests Execution", redLabelStyle, GUILayout.MinWidth(150f), GUILayout.ExpandWidth(true)))
                {
                    newEnableTests = !data.EnableTestsExecution;
                }
                if (newEnableTests != data.EnableTestsExecution)
                {
                    enableTestsCallback?.Invoke(newEnableTests);
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(2);
                
                // Allow Menu Item Execution
                EditorGUILayout.BeginHorizontal();
                bool newAllowMenu = EditorGUILayout.Toggle(data.AllowMenuItemExecution, GUILayout.Width(20));
                if (GUILayout.Button("Allow Menu Item Execution", redLabelStyle, GUILayout.MinWidth(150f), GUILayout.ExpandWidth(true)))
                {
                    newAllowMenu = !data.AllowMenuItemExecution;
                }
                if (newAllowMenu != data.AllowMenuItemExecution)
                {
                    allowMenuCallback?.Invoke(newAllowMenu);
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(2);
                
                // Allow Third Party Tools
                EditorGUILayout.BeginHorizontal();
                bool newAllowThirdParty = EditorGUILayout.Toggle(data.AllowThirdPartyTools, GUILayout.Width(20));
                if (GUILayout.Button("Allow Third Party Tools", redLabelStyle, GUILayout.MinWidth(150f), GUILayout.ExpandWidth(true)))
                {
                    newAllowThirdParty = !data.AllowThirdPartyTools;
                }
                if (newAllowThirdParty != data.AllowThirdPartyTools)
                {
                    allowThirdPartyCallback?.Invoke(newAllowThirdParty);
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(2);
                
                EditorGUILayout.Space(10);
                
                // Dynamic Code Security Level - moved to bottom with red label
                EditorGUILayout.LabelField("Dynamic Code Security Level", redLabelStyle);
                DynamicCodeSecurityLevel currentLevel = McpEditorSettings.GetDynamicCodeSecurityLevel();
                DynamicCodeSecurityLevel newLevel = (DynamicCodeSecurityLevel)EditorGUILayout.EnumPopup("Security Level", currentLevel);
                if (newLevel != currentLevel)
                {
                    // Show confirmation dialog when enabling Roslyn features
                    if (currentLevel == DynamicCodeSecurityLevel.Disabled && newLevel != DynamicCodeSecurityLevel.Disabled)
                    {
                        bool roslynAvailable = RoslynAssemblyChecker.IsRoslynAvailable();
                        
                        if (!roslynAvailable)
                        {
                            // If Roslyn is not installed
                            EditorUtility.DisplayDialog(
                                "Roslyn Not Installed",
                                RoslynAssemblyChecker.GetInstallationMessage(),
                                "OK"
                            );
                            return; // Cancel security level change
                        }
                        else
                        {
                            // If Roslyn is already installed
                            string version = RoslynAssemblyChecker.GetRoslynVersion();
                            bool confirmed = EditorUtility.DisplayDialog(
                                "Enable Roslyn Features",
                                $"Microsoft.CodeAnalysis.CSharp is installed (version: {version}).\n\n" +
                                "This will enable advanced code analysis and execution features.\n\n" +
                                "Continue?",
                                "Enable",
                                "Cancel"
                            );
                            
                            if (confirmed)
                            {
                                McpEditorSettings.SetDynamicCodeSecurityLevel(newLevel);
                            }
                        }
                    }
                    else
                    {
                        McpEditorSettings.SetDynamicCodeSecurityLevel(newLevel);
                    }
                }
                
                // Security level description
                string levelDescription = newLevel switch
                {
                    DynamicCodeSecurityLevel.Disabled => "Level 0: Code execution completely disabled (safest)",
                    DynamicCodeSecurityLevel.Restricted => "Level 1: Dangerous APIs blocked (recommended)",
                    DynamicCodeSecurityLevel.FullAccess => "Level 2: All APIs available (use with caution)",
                    _ => "Unknown level"
                };
                
                // Choose appropriate MessageType based on security level
                MessageType messageType = newLevel switch
                {
                    DynamicCodeSecurityLevel.Disabled => MessageType.Info,      // Blue info icon for disabled (safe)
                    DynamicCodeSecurityLevel.Restricted => MessageType.Info,    // Blue info icon for restricted (recommended)
                    DynamicCodeSecurityLevel.FullAccess => MessageType.Warning, // Yellow warning icon for full access (dangerous)
                    _ => MessageType.Info
                };
                
                EditorGUILayout.HelpBox(levelDescription, messageType);
                
                EditorGUILayout.Space();
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        /// <summary>
        /// Check if client name is valid for display
        /// </summary>
        private bool IsValidClientName(string clientName)
        {
            // Only show clients with properly set names
            // Filter out empty names and the default "Unknown Client" placeholder
            return !string.IsNullOrEmpty(clientName) && clientName != McpConstants.UNKNOWN_CLIENT_NAME;
        }

        public void Dispose()
        {
            if (_sectionBackgroundTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(_sectionBackgroundTexture);
                _sectionBackgroundTexture = null;
            }

            if (_clientItemBackgroundTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(_clientItemBackgroundTexture);
                _clientItemBackgroundTexture = null;
            }

            _sectionBoxStyle = null;
            _clientItemBoxStyle = null;
        }
    }
} 
