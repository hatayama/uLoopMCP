using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using System;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Editor configuration component for UI Toolkit
    /// Handles LLM tool settings configuration
    /// </summary>
    public class EditorConfigView
    {
        private readonly Foldout _foldout;
        private readonly VisualElement _contentContainer;
        private EnumField _editorTypeField;
        private Button _configureButton;
        private Button _openSettingsButton;
        private VisualElement _errorBox;
        private Label _errorLabel;
        
        // Callbacks
        private Action<McpEditorType> _editorChangeCallback;
        private Action<string> _configureCallback;
        private Action<bool> _foldoutCallback;
        
        public EditorConfigView(Foldout foldout)
        {
            _foldout = foldout;
            _foldout.text = "LLM Tool Settings";
            
            // Get or create content container
            _contentContainer = _foldout.Q<VisualElement>("editor-config-content");
            if (_contentContainer == null)
            {
                _contentContainer = new();
                _contentContainer.name = "editor-config-content";
                _foldout.Add(_contentContainer);
            }
            
            BuildUI();
            
            // Register foldout change callback
            _foldout.RegisterValueChangedCallback(OnFoldoutChanged);
        }
        
        private void BuildUI()
        {
            _contentContainer.Clear();
            
            // Editor type selection row
            VisualElement editorRow = new();
            editorRow.AddToClassList("mcp-editor-config__row");
            
            Label targetLabel = new("Target:");
            targetLabel.AddToClassList("mcp-editor-config__label");
            editorRow.Add(targetLabel);
            
            _editorTypeField = new EnumField(McpEditorType.Cursor);
            _editorTypeField.AddToClassList("mcp-enum-field");
            _editorTypeField.RegisterValueChangedCallback(OnEditorTypeChanged);
            editorRow.Add(_editorTypeField);
            
            _contentContainer.Add(editorRow);
            
            // Error box (initially hidden)
            _errorBox = new();
            _errorBox.AddToClassList("mcp-helpbox");
            _errorBox.AddToClassList("mcp-helpbox--error");
            _errorBox.style.display = DisplayStyle.None;
            
            _errorLabel = new();
            _errorBox.Add(_errorLabel);
            _contentContainer.Add(_errorBox);
            
            // Configure button
            _configureButton = new Button(OnConfigureButtonClicked);
            _configureButton.AddToClassList("mcp-button");
            _configureButton.AddToClassList("mcp-button--primary");
            _contentContainer.Add(_configureButton);
            
            // Open settings file button
            _openSettingsButton = new Button(OnOpenSettingsButtonClicked);
            _openSettingsButton.AddToClassList("mcp-button");
            _openSettingsButton.AddToClassList("mcp-button--secondary");
            _contentContainer.Add(_openSettingsButton);
        }
        
        public void Update(EditorConfigData data, Action<McpEditorType> editorCallback,
            Action<string> configureCallback, Action<bool> foldoutCallback)
        {
            // Store callbacks
            _editorChangeCallback = editorCallback;
            _configureCallback = configureCallback;
            _foldoutCallback = foldoutCallback;
            
            // Update foldout state
            _foldout.SetValueWithoutNotify(data.ShowFoldout);
            
            if (!data.ShowFoldout)
            {
                return;
            }
            
            // Update editor type
            _editorTypeField.SetValueWithoutNotify(data.SelectedEditor);
            
            // Update error display
            if (!string.IsNullOrEmpty(data.ConfigurationError))
            {
                _errorLabel.text = $"Error loading {GetEditorDisplayName(data.SelectedEditor)} configuration: {data.ConfigurationError}";
                _errorBox.style.display = DisplayStyle.Flex;
            }
            else
            {
                _errorBox.style.display = DisplayStyle.None;
            }
            
            // Update configure button
            string buttonText;
            bool buttonEnabled = true;
            
            if (data.IsConfigured)
            {
                if (data.IsUpdateNeeded)
                {
                    if (data.HasPortMismatch)
                    {
                        buttonText = data.IsServerRunning ? 
                            $"Update {GetEditorDisplayName(data.SelectedEditor)} Settings\n(Port mismatch - Server: {data.CurrentPort})" : 
                            $"Update {GetEditorDisplayName(data.SelectedEditor)} Settings\n(Port mismatch)";
                    }
                    else
                    {
                        buttonText = data.IsServerRunning ? 
                            $"Update {GetEditorDisplayName(data.SelectedEditor)} Settings\n(Port {data.CurrentPort})" : 
                            $"Update {GetEditorDisplayName(data.SelectedEditor)} Settings";
                    }
                    buttonEnabled = true;
                }
                else
                {
                    buttonText = data.IsServerRunning ? 
                        $"Settings Already Configured\n(Port {data.CurrentPort})" : 
                        $"Settings Already Configured\n(Port {data.CurrentPort})";
                    buttonEnabled = false;
                }
            }
            else
            {
                buttonText = $"Settings not found.\nConfigure {GetEditorDisplayName(data.SelectedEditor)}";
                buttonEnabled = true;
            }
            
            _configureButton.text = buttonText;
            _configureButton.SetEnabled(buttonEnabled);
            
            // Update button styling based on state
            _configureButton.RemoveFromClassList("mcp-button--warning");
            _configureButton.RemoveFromClassList("mcp-button--disabled");
            
            if (!buttonEnabled)
            {
                _configureButton.style.backgroundColor = new StyleColor(new UnityEngine.Color(0.5f, 0.5f, 0.5f, 0.5f));
                _configureButton.style.color = StyleKeyword.Null;
            }
            else if (!data.IsConfigured || data.HasPortMismatch)
            {
                _configureButton.style.backgroundColor = new StyleColor(new UnityEngine.Color(1f, 0.9f, 0.4f));
                _configureButton.style.color = new StyleColor(UnityEngine.Color.black);
            }
            else if (data.IsUpdateNeeded)
            {
                _configureButton.style.backgroundColor = new StyleColor(new UnityEngine.Color(0.7f, 0.9f, 1f));
                _configureButton.style.color = StyleKeyword.Null;
            }
            else
            {
                _configureButton.style.backgroundColor = StyleKeyword.Null;
                _configureButton.style.color = StyleKeyword.Null;
            }
            
            // Adjust button height based on content
            if (buttonText.Contains("\n"))
            {
                _configureButton.style.minHeight = 40;
                _configureButton.style.height = StyleKeyword.Auto;
            }
            else
            {
                _configureButton.style.minHeight = 25;
                _configureButton.style.height = 25;
            }
            
            // Update open settings button
            _openSettingsButton.text = $"Open {GetEditorDisplayName(data.SelectedEditor)} Settings File";
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
        
        private void OnEditorTypeChanged(ChangeEvent<Enum> evt)
        {
            if (evt.newValue is McpEditorType editorType)
            {
                _editorChangeCallback?.Invoke(editorType);
            }
        }
        
        private void OnConfigureButtonClicked()
        {
            string editorName = GetEditorDisplayName((McpEditorType)_editorTypeField.value);
            _configureCallback?.Invoke(editorName);
        }
        
        private void OnOpenSettingsButtonClicked()
        {
            OpenConfigurationFile((McpEditorType)_editorTypeField.value);
        }
        
        private void OnFoldoutChanged(ChangeEvent<bool> evt)
        {
            _foldoutCallback?.Invoke(evt.newValue);
        }
        
        private void OpenConfigurationFile(McpEditorType editorType)
        {
            try
            {
                string configPath = UnityMcpPathResolver.GetConfigPath(editorType);
                if (System.IO.File.Exists(configPath))
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
    }
}