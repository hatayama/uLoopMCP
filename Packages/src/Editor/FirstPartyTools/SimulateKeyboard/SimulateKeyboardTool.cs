using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.UnityCliLoop
{
    /// <summary>
    /// Bundled tool entry point for keyboard simulation. The platform supplies simulation work through host services.
    /// </summary>
    [UnityCliLoopTool]
    public class SimulateKeyboardTool : UnityCliLoopTool<SimulateKeyboardSchema, SimulateKeyboardResponse>, IUnityCliLoopToolHostServicesReceiver
    {
        private IUnityCliLoopKeyboardSimulationService _keyboardSimulation;

        public override string ToolName => "simulate-keyboard";

        public void InitializeHostServices(IUnityCliLoopToolHostServices services)
        {
            if (services == null)
            {
                throw new System.ArgumentNullException(nameof(services));
            }

            _keyboardSimulation = services.KeyboardSimulation ?? throw new System.ArgumentNullException(nameof(services.KeyboardSimulation));
        }

        protected override async Task<SimulateKeyboardResponse> ExecuteAsync(SimulateKeyboardSchema parameters, CancellationToken ct)
        {
            if (_keyboardSimulation == null)
            {
                throw new System.InvalidOperationException("Host services were not initialized.");
            }

            UnityCliLoopKeyboardSimulationResult result = await _keyboardSimulation.SimulateKeyboardAsync(ToRequest(parameters), ct);
            return ToResponse(result);
        }

        private static UnityCliLoopKeyboardSimulationRequest ToRequest(SimulateKeyboardSchema parameters)
        {
            if (parameters == null)
            {
                throw new System.ArgumentNullException(nameof(parameters));
            }

            return new UnityCliLoopKeyboardSimulationRequest
            {
                Action = parameters.Action,
                Key = parameters.Key,
                Duration = parameters.Duration,
            };
        }

        private static SimulateKeyboardResponse ToResponse(UnityCliLoopKeyboardSimulationResult result)
        {
            if (result == null)
            {
                throw new System.ArgumentNullException(nameof(result));
            }

            return new SimulateKeyboardResponse
            {
                Success = result.Success,
                Message = result.Message,
                Action = result.Action,
                KeyName = result.KeyName,
            };
        }
    }
}
