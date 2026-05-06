using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.UnityCliLoop
{
    [UnityCliLoopTool(Description = "Control Unity Editor play mode (play/stop/pause)")]
    public class ControlPlayModeTool : UnityCliLoopTool<ControlPlayModeSchema, ControlPlayModeResponse>
    {
        public override string ToolName => "control-play-mode";

        protected override async Task<ControlPlayModeResponse> ExecuteAsync(ControlPlayModeSchema parameters, CancellationToken cancellationToken)
        {
            ControlPlayModeUseCase useCase = new();
            return await useCase.ExecuteAsync(parameters, cancellationToken);
        }
    }
}

