using System;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace io.github.hatayama.uLoopMCP
{
    public class ServerControlsSection
    {
        private readonly IntegerField _portField;
        private readonly VisualElement _portWarningContainer;
        private readonly Label _portWarningLabel;
        private readonly Button _toggleServerButton;
        private readonly Toggle _autoStartToggle;

        private ServerControlsData _lastData;

        public event Action OnToggleServer;
        public event Action<bool> OnAutoStartChanged;
        public event Action<int> OnPortChanged;

        public ServerControlsSection(VisualElement root)
        {
            _portField = root.Q<IntegerField>("port-field");
            _portWarningContainer = root.Q<VisualElement>("port-warning-container");
            _portWarningLabel = root.Q<Label>("port-warning-label");
            _toggleServerButton = root.Q<Button>("toggle-server-button");
            _autoStartToggle = root.Q<Toggle>("auto-start-toggle");

            SetupBindings();
        }

        private void SetupBindings()
        {
            _portField.RegisterValueChangedCallback(evt => OnPortChanged?.Invoke(evt.newValue));
            _toggleServerButton.clicked += () => OnToggleServer?.Invoke();
            _autoStartToggle.RegisterValueChangedCallback(evt => OnAutoStartChanged?.Invoke(evt.newValue));
        }

        public void Update(ServerControlsData data)
        {
            if (_lastData != null && _lastData.Equals(data))
            {
                return;
            }

            _lastData = data;

            ViewDataBinder.UpdateIntegerField(_portField, data.CustomPort);
            _portField.SetEnabled(!data.IsServerRunning);

            ViewDataBinder.UpdateToggle(_autoStartToggle, data.AutoStartServer);

            UpdatePortWarning(data);
            UpdateToggleButton(data);
        }

        private void UpdatePortWarning(ServerControlsData data)
        {
            bool showWarning = data.HasPortWarning && !string.IsNullOrEmpty(data.PortWarningMessage);
            ViewDataBinder.ToggleClass(_portWarningContainer, "mcp-warning-container--visible", showWarning);

            if (showWarning)
            {
                _portWarningLabel.text = data.PortWarningMessage;
            }
        }

        private void UpdateToggleButton(ServerControlsData data)
        {
            if (data.IsServerRunning)
            {
                _toggleServerButton.text = "Stop Server";
                _toggleServerButton.RemoveFromClassList("mcp-button--start");
                _toggleServerButton.AddToClassList("mcp-button--stop");
            }
            else
            {
                _toggleServerButton.text = "Start Server";
                _toggleServerButton.RemoveFromClassList("mcp-button--stop");
                _toggleServerButton.AddToClassList("mcp-button--start");
            }
        }
    }
}
