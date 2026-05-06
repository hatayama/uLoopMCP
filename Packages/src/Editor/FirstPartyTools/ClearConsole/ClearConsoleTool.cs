using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.UnityCliLoop
{
    /// <summary>
    /// Bundled tool entry point that clears Unity Console entries through the public tool contract.
    /// </summary>
    [UnityCliLoopTool]
    public class ClearConsoleTool : UnityCliLoopTool<ClearConsoleSchema, ClearConsoleResponse>,
        IUnityCliLoopToolHostServicesReceiver
    {
        private IUnityCliLoopConsoleClearService _consoleClear;

        public override string ToolName => "clear-console";

        public void InitializeHostServices(IUnityCliLoopToolHostServices services)
        {
            if (services == null)
            {
                throw new System.ArgumentNullException(nameof(services));
            }

            _consoleClear = services.ConsoleClear ?? throw new System.ArgumentNullException(nameof(services.ConsoleClear));
        }

        protected override Task<ClearConsoleResponse> ExecuteAsync(ClearConsoleSchema parameters, CancellationToken ct)
        {
            if (parameters == null)
            {
                throw new System.ArgumentNullException(nameof(parameters));
            }

            if (_consoleClear == null)
            {
                throw new System.InvalidOperationException("Host services were not initialized.");
            }

            ct.ThrowIfCancellationRequested();
            UnityCliLoopConsoleClearResult result = _consoleClear.Clear(parameters.AddConfirmationMessage);
            ClearConsoleResponse response = new ClearConsoleResponse(
                success: result.Success,
                clearedLogCount: result.ClearedLogCount,
                clearedCounts: new ClearedLogCounts(
                    result.ClearedCounts.ErrorCount,
                    result.ClearedCounts.WarningCount,
                    result.ClearedCounts.LogCount),
                message: result.Message);

            return Task.FromResult(response);
        }
    }
}
