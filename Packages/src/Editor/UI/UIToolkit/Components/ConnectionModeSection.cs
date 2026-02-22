using System;
using UnityEngine.UIElements;

namespace io.github.hatayama.uLoopMCP
{
    public class ConnectionModeSection
    {
        private readonly Button _mcpModeButton;
        private readonly Button _cliModeButton;
        private ConnectionModeData _lastData;

        public event Action<ConnectionMode> OnModeChanged;

        public ConnectionModeSection(VisualElement root)
        {
            _mcpModeButton = root.Q<Button>("mcp-mode-button");
            _cliModeButton = root.Q<Button>("cli-mode-button");

            _mcpModeButton.clicked += () => OnModeChanged?.Invoke(ConnectionMode.MCP);
            _cliModeButton.clicked += () => OnModeChanged?.Invoke(ConnectionMode.CLI);
        }

        public void Update(ConnectionModeData data)
        {
            if (_lastData != null && _lastData.Equals(data))
            {
                return;
            }

            _lastData = data;

            bool isMcp = data.Mode == ConnectionMode.MCP;

            ViewDataBinder.ToggleClass(_mcpModeButton, "mcp-mode-button--active", isMcp);
            ViewDataBinder.ToggleClass(_cliModeButton, "mcp-mode-button--active", !isMcp);
        }
    }
}
