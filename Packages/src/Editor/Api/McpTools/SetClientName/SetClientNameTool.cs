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

        protected override async Task<SetClientNameResponse> ExecuteAsync(SetClientNameSchema parameters, CancellationToken cancellationToken)
        {
            string clientName = parameters.ClientName;
            int clientPort = parameters.ClientPort;
            string pushEndpoint = parameters.PushNotificationEndpoint;
            
            // Use MCP connection endpoint for client identification (not Push notification endpoint)
            string jsonRpcEndpoint = JsonRpcProcessor.CurrentClientContext?.Endpoint ?? "unknown";
            string actualEndpoint = GetActualClientEndpoint();
            string clientEndpoint = actualEndpoint;  // Use actual MCP connection endpoint
            
            UnityEngine.Debug.Log($"[uLoopMCP] SetClientName endpoint comparison - JsonRpc: {jsonRpcEndpoint}, Actual: {actualEndpoint}");
            
            UpdateClientNameInServer(clientName);
            
            // Debug log for push endpoint registration
            UnityEngine.Debug.Log($"[uLoopMCP] SetClientName - clientName: {clientName}, clientPort: {clientPort}, pushEndpoint: '{pushEndpoint}', clientEndpoint: {clientEndpoint}");
            
            // Save push notification endpoint if provided
            if (!string.IsNullOrEmpty(pushEndpoint))
            {
                McpSessionManager sessionManager = await McpSessionManager.GetSafeInstanceAsync();
                if (sessionManager != null)
                {
                    UnityEngine.Debug.Log($"[uLoopMCP] Registering push endpoint - clientName: {clientName}, clientEndpoint: {clientEndpoint}, pushEndpoint: {pushEndpoint}");
                    // Use actual client endpoint and client name to avoid overwriting
                    sessionManager.SetPushServerEndpoint(clientEndpoint, pushEndpoint, clientName);
                    UnityEngine.Debug.Log($"[uLoopMCP] Push endpoint registered successfully");
                }
                else
                {
                    UnityEngine.Debug.LogError("[uLoopMCP] Failed to access McpSessionManager instance safely");
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[uLoopMCP] Push endpoint is empty for client: {clientName}");
            }
            
            string message = string.Format(McpConstants.CLIENT_SUCCESS_MESSAGE_TEMPLATE, clientName);
            SetClientNameResponse response = new SetClientNameResponse(message, clientName, pushEndpoint);
            return response;
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

        /// <summary>
        /// Get the actual client endpoint from connected clients (consistent with McpBridgeServer)
        /// </summary>
        private string GetActualClientEndpoint()
        {
            McpBridgeServer server = McpServerController.CurrentServer;
            if (server == null) return "unknown";
            
            var connectedClients = server.GetConnectedClients();
            if (connectedClients.Count == 0) return "unknown";
            
            // Get current client context
            var clientContext = JsonRpcProcessor.CurrentClientContext;
            if (clientContext == null) return "unknown";
            
            // Find client by JsonRpcProcessor endpoint and return the actual ConnectedClient endpoint
            ConnectedClient targetClient = connectedClients
                .FirstOrDefault(c => c.Endpoint == clientContext.Endpoint);
            
            return targetClient?.Endpoint ?? "unknown";
        }
    }
}