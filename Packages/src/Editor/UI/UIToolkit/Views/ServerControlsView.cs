using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Server controls component for UI Toolkit
    /// Handles port configuration, start/stop button, and auto-start setting
    /// </summary>
    public class ServerControlsView
    {
        // Element names for this view
        private const string ELEMENT_STATUS_LABEL = "status-label";
        private const string ELEMENT_PORT_FIELD = "port-field";
        private const string ELEMENT_PORT_WARNING_BOX = "port-warning-box";
        private const string ELEMENT_PORT_WARNING_LABEL = "port-warning-label";
        private const string ELEMENT_TOGGLE_BUTTON = "toggle-button";
        private const string ELEMENT_AUTO_START_TOGGLE = "auto-start-toggle";
        private const string ELEMENT_AUTO_START_LABEL = "auto-start-label";
        
        // CSS classes for this view
        private const string CLASS_TOGGLE_BUTTON_START = "mcp-server-controls__toggle-button--start";
        private const string CLASS_TOGGLE_BUTTON_STOP = "mcp-server-controls__toggle-button--stop";
        private readonly VisualElement _container;
        private Label _statusLabel;
        private IntegerField _portField;
        private Button _toggleButton;
        private Toggle _autoStartToggle;
        private Label _autoStartLabel;
        private VisualElement _portWarningBox;
        private Label _portWarningLabel;
        
        // Callbacks
        private Action _toggleCallback;
        private Action<bool> _autoStartCallback;
        private Action<int> _portCallback;
        
        public ServerControlsView(VisualElement container)
        {
            _container = container;
            BuildUI();
        }
        
        private void BuildUI()
        {
            // UXMLで定義された要素を取得（動的生成から変更）
            _statusLabel = _container.Q<Label>(ELEMENT_STATUS_LABEL);
            _portField = _container.Q<IntegerField>(ELEMENT_PORT_FIELD);
            _portWarningBox = _container.Q<VisualElement>(ELEMENT_PORT_WARNING_BOX);
            _portWarningLabel = _portWarningBox?.Q<Label>(ELEMENT_PORT_WARNING_LABEL);
            _toggleButton = _container.Q<Button>(ELEMENT_TOGGLE_BUTTON);
            _autoStartToggle = _container.Q<Toggle>(ELEMENT_AUTO_START_TOGGLE);
            _autoStartLabel = _container.Q<Label>(ELEMENT_AUTO_START_LABEL);
            
            // イベントハンドラーの登録
            if (_portField != null)
                _portField.RegisterValueChangedCallback(OnPortChanged);
                
            if (_toggleButton != null)
                _toggleButton.clicked += OnToggleButtonClicked;
                
            if (_autoStartToggle != null)
                _autoStartToggle.RegisterValueChangedCallback(OnAutoStartChanged);
                
            // ラベルクリックでトグルを切り替える機能
            if (_autoStartLabel != null)
            {
                _autoStartLabel.RegisterCallback<ClickEvent>(evt => {
                    if (_autoStartToggle != null)
                        _autoStartToggle.value = !_autoStartToggle.value;
                });
            }
        }
        
        public void Update(ServerControlsData data, Action toggleCallback, 
            Action<bool> autoStartCallback, Action<int> portCallback)
        {
            // Store callbacks
            _toggleCallback = toggleCallback;
            _autoStartCallback = autoStartCallback;
            _portCallback = portCallback;
            
            // Update status label
            string statusText = data.IsServerRunning ? 
                "Status: <b><color=#4CAF50>Running</color></b>" : 
                "Status: <b><color=#F44336>Stopped</color></b>";
            _statusLabel.text = statusText;
            
            // Update port field
            _portField.SetValueWithoutNotify(data.CustomPort);
            _portField.SetEnabled(data.PortEditable);
            
            // Update port warning
            if (data.HasPortWarning && !string.IsNullOrEmpty(data.PortWarningMessage))
            {
                _portWarningLabel.text = data.PortWarningMessage;
                _portWarningBox.style.display = DisplayStyle.Flex;
            }
            else
            {
                _portWarningBox.style.display = DisplayStyle.None;
            }
            
            // Update toggle button
            _toggleButton.text = data.IsServerRunning ? "Stop Server" : "Start Server";
            
            // Remove existing button classes
            _toggleButton.RemoveFromClassList(CLASS_TOGGLE_BUTTON_START);
            _toggleButton.RemoveFromClassList(CLASS_TOGGLE_BUTTON_STOP);
            
            // Add appropriate button class
            string buttonClass = data.IsServerRunning ?
                CLASS_TOGGLE_BUTTON_STOP :
                CLASS_TOGGLE_BUTTON_START;
            _toggleButton.AddToClassList(buttonClass);
            
            // Update auto start toggle
            _autoStartToggle.SetValueWithoutNotify(data.AutoStartServer);
        }
        
        private void OnPortChanged(ChangeEvent<int> evt)
        {
            _portCallback?.Invoke(evt.newValue);
        }
        
        private void OnToggleButtonClicked()
        {
            _toggleCallback?.Invoke();
        }
        
        private void OnAutoStartChanged(ChangeEvent<bool> evt)
        {
            _autoStartCallback?.Invoke(evt.newValue);
        }
    }
}