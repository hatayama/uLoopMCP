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
            
            UpdateClientNameInServer(clientName);
            
            // Save notification port if provided
            if (parameters.NotificationPort.HasValue)
            {
                SaveNotificationPort(clientName, parameters.NotificationPort.Value);
            }
            
            string message = string.Format(McpConstants.CLIENT_SUCCESS_MESSAGE_TEMPLATE, clientName);
            SetClientNameResponse response = new SetClientNameResponse(message, clientName);
            return Task.FromResult(response);
        }
        
        /// <summary>
        /// Gets the current client context from JsonRpcProcessor
        /// </summary>
        /// <returns>The current client context, or null if not available</returns>
        private ClientExecutionContext GetCurrentClientContext()
        {
            return JsonRpcProcessor.CurrentClientContext;
        }
        
        private void UpdateClientNameInServer(string clientName)
        {
            McpBridgeServer server = McpServerController.CurrentServer;
            if (server == null) return;
            
            var connectedClients = server.GetConnectedClients();
            if (connectedClients.Count == 0) return;
            
            // Get current client context
            var clientContext = GetCurrentClientContext();
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
        
        private void SaveNotificationPort(string clientName, int notificationPort)
        {
            // Get current client context to identify the endpoint
            var clientContext = GetCurrentClientContext();
            if (clientContext == null)
            {
                return;
            }
            
            string clientEndpoint = clientContext.Endpoint;
            
            try
            {
                // Save notification port through DomainReloadDetectionService
                DomainReloadDetectionService.SaveClientNotificationPort(clientEndpoint, notificationPort);
                
                VibeLogger.LogInfo(
                    "client_notification_port_received",
                    "Received and saved notification port from TypeScript client",
                    new { clientName, clientEndpoint, notificationPort }
                );
            }
            catch (System.Exception ex)
            {
                VibeLogger.LogError(
                    "client_notification_port_save_failed",
                    "Failed to save client notification port",
                    new { clientName, clientEndpoint, notificationPort, error = ex.Message }
                );
            }
        }
    }
}