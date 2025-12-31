using System;
using System.Linq;
using UnityEngine.UIElements;

namespace io.github.hatayama.uLoopMCP
{
    public class ConnectedToolsSection
    {
        private readonly Foldout _foldout;
        private readonly VisualElement _clientsList;
        private readonly Label _noClientsMessage;
        private readonly Label _serverNotRunningMessage;

        private bool _hasLastValidData;
        private ConnectedToolsData _lastData;

        public event Action<bool> OnFoldoutChanged;

        public ConnectedToolsSection(VisualElement root)
        {
            _foldout = root.Q<Foldout>("connected-tools-foldout");
            _clientsList = root.Q<VisualElement>("clients-list");
            _noClientsMessage = root.Q<Label>("no-clients-message");
            _serverNotRunningMessage = root.Q<Label>("server-not-running-message");

            SetupBindings();
        }

        private void SetupBindings()
        {
            _foldout.RegisterValueChangedCallback(evt => OnFoldoutChanged?.Invoke(evt.newValue));
        }

        public void Update(ConnectedToolsData data)
        {
            if (data.ShowReconnectingUI && _hasLastValidData)
            {
                return;
            }

            ViewDataBinder.UpdateFoldout(_foldout, data.ShowFoldout);

            UpdateClientsList(data);

            _lastData = data;
        }

        private void UpdateClientsList(ConnectedToolsData data)
        {
            _clientsList.Clear();

            bool hasValidClients = false;

            if (!data.IsServerRunning)
            {
                ViewDataBinder.SetVisible(_serverNotRunningMessage, true);
                ViewDataBinder.SetVisible(_noClientsMessage, false);
                ViewDataBinder.SetVisible(_clientsList, false);
                _hasLastValidData = false;
                return;
            }

            ViewDataBinder.SetVisible(_serverNotRunningMessage, false);

            if (data.Clients != null && data.Clients.Count > 0)
            {
                System.Collections.Generic.List<ConnectedClient> validClients = data.Clients
                    .Where(client => IsValidClientName(client.ClientName))
                    .ToList();

                if (validClients.Count > 0)
                {
                    hasValidClients = true;
                    foreach (ConnectedClient client in validClients)
                    {
                        VisualElement clientItem = CreateClientItem(client);
                        _clientsList.Add(clientItem);
                    }
                }
            }

            ViewDataBinder.SetVisible(_clientsList, hasValidClients);
            ViewDataBinder.SetVisible(_noClientsMessage, !hasValidClients);

            _hasLastValidData = hasValidClients;
        }

        private VisualElement CreateClientItem(ConnectedClient client)
        {
            VisualElement container = new VisualElement();
            container.AddToClassList("mcp-client");

            Label iconLabel = new Label(McpUIConstants.CLIENT_ICON);
            iconLabel.AddToClassList("mcp-client__icon");
            container.Add(iconLabel);

            Label nameLabel = new Label(client.ClientName);
            nameLabel.AddToClassList("mcp-client__name");
            container.Add(nameLabel);

            Label portLabel = new Label($":{client.Port}");
            portLabel.AddToClassList("mcp-client__port");
            container.Add(portLabel);

            return container;
        }

        private bool IsValidClientName(string clientName)
        {
            return !string.IsNullOrEmpty(clientName) && clientName != McpConstants.UNKNOWN_CLIENT_NAME;
        }
    }
}
