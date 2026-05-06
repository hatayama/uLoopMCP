using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.UnityCliLoop
{
    /// <summary>
    /// Bundled tool entry point for input recording. The platform supplies recording work through host services.
    /// </summary>
    [UnityCliLoopTool]
    public class RecordInputTool : UnityCliLoopTool<RecordInputSchema, RecordInputResponse>, IUnityCliLoopToolHostServicesReceiver
    {
        private IUnityCliLoopRecordInputService _recordInput;

        public override string ToolName => "record-input";

        public void InitializeHostServices(IUnityCliLoopToolHostServices services)
        {
            if (services == null)
            {
                throw new System.ArgumentNullException(nameof(services));
            }

            _recordInput = services.RecordInput ?? throw new System.ArgumentNullException(nameof(services.RecordInput));
        }

        protected override async Task<RecordInputResponse> ExecuteAsync(RecordInputSchema parameters, CancellationToken ct)
        {
            if (_recordInput == null)
            {
                throw new System.InvalidOperationException("Host services were not initialized.");
            }

            UnityCliLoopRecordInputResult result = await _recordInput.RecordInputAsync(ToRequest(parameters), ct);
            return ToResponse(result);
        }

        private static UnityCliLoopRecordInputRequest ToRequest(RecordInputSchema parameters)
        {
            if (parameters == null)
            {
                throw new System.ArgumentNullException(nameof(parameters));
            }

            return new UnityCliLoopRecordInputRequest
            {
                Action = parameters.Action,
                OutputPath = parameters.OutputPath,
                Keys = parameters.Keys,
                DelaySeconds = parameters.DelaySeconds,
                ShowOverlay = parameters.ShowOverlay,
            };
        }

        private static RecordInputResponse ToResponse(UnityCliLoopRecordInputResult result)
        {
            if (result == null)
            {
                throw new System.ArgumentNullException(nameof(result));
            }

            return new RecordInputResponse
            {
                Success = result.Success,
                Message = result.Message,
                Action = result.Action,
                OutputPath = result.OutputPath,
                TotalFrames = result.TotalFrames,
                DurationSeconds = result.DurationSeconds,
            };
        }
    }
}
