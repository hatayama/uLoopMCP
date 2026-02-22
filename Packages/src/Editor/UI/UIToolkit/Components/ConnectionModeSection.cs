using System;
using UnityEngine.UIElements;

namespace io.github.hatayama.uLoopMCP
{
    public class ConnectionModeSection
    {
        private readonly Foldout _foldout;
        private readonly Button _mcpTabButton;
        private readonly Button _cliTabButton;
        private ConnectionModeData _lastData;

        public event Action<ConnectionMode> OnModeChanged;
        public event Action<bool> OnFoldoutChanged;

        public ConnectionModeSection(VisualElement root)
        {
            _foldout = root.Q<Foldout>("configuration-foldout");
            _mcpTabButton = root.Q<Button>("mcp-tab-button");
            _cliTabButton = root.Q<Button>("cli-tab-button");

            _mcpTabButton.clicked += () => OnModeChanged?.Invoke(ConnectionMode.MCP);
            _cliTabButton.clicked += () => OnModeChanged?.Invoke(ConnectionMode.CLI);
            _foldout.RegisterValueChangedCallback(evt => OnFoldoutChanged?.Invoke(evt.newValue));
        }

        public void Update(ConnectionModeData data)
        {
            if (_lastData != null && _lastData.Equals(data))
            {
                return;
            }

            _lastData = data;

            bool isMcp = data.Mode == ConnectionMode.MCP;

            ViewDataBinder.ToggleClass(_mcpTabButton, "mcp-tab-button--active", isMcp);
            ViewDataBinder.ToggleClass(_cliTabButton, "mcp-tab-button--active", !isMcp);
        }

        public void UpdateFoldout(bool show)
        {
            ViewDataBinder.UpdateFoldout(_foldout, show);
        }
    }
}
