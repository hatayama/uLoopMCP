using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.UnityCliLoop
{
    /// <summary>
    /// Dynamic C# code execution tool exposed through the CLI.
    /// Delegates workflow orchestration to the dedicated execute-dynamic-code use case.
    /// </summary>
    [UnityCliLoopTool]
    public class ExecuteDynamicCodeTool : UnityCliLoopTool<ExecuteDynamicCodeSchema, ExecuteDynamicCodeResponse>
    {
        public override string ToolName => "execute-dynamic-code";

        protected override async Task<ExecuteDynamicCodeResponse> ExecuteAsync(
            ExecuteDynamicCodeSchema parameters,
            CancellationToken ct)
        {
            IExecuteDynamicCodeUseCase useCase = await DynamicCodeServices.GetExecuteDynamicCodeUseCaseAsync();
            return await useCase.ExecuteAsync(parameters, ct);
        }
    }
}
