using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// SetClientName tool handler - Allows MCP clients to register their name
    /// This tool is called by TypeScript clients to identify themselves
    /// </summary>
    [McpTool(
        Description = "Register client name for identification in Unity MCP server",
        DisplayDevelopmentOnly = true
    )]
    public class SetClientNameTool : AbstractUnityTool<SetClientNameSchema, SetClientNameResponse>
    {
        public override string ToolName => "set-client-name";

        protected override Task<SetClientNameResponse> ExecuteAsync(SetClientNameSchema parameters, CancellationToken cancellationToken)
        {
            string clientName = parameters.ClientName;
            int clientPort = parameters.ClientPort;
            string pushEndpoint = parameters.PushNotificationEndpoint;
            
            // Get actual client endpoint from connection context
            string clientEndpoint = JsonRpcProcessor.CurrentClientContext?.Endpoint ?? "unknown";
            
            UpdateClientNameInServer(clientName);
            
            // Save push notification endpoint if provided
            if (!string.IsNullOrEmpty(pushEndpoint))
            {
                McpSessionManager sessionManager = McpSessionManager.GetSafeInstance();
                if (sessionManager != null)
                {
                    sessionManager.SetPushServerEndpoint(clientEndpoint, pushEndpoint);
                }
                else
                {
                    UnityEngine.Debug.LogError("[uLoopMCP] Failed to access McpSessionManager instance safely");
                }
            }
            
            string message = string.Format(McpConstants.CLIENT_SUCCESS_MESSAGE_TEMPLATE, clientName);
            SetClientNameResponse response = new SetClientNameResponse(message, clientName, pushEndpoint);
            return Task.FromResult(response);
        }
        
        private void UpdateClientNameInServer(string clientName)
        {
            McpBridgeServer server = McpServerController.CurrentServer;
            if (server == null) return;
            
            var connectedClients = server.GetConnectedClients();
            if (connectedClients.Count == 0) return;
            
            // Get current client context
            var clientContext = JsonRpcProcessor.CurrentClientContext;
            if (clientContext == null)
            {
                return;
            }
            
            
            // Find client by endpoint
            ConnectedClient targetClient = connectedClients
                .FirstOrDefault(c => c.Endpoint == clientContext.Endpoint);
            
            if (targetClient != null)
            {
                server.UpdateClientName(targetClient.Endpoint, clientName);
                return;
            }
        }
    }
}