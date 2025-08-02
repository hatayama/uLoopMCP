using UnityEngine.UIElements;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Server status display component for UI Toolkit
    /// Shows server running state and status text
    /// </summary>
    public class ServerStatusView
    {
        private readonly VisualElement _container;
        private Label _statusLabel;
        private Label _statusValue;
        
        public ServerStatusView(VisualElement container)
        {
            _container = container;
            BuildUI();
        }
        
        private void BuildUI()
        {
            _container.Clear();
            _container.AddToClassList("mcp-server-status");
            
            _statusLabel = new Label("Status:");
            _statusLabel.AddToClassList("mcp-server-status__label");
            _container.Add(_statusLabel);
            
            _statusValue = new Label();
            _statusValue.AddToClassList("mcp-server-status__value");
            _container.Add(_statusValue);
        }
        
        public void Update(ServerStatusData data)
        {
            _statusValue.text = data.Status;
            
            // Remove existing status classes
            _statusValue.RemoveFromClassList("mcp-server-status__value--running");
            _statusValue.RemoveFromClassList("mcp-server-status__value--stopped");
            
            // Add appropriate status class
            string statusClass = data.IsRunning ? 
                "mcp-server-status__value--running" : 
                "mcp-server-status__value--stopped";
            _statusValue.AddToClassList(statusClass);
        }
    }
}