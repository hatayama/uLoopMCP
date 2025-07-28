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
            // Get current client context for endpoint information
            var clientContext = GetCurrentClientContext();
            if (clientContext == null)
            {
                return new SetClientNameResponse("No client context available", parameters.ClientName);
            }

            // Delegate temporal cohesion to UseCase
            var useCase = new ClientRegistrationUseCase();
            var useCaseSchema = new ClientRegistrationSchema
            {
                ClientName = parameters.ClientName,
                ClientEndpoint = clientContext.Endpoint,
                NotificationPort = parameters.NotificationPort
            };

            var useCaseResult = await useCase.ExecuteAsync(useCaseSchema, cancellationToken);
            
            // Convert UseCase response to Tool response
            return new SetClientNameResponse(useCaseResult.Message, useCaseResult.ClientName);
        }
        
        /// <summary>
        /// Gets the current client context from JsonRpcProcessor
        /// </summary>
        /// <returns>The current client context, or null if not available</returns>
        private ClientExecutionContext GetCurrentClientContext()
        {
            return JsonRpcProcessor.CurrentClientContext;
        }
        
    }
}