using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.UnityCliLoop
{
    /// <summary>
    /// Bundled tool entry point for mouse input simulation. The platform supplies simulation work through host services.
    /// </summary>
    [UnityCliLoopTool]
    public class SimulateMouseInputTool : UnityCliLoopTool<SimulateMouseInputSchema, SimulateMouseInputResponse>, IUnityCliLoopToolHostServicesReceiver
    {
        private IUnityCliLoopMouseInputSimulationService _mouseInputSimulation;

        public override string ToolName => "simulate-mouse-input";

        public void InitializeHostServices(IUnityCliLoopToolHostServices services)
        {
            if (services == null)
            {
                throw new System.ArgumentNullException(nameof(services));
            }

            _mouseInputSimulation = services.MouseInputSimulation ?? throw new System.ArgumentNullException(nameof(services.MouseInputSimulation));
        }

        protected override async Task<SimulateMouseInputResponse> ExecuteAsync(SimulateMouseInputSchema parameters, CancellationToken ct)
        {
            if (_mouseInputSimulation == null)
            {
                throw new System.InvalidOperationException("Host services were not initialized.");
            }

            UnityCliLoopMouseInputSimulationResult result =
                await _mouseInputSimulation.SimulateMouseInputAsync(ToRequest(parameters), ct);
            return ToResponse(result);
        }

        private static UnityCliLoopMouseInputSimulationRequest ToRequest(SimulateMouseInputSchema parameters)
        {
            if (parameters == null)
            {
                throw new System.ArgumentNullException(nameof(parameters));
            }

            return new UnityCliLoopMouseInputSimulationRequest
            {
                Action = parameters.Action,
                X = parameters.X,
                Y = parameters.Y,
                Button = parameters.Button,
                Duration = parameters.Duration,
                DeltaX = parameters.DeltaX,
                DeltaY = parameters.DeltaY,
                ScrollX = parameters.ScrollX,
                ScrollY = parameters.ScrollY,
            };
        }

        private static SimulateMouseInputResponse ToResponse(UnityCliLoopMouseInputSimulationResult result)
        {
            if (result == null)
            {
                throw new System.ArgumentNullException(nameof(result));
            }

            return new SimulateMouseInputResponse
            {
                Success = result.Success,
                Message = result.Message,
                Action = result.Action,
                Button = result.Button,
                PositionX = result.PositionX,
                PositionY = result.PositionY,
            };
        }
    }
}
