using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    public sealed class PingSchema : BaseToolSchema { }

    public sealed class PingResponse : BaseToolResponse
    {
        public string Status { get; set; }
        public long Uptime { get; set; }
    }

    public sealed class PingTool : AbstractDeviceTool<PingSchema, PingResponse>
    {
        public override string ToolName => "ping";

        private readonly System.Diagnostics.Stopwatch _uptime;

        public PingTool(System.Diagnostics.Stopwatch uptime)
        {
            _uptime = uptime;
        }

        protected override Task<PingResponse> ExecuteAsync(PingSchema parameters, CancellationToken ct)
        {
            PingResponse response = new()
            {
                Status = "ok",
                Uptime = _uptime.ElapsedMilliseconds
            };
            return Task.FromResult(response);
        }
    }
}
