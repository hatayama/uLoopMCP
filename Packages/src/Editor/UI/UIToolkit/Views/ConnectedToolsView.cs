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
            _contentContainer = _foldout.Q<VisualElement>(McpUIToolkitConstants.ELEMENT_CONNECTED_TOOLS_CONTENT);
            if (_contentContainer == null)
            {
                _contentContainer = new();
                _contentContainer.name = McpUIToolkitConstants.ELEMENT_CONNECTED_TOOLS_CONTENT;
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
            clientItem.AddToClassList(McpUIToolkitConstants.CLASS_MCP_CONNECTED_TOOLS_CLIENT_ITEM);
            
            // Client icon
            Label iconLabel = new(McpUIConstants.CLIENT_ICON);
            iconLabel.AddToClassList(McpUIToolkitConstants.CLASS_MCP_CONNECTED_TOOLS_CLIENT_ICON);
            clientItem.Add(iconLabel);
            
            // Client name
            Label nameLabel = new(client.ClientName);
            nameLabel.AddToClassList(McpUIToolkitConstants.CLASS_MCP_CONNECTED_TOOLS_CLIENT_NAME);
            clientItem.Add(nameLabel);
            
            // Client port
            Label portLabel = new($":{client.Port}");
            portLabel.AddToClassList(McpUIToolkitConstants.CLASS_MCP_CONNECTED_TOOLS_CLIENT_PORT);
            clientItem.Add(portLabel);
            
            _contentContainer.Add(clientItem);
        }
        
        private void ShowHelpBox(string message, McpMessageType type)
        {
            VisualElement helpBox = new();
            helpBox.AddToClassList(McpUIToolkitConstants.CLASS_MCP_HELPBOX);
            
            switch (type)
            {
                case McpMessageType.Info:
                    helpBox.AddToClassList(McpUIToolkitConstants.CLASS_MCP_HELPBOX_INFO);
                    break;
                case McpMessageType.Warning:
                    helpBox.AddToClassList(McpUIToolkitConstants.CLASS_MCP_HELPBOX_WARNING);
                    break;
                case McpMessageType.Error:
                    helpBox.AddToClassList(McpUIToolkitConstants.CLASS_MCP_HELPBOX_ERROR);
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