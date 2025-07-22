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
            
            // DEBUG: Log all setClientName calls with detailed info
            UnityEngine.Debug.Log($"[uLoopMCP] [DEBUG] SetClientName CALLED: ClientName='{clientName}', ClientPort={clientPort}, ClientEndpoint='{clientEndpoint}', PushEndpoint='{pushEndpoint}' (Length: {pushEndpoint?.Length ?? -1}, IsNullOrEmpty: {string.IsNullOrEmpty(pushEndpoint)})");
            
            UpdateClientNameInServer(clientName);
            
            // Save push notification endpoint if provided
            if (!string.IsNullOrEmpty(pushEndpoint))
            {
                UnityEngine.Debug.Log($"[uLoopMCP] [DEBUG] SetClientName: Saving push notification endpoint for client '{clientEndpoint}': {pushEndpoint}");
                McpSessionManager sessionManager = McpSessionManager.GetSafeInstance();
                if (sessionManager != null)
                {
                    sessionManager.SetPushServerEndpoint(clientEndpoint, pushEndpoint);
                }
                else
                {
                    UnityEngine.Debug.LogError("[uLoopMCP] [ERROR] Failed to access McpSessionManager instance safely");
                }
            }
            else
            {
                // Check if we already have a valid endpoint saved for this client
                McpSessionManager sessionManager = McpSessionManager.GetSafeInstance();
                string existingEndpoint = sessionManager?.GetPushServerEndpoint(clientEndpoint);
                if (!string.IsNullOrEmpty(existingEndpoint))
                {
                    UnityEngine.Debug.LogWarning($"[uLoopMCP] [WARNING] SetClientName: Empty/null push notification endpoint received for client '{clientEndpoint}' - NOT overwriting existing endpoint '{existingEndpoint}'");
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[uLoopMCP] [WARNING] SetClientName: Empty/null push notification endpoint received for client '{clientEndpoint}' and no existing endpoint found");
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