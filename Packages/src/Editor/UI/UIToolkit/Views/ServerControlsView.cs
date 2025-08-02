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
            _container.Clear();
            _container.AddToClassList("mcp-server-controls");
            
            // Status and Port row
            VisualElement statusPortRow = new();
            statusPortRow.AddToClassList("mcp-server-controls__status-port-row");
            
            // Status label
            _statusLabel = new Label();
            _statusLabel.AddToClassList("mcp-server-controls__status-label");
            _statusLabel.enableRichText = true;
            statusPortRow.Add(_statusLabel);
            
            // Port label
            Label portLabel = new("Port:");
            portLabel.AddToClassList("mcp-server-controls__port-label");
            statusPortRow.Add(portLabel);
            
            _portField = new IntegerField();
            _portField.AddToClassList("mcp-server-controls__port-field");
            _portField.RegisterValueChangedCallback(OnPortChanged);
            statusPortRow.Add(_portField);
            
            _container.Add(statusPortRow);
            
            // Port warning message
            _portWarningBox = new();
            _portWarningBox.AddToClassList("mcp-helpbox");
            _portWarningBox.AddToClassList("mcp-helpbox--warning");
            _portWarningBox.style.display = DisplayStyle.None;
            
            _portWarningLabel = new();
            _portWarningBox.Add(_portWarningLabel);
            _container.Add(_portWarningBox);
            
            // Toggle server button
            _toggleButton = new Button(OnToggleButtonClicked);
            _toggleButton.AddToClassList("mcp-server-controls__toggle-button");
            _container.Add(_toggleButton);
            
            // Auto start checkbox row
            VisualElement autoStartRow = new();
            autoStartRow.AddToClassList("mcp-server-controls__auto-start-row");
            
            _autoStartToggle = new Toggle();
            _autoStartToggle.AddToClassList("mcp-server-controls__auto-start-toggle");
            _autoStartToggle.RegisterValueChangedCallback(OnAutoStartChanged);
            autoStartRow.Add(_autoStartToggle);
            
            _autoStartLabel = new Label("Auto Start Server");
            _autoStartLabel.AddToClassList("mcp-server-controls__auto-start-label");
            _autoStartLabel.RegisterCallback<ClickEvent>(evt => {
                _autoStartToggle.value = !_autoStartToggle.value;
            });
            autoStartRow.Add(_autoStartLabel);
            
            _container.Add(autoStartRow);
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
            _toggleButton.RemoveFromClassList("mcp-server-controls__toggle-button--start");
            _toggleButton.RemoveFromClassList("mcp-server-controls__toggle-button--stop");
            
            // Add appropriate button class
            string buttonClass = data.IsServerRunning ?
                "mcp-server-controls__toggle-button--stop" :
                "mcp-server-controls__toggle-button--start";
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