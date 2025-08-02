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
        
        public ServerStatusView(VisualElement container)
        {
            _container = container;
            BuildUI();
        }
        
        private void BuildUI()
        {
            _container.Clear();
            _container.AddToClassList("mcp-server-status");
            
            _statusLabel = new Label();
            _statusLabel.AddToClassList("mcp-server-status__label");
            _statusLabel.enableRichText = true;
            _container.Add(_statusLabel);
        }
        
        public void Update(ServerStatusData data)
        {
            // Use rich text to make the status value bold and colored
            string statusText = data.IsRunning ? 
                "Status: <b><color=#4CAF50>Running</color></b>" : 
                "Status: <b><color=#F44336>Stopped</color></b>";
            _statusLabel.text = statusText;
        }
    }
}