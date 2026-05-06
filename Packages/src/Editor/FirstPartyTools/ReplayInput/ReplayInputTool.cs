using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.UnityCliLoop
{
    /// <summary>
    /// Bundled tool entry point for input replay. The platform supplies replay work through host services.
    /// </summary>
    [UnityCliLoopTool]
    public class ReplayInputTool : UnityCliLoopTool<ReplayInputSchema, ReplayInputResponse>, IUnityCliLoopToolHostServicesReceiver
    {
        private IUnityCliLoopReplayInputService _replayInput;

        public override string ToolName => "replay-input";

        public void InitializeHostServices(IUnityCliLoopToolHostServices services)
        {
            if (services == null)
            {
                throw new System.ArgumentNullException(nameof(services));
            }

            _replayInput = services.ReplayInput ?? throw new System.ArgumentNullException(nameof(services.ReplayInput));
        }

        protected override async Task<ReplayInputResponse> ExecuteAsync(ReplayInputSchema parameters, CancellationToken ct)
        {
            if (_replayInput == null)
            {
                throw new System.InvalidOperationException("Host services were not initialized.");
            }

            UnityCliLoopReplayInputResult result = await _replayInput.ReplayInputAsync(ToRequest(parameters), ct);
            return ToResponse(result);
        }

        private static UnityCliLoopReplayInputRequest ToRequest(ReplayInputSchema parameters)
        {
            if (parameters == null)
            {
                throw new System.ArgumentNullException(nameof(parameters));
            }

            return new UnityCliLoopReplayInputRequest
            {
                Action = parameters.Action,
                InputPath = parameters.InputPath,
                ShowOverlay = parameters.ShowOverlay,
                Loop = parameters.Loop,
            };
        }

        private static ReplayInputResponse ToResponse(UnityCliLoopReplayInputResult result)
        {
            if (result == null)
            {
                throw new System.ArgumentNullException(nameof(result));
            }

            return new ReplayInputResponse
            {
                Success = result.Success,
                Message = result.Message,
                Action = result.Action,
                InputPath = result.InputPath,
                CurrentFrame = result.CurrentFrame,
                TotalFrames = result.TotalFrames,
                Progress = result.Progress,
                IsReplaying = result.IsReplaying,
            };
        }
    }
}
