using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.UnityCliLoop
{
    /// <summary>
    /// Development-only connection test tool.
    /// </summary>
    [UnityCliLoopTool(DisplayDevelopmentOnly = true)]
    public class PingTool : UnityCliLoopTool<PingSchema, PingResponse>
    {
        public override string ToolName => "ping";

        protected override Task<PingResponse> ExecuteAsync(PingSchema parameters, CancellationToken ct)
        {
            string message = parameters.Message;
            string response = $"Unity CLI Loop bridge received: {message}";

            PingResponse pingResponse = new PingResponse(response);
            return Task.FromResult(pingResponse);
        }
    }
}
