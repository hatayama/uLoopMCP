using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.UnityCliLoop
{
    /// <summary>
    /// Dynamic C# code execution tool exposed through the CLI.
    /// Delegates workflow orchestration to the dedicated execute-dynamic-code use case.
    /// </summary>
    [UnityCliLoopTool]
    public class ExecuteDynamicCodeTool : UnityCliLoopTool<ExecuteDynamicCodeSchema, ExecuteDynamicCodeResponse>,
        IUnityCliLoopToolHostServicesReceiver
    {
        private IUnityCliLoopDynamicCodeExecutionService _dynamicCodeExecution;

        public override string ToolName => "execute-dynamic-code";

        public void InitializeHostServices(IUnityCliLoopToolHostServices services)
        {
            if (services == null)
            {
                throw new System.ArgumentNullException(nameof(services));
            }

            _dynamicCodeExecution = services.DynamicCodeExecution ??
                                    throw new System.ArgumentNullException(nameof(services.DynamicCodeExecution));
        }

        protected override async Task<ExecuteDynamicCodeResponse> ExecuteAsync(
            ExecuteDynamicCodeSchema parameters,
            CancellationToken ct)
        {
            if (_dynamicCodeExecution == null)
            {
                throw new System.InvalidOperationException("Host services were not initialized.");
            }

            return await _dynamicCodeExecution.ExecuteAsync(parameters, ct);
        }
    }
}
