using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.UnityCliLoop
{
    /// <summary>
    /// Bundled tool entry point that exposes Unity Console snapshots through the public tool contract.
    /// </summary>
    [UnityCliLoopTool]
    public class GetLogsTool : UnityCliLoopTool<GetLogsSchema, GetLogsResponse>, IUnityCliLoopToolHostServicesReceiver
    {
        private IUnityCliLoopConsoleLogService _consoleLogs;

        public override string ToolName => "get-logs";

        public void InitializeHostServices(IUnityCliLoopToolHostServices services)
        {
            if (services == null)
            {
                throw new System.ArgumentNullException(nameof(services));
            }

            _consoleLogs = services.ConsoleLogs ?? throw new System.ArgumentNullException(nameof(services.ConsoleLogs));
        }

        protected override async Task<GetLogsResponse> ExecuteAsync(GetLogsSchema parameters, CancellationToken ct)
        {
            if (_consoleLogs == null)
            {
                throw new System.InvalidOperationException("Host services were not initialized.");
            }

            GetLogsUseCase useCase = new GetLogsUseCase(_consoleLogs, new LogFilteringService());
            return await useCase.ExecuteAsync(parameters, ct);
        }
    }
}
