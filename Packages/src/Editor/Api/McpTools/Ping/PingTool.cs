using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Ping tool handler - Type-safe implementation using Schema and Response
    /// Connection test and message echo functionality
    /// </summary>
    [McpTool(
        DisplayDevelopmentOnly = true,
        Description = "Connection test and message echo"
    )]
    public class PingTool : AbstractUnityTool<PingSchema, PingResponse>
    {
        public override string ToolName => "ping";

        protected override Task<PingResponse> ExecuteAsync(PingSchema parameters, CancellationToken cancellationToken)
        {
            // Type-safe parameter access - no more string parsing!
            string message = parameters.Message;
            string response = $"Unity MCP Bridge received: {message}";

            // Create type-safe response
            PingResponse pingResponse = new PingResponse(response);
            return Task.FromResult(pingResponse);
        }
    }
}