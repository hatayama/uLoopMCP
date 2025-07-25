using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Test execution tool handler - Type-safe implementation using Schema and Response
    /// Executes tests using Unity Test Runner and returns the results
    /// </summary>
    [McpTool(
        RequiredSecuritySetting = SecuritySettings.EnableTestsExecution,
        Description = "Execute Unity Test Runner with advanced filtering options - exact test methods, regex patterns for classes/namespaces, assembly filtering"
    )]
    public class RunTestsTool : AbstractUnityTool<RunTestsSchema, RunTestsResponse>
    {
        public override string ToolName => "run-tests";

        protected override async Task<RunTestsResponse> ExecuteAsync(RunTestsSchema parameters, CancellationToken cancellationToken)
        {
            // RunTestsUseCaseインスタンスを生成して実行
            RunTestsUseCase useCase = new();
            return await useCase.ExecuteAsync(parameters, cancellationToken);
        }

    }
}