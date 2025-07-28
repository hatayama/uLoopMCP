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
            
            // IMPORTANT: Update client name first before saving notification port
            // This ensures the client is properly registered before UpdateNotificationPort creates entries
            UpdateClientNameInServer(clientName);
            
            // Save notification port if provided (after client name is updated)
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
            // Get current client context
            var clientContext = GetCurrentClientContext();
            if (clientContext == null)
            {
                VibeLogger.LogWarning(
                    "set_client_name_no_context",
                    "No client context available for SetClientName",
                    new { clientName }
                );
                return;
            }
            
            string clientEndpoint = clientContext.Endpoint;
            
            // FIXED: Directly add/update tool entry instead of relying on existing entries
            // This resolves the chicken-and-egg problem where UpdateClientName required existing clients
            ConnectedToolsMonitoringService.AddOrUpdateTool(
                clientName, 
                clientEndpoint, 
                0, // notification port will be updated separately in SaveNotificationPort
                System.DateTime.Now
            );
            
            VibeLogger.LogInfo(
                "client_name_updated_in_server",
                "Client name updated directly in ConnectedToolsMonitoringService",
                new { clientName, clientEndpoint }
            );
            
            // Also update McpBridgeServer for backward compatibility
            McpBridgeServer server = McpServerController.CurrentServer;
            if (server != null)
            {
                server.UpdateClientName(clientEndpoint, clientName);
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