using UnityEngine.UIElements;
using System;
using System.Linq;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Connected tools display component for UI Toolkit
    /// Shows list of connected MCP clients in a foldout
    /// </summary>
    public class ConnectedToolsView
    {
        private readonly Foldout _foldout;
        private readonly VisualElement _contentContainer;
        private Action<bool> _foldoutCallback;
        
        public ConnectedToolsView(Foldout foldout)
        {
            _foldout = foldout;
            _foldout.text = McpUIConstants.CONNECTED_TOOLS_FOLDOUT_TEXT;
            
            // Get or create content container
            _contentContainer = _foldout.Q<VisualElement>("connected-tools-content");
            if (_contentContainer == null)
            {
                _contentContainer = new();
                _contentContainer.name = "connected-tools-content";
                _foldout.Add(_contentContainer);
            }
            
            // Register foldout change callback
            _foldout.RegisterValueChangedCallback(OnFoldoutChanged);
        }
        
        public void Update(ConnectedToolsData data, Action<bool> foldoutCallback)
        {
            _foldoutCallback = foldoutCallback;
            
            // Update foldout state
            _foldout.SetValueWithoutNotify(data.ShowFoldout);
            
            // Clear existing content
            _contentContainer.Clear();
            
            if (!data.ShowFoldout)
            {
                return;
            }
            
            // Show appropriate content based on server state
            if (!data.IsServerRunning)
            {
                ShowHelpBox("Server is not running. Start the server to see connected tools.", 
                    McpMessageType.Warning);
                return;
            }
            
            if (data.ShowReconnectingUI)
            {
                ShowHelpBox("Reconnecting to clients...", McpMessageType.Info);
                return;
            }
            
            if (data.Clients != null && data.Clients.Count > 0)
            {
                // Filter out clients with invalid names
                var validClients = data.Clients
                    .Where(client => IsValidClientName(client.ClientName))
                    .ToList();
                
                if (validClients.Count > 0)
                {
                    foreach (var client in validClients)
                    {
                        AddClientItem(client);
                    }
                }
                else
                {
                    ShowHelpBox("No connected tools found.", McpMessageType.Info);
                }
            }
            else
            {
                ShowHelpBox("No connected tools found.", McpMessageType.Info);
            }
        }
        
        private void AddClientItem(ConnectedClient client)
        {
            VisualElement clientItem = new();
            clientItem.AddToClassList("mcp-connected-tools__client-item");
            
            // Client icon
            Label iconLabel = new(McpUIConstants.CLIENT_ICON);
            iconLabel.AddToClassList("mcp-connected-tools__client-icon");
            clientItem.Add(iconLabel);
            
            // Client name
            Label nameLabel = new(client.ClientName);
            nameLabel.AddToClassList("mcp-connected-tools__client-name");
            clientItem.Add(nameLabel);
            
            // Client port
            Label portLabel = new($":{client.Port}");
            portLabel.AddToClassList("mcp-connected-tools__client-port");
            clientItem.Add(portLabel);
            
            _contentContainer.Add(clientItem);
        }
        
        private void ShowHelpBox(string message, McpMessageType type)
        {
            VisualElement helpBox = new();
            helpBox.AddToClassList("mcp-helpbox");
            
            switch (type)
            {
                case McpMessageType.Info:
                    helpBox.AddToClassList("mcp-helpbox--info");
                    break;
                case McpMessageType.Warning:
                    helpBox.AddToClassList("mcp-helpbox--warning");
                    break;
                case McpMessageType.Error:
                    helpBox.AddToClassList("mcp-helpbox--error");
                    break;
            }
            
            Label messageLabel = new(message);
            helpBox.Add(messageLabel);
            
            _contentContainer.Add(helpBox);
        }
        
        private bool IsValidClientName(string clientName)
        {
            return !string.IsNullOrEmpty(clientName) && 
                   clientName != McpConstants.UNKNOWN_CLIENT_NAME;
        }
        
        private void OnFoldoutChanged(ChangeEvent<bool> evt)
        {
            _foldoutCallback?.Invoke(evt.newValue);
        }
    }
    
    /// <summary>
    /// Message type for help boxes (matching Unity's MessageType)
    /// </summary>
    internal enum McpMessageType
    {
        Info,
        Warning,
        Error
    }
}