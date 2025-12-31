using UnityEngine.UIElements;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// UI section displaying server running status.
    /// Part of McpEditorWindowUI's component hierarchy.
    /// </summary>
    public class ServerStatusSection
    {
        private readonly Label _statusValueLabel;
        private ServerStatusData _lastData;

        public ServerStatusSection(VisualElement root)
        {
            _statusValueLabel = root.Q<Label>("status-value");
        }

        public void Update(ServerStatusData data)
        {
            if (_lastData != null && _lastData.Equals(data))
            {
                return;
            }

            _lastData = data;

            _statusValueLabel.text = data.Status;

            _statusValueLabel.RemoveFromClassList("mcp-status__value--running");
            _statusValueLabel.RemoveFromClassList("mcp-status__value--stopped");

            if (data.IsRunning)
            {
                _statusValueLabel.AddToClassList("mcp-status__value--running");
            }
            else
            {
                _statusValueLabel.AddToClassList("mcp-status__value--stopped");
            }
        }
    }
}
