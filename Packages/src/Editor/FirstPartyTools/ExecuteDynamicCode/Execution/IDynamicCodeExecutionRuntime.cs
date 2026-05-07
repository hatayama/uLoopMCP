using System.Threading;
using System.Threading.Tasks;

using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    internal interface IDynamicCodeExecutionRuntime
    {
        bool SupportsAutoPrewarm();

        Task<ExecutionResult> ExecuteAsync(
            DynamicCodeExecutionRequest request,
            CancellationToken cancellationToken = default);

        Task<(bool Entered, ExecutionResult Result)> TryExecuteIfIdleAsync(
            DynamicCodeExecutionRequest request,
            CancellationToken cancellationToken = default);
    }
}
