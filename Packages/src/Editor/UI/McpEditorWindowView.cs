using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// View layer for McpEditorWindow - handles only UI rendering
    /// Related classes: McpEditorWindow (Presenter+Model), McpEditorWindowViewData
    /// Design document: ARCHITECTURE.md - UI layer separation pattern
    /// </summary>
    public class McpEditorWindowView
    {
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
            GUIStyle statusStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = data.StatusColor },
                fontStyle = FontStyle.Bold
            };
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Status:", EditorStyles.boldLabel, GUILayout.Width(50f));
            EditorGUILayout.LabelField($"{data.Status}", statusStyle, GUILayout.ExpandWidth(false));
            EditorGUILayout.EndHorizontal();
        }

        public void DrawServerControls(ServerControlsData data, Action toggleServerCallback, Action<bool> autoStartCallback, Action<int> portChangeCallback)
        {
            EditorGUILayout.BeginVertical("box");
            
            // Port settings
            EditorGUI.BeginDisabledGroup(data.IsServerRunning);
            int newPort = EditorGUILayout.IntField("Port:", data.CustomPort);
            if (newPort != data.CustomPort)
            {
                portChangeCallback?.Invoke(newPort);
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
            if (GUILayout.Button("Auto Start Server", EditorStyles.label, GUILayout.MinWidth(150f), GUILayout.ExpandWidth(true)))
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
            EditorGUILayout.BeginVertical("box");
            
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

        public void DrawEditorConfigSection(EditorConfigData data, Action<McpEditorType> editorChangeCallback, Action<string> configureCallback, Action<bool> foldoutCallback)
        {
            EditorGUILayout.BeginVertical("box");
            
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
                McpEditorType newSelectedEditor = (McpEditorType)EditorGUILayout.EnumPopup(data.SelectedEditor, GUILayout.ExpandWidth(true));
                EditorGUILayout.EndHorizontal();
                
                if (newSelectedEditor != data.SelectedEditor)
                {
                    editorChangeCallback?.Invoke(newSelectedEditor);
                }
                
                EditorGUILayout.Space();
                
                string editorName = GetEditorDisplayName(data.SelectedEditor);
                
                // Display configuration error if any
                if (!string.IsNullOrEmpty(data.ConfigurationError))
                {
                    EditorGUILayout.HelpBox($"Error loading {editorName} configuration: {data.ConfigurationError}", MessageType.Error);
                }
                
                EditorGUILayout.Space();
                
                // Determine button text based on configuration status
                string buttonText;
                if (data.IsConfigured)
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
                }
                else
                {
                    buttonText = $"Settings not found. \nConfigure {editorName}";
                }
                
                // Apply warning yellow color for unconfigured state or port mismatch
                Color originalColor = GUI.backgroundColor;
                if (!data.IsConfigured || data.HasPortMismatch)
                {
                    GUI.backgroundColor = new Color(1f, 0.9f, 0.4f); // Warning yellow
                }
                
                if (GUILayout.Button(buttonText, GUILayout.Height(data.IsServerRunning ? 40f : 25f)))
                {
                    configureCallback?.Invoke(editorName);
                }
                
                // Restore original color
                GUI.backgroundColor = originalColor;
                
                EditorGUILayout.Space();
                
                // Open settings file button
                if (GUILayout.Button($"Open {editorName} Settings File"))
                {
                    OpenConfigurationFile(data.SelectedEditor);
                }
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawConnectionStatus(ConnectedToolsData data)
        {
            if (!data.IsServerRunning)
            {
                EditorGUILayout.HelpBox("Server is not running. Start the server to see connected tools.", MessageType.Warning);
                return;
            }
            
            if (data.ShowReconnectingUI)
            {
                EditorGUILayout.HelpBox(McpUIConstants.RECONNECTING_MESSAGE, MessageType.Info);
            }
            else if (data.Clients != null && data.Clients.Count > 0)
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
            EditorGUILayout.BeginVertical("box");
            
            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.LabelField(McpUIConstants.CLIENT_ICON + client.ClientName, new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold }, GUILayout.ExpandWidth(true));
            
            EditorGUILayout.EndHorizontal();
            
            // Display endpoint information on a separate line to prevent horizontal overflow
            EditorGUILayout.BeginHorizontal();
            GUIStyle endpointStyle = new GUIStyle(EditorStyles.miniLabel);
            endpointStyle.normal.textColor = Color.gray;
            endpointStyle.wordWrap = true;
            EditorGUILayout.LabelField(McpUIConstants.ENDPOINT_ARROW + client.Endpoint, endpointStyle, GUILayout.ExpandWidth(true));
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
                McpEditorType.McpInspector => "MCP Inspector",
                _ => editorType.ToString()
            };
        }

        /// <summary>
        /// Open configuration file for the specified editor type
        /// </summary>
        private void OpenConfigurationFile(McpEditorType editorType)
        {
            try
            {
                string configPath = UnityMcpPathResolver.GetConfigPath(editorType);
                if (System.IO.File.Exists(configPath))
                {
                    UnityEditor.EditorUtility.OpenWithDefaultApp(configPath);
                }
                else
                {
                    UnityEditor.EditorUtility.DisplayDialog(
                        "Configuration File Not Found",
                        $"Configuration file for {GetEditorDisplayName(editorType)} not found at:\n{configPath}\n\nPlease run 'Configure {GetEditorDisplayName(editorType)}' first to create the configuration file.",
                        "OK");
                }
            }
            catch (System.Exception ex)
            {
                UnityEditor.EditorUtility.DisplayDialog(
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
            EditorGUILayout.BeginVertical("box");
            
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
    }
} 