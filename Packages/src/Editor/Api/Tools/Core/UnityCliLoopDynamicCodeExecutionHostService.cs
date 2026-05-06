using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.UnityCliLoop
{
    /// <summary>
    /// Application-owned bridge from the public host-service contract to dynamic-code orchestration.
    /// </summary>
    internal sealed class UnityCliLoopDynamicCodeExecutionHostService : IUnityCliLoopDynamicCodeExecutionService
    {
        public async Task<ExecuteDynamicCodeResponse> ExecuteAsync(ExecuteDynamicCodeSchema parameters, CancellationToken ct)
        {
            IExecuteDynamicCodeUseCase useCase = await DynamicCodeServices.GetExecuteDynamicCodeUseCaseAsync();
            return await useCase.ExecuteAsync(parameters, ct);
        }
    }
}
