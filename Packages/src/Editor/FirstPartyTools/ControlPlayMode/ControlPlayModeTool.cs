using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.UnityCliLoop
{
    [UnityCliLoopTool]
    public class ControlPlayModeTool : UnityCliLoopTool<ControlPlayModeSchema, ControlPlayModeResponse>
    {
        public override string ToolName => "control-play-mode";

        protected override async Task<ControlPlayModeResponse> ExecuteAsync(ControlPlayModeSchema parameters, CancellationToken ct)
        {
            ControlPlayModeUseCase useCase = new ControlPlayModeUseCase();
            return await useCase.ExecuteAsync(parameters, ct);
        }
    }
}
