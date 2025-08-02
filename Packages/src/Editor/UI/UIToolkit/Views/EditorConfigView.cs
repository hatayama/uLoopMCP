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
            _contentContainer = _foldout.Q<VisualElement>(McpUIToolkitConstants.ELEMENT_EDITOR_CONFIG_CONTENT);
            if (_contentContainer == null)
            {
                _contentContainer = new();
                _contentContainer.name = McpUIToolkitConstants.ELEMENT_EDITOR_CONFIG_CONTENT;
                _foldout.Add(_contentContainer);
            }
            
            BuildUI();
            
            // Register foldout change callback
            _foldout.RegisterValueChangedCallback(OnFoldoutChanged);
        }
        
        private void BuildUI()
        {
            // UXMLで定義された要素を取得（動的生成から変更）
            _editorTypeField = _contentContainer.Q<EnumField>(McpUIToolkitConstants.ELEMENT_EDITOR_TYPE_FIELD);
            _errorBox = _contentContainer.Q<VisualElement>(McpUIToolkitConstants.ELEMENT_ERROR_BOX);
            _errorLabel = _errorBox?.Q<Label>(McpUIToolkitConstants.ELEMENT_ERROR_LABEL);
            _configureButton = _contentContainer.Q<Button>(McpUIToolkitConstants.ELEMENT_CONFIGURE_BUTTON);
            _openSettingsButton = _contentContainer.Q<Button>(McpUIToolkitConstants.ELEMENT_OPEN_SETTINGS_BUTTON);
            
            // EnumFieldの初期設定
            if (_editorTypeField != null)
            {
                _editorTypeField.Init(McpEditorType.Cursor);
                _editorTypeField.RegisterValueChangedCallback(OnEditorTypeChanged);
            }
            
            // イベントハンドラーの登録
            if (_configureButton != null)
                _configureButton.clicked += OnConfigureButtonClicked;
                
            if (_openSettingsButton != null)
                _openSettingsButton.clicked += OnOpenSettingsButtonClicked;
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
            _configureButton.RemoveFromClassList(McpUIToolkitConstants.CLASS_MCP_BUTTON_WARNING);
            _configureButton.RemoveFromClassList(McpUIToolkitConstants.CLASS_MCP_BUTTON_DISABLED);
            
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
                _configureButton.style.minHeight = McpUIToolkitConstants.BUTTON_HEIGHT_MULTILINE;
                _configureButton.style.height = StyleKeyword.Auto;
            }
            else
            {
                _configureButton.style.minHeight = McpUIToolkitConstants.BUTTON_HEIGHT_NORMAL;
                _configureButton.style.height = McpUIToolkitConstants.BUTTON_HEIGHT_NORMAL;
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