using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.UnityCliLoop
{
    /// <summary>
    /// Bundled tool entry point for Unity Test Runner execution. The platform supplies test execution through host services.
    /// </summary>
    [UnityCliLoopTool]
    public class RunTestsTool : UnityCliLoopTool<RunTestsSchema, RunTestsResponse>, IUnityCliLoopToolHostServicesReceiver
    {
        private IUnityCliLoopTestExecutionService _testExecution;

        public override string ToolName => "run-tests";

        public void InitializeHostServices(IUnityCliLoopToolHostServices services)
        {
            if (services == null)
            {
                throw new System.ArgumentNullException(nameof(services));
            }

            _testExecution = services.TestExecution ?? throw new System.ArgumentNullException(nameof(services.TestExecution));
        }

        protected override async Task<RunTestsResponse> ExecuteAsync(RunTestsSchema parameters, CancellationToken ct)
        {
            if (_testExecution == null)
            {
                throw new System.InvalidOperationException("Host services were not initialized.");
            }

            UnityCliLoopTestExecutionResult result = await _testExecution.RunTestsAsync(ToRequest(parameters), ct);
            return ToResponse(result);
        }

        private static UnityCliLoopTestExecutionRequest ToRequest(RunTestsSchema parameters)
        {
            if (parameters == null)
            {
                throw new System.ArgumentNullException(nameof(parameters));
            }

            return new UnityCliLoopTestExecutionRequest
            {
                TestMode = ToContractTestMode(parameters.TestMode),
                FilterType = parameters.FilterType,
                FilterValue = parameters.FilterValue,
                SaveBeforeRun = parameters.SaveBeforeRun,
            };
        }

        private static UnityCliLoopTestMode ToContractTestMode(UnityEditor.TestTools.TestRunner.Api.TestMode testMode)
        {
            if (testMode == UnityEditor.TestTools.TestRunner.Api.TestMode.PlayMode)
            {
                return UnityCliLoopTestMode.PlayMode;
            }

            return UnityCliLoopTestMode.EditMode;
        }

        private static RunTestsResponse ToResponse(UnityCliLoopTestExecutionResult result)
        {
            if (result == null)
            {
                throw new System.ArgumentNullException(nameof(result));
            }

            return new RunTestsResponse(
                success: result.Success,
                message: result.Message,
                completedAt: result.CompletedAt,
                testCount: result.TestCount,
                passedCount: result.PassedCount,
                failedCount: result.FailedCount,
                skippedCount: result.SkippedCount,
                xmlPath: result.XmlPath);
        }
    }
}
