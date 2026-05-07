using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.UnityCliLoop
{
    /// <summary>
    /// Application-owned bridge from the public host-service contract to dynamic-code orchestration.
    /// </summary>
    internal sealed class UnityCliLoopDynamicCodeExecutionHostService : IUnityCliLoopDynamicCodeExecutionService
    {
        private readonly DynamicCodeServicesRegistry _dynamicCodeServices;

        public UnityCliLoopDynamicCodeExecutionHostService(DynamicCodeServicesRegistry dynamicCodeServices)
        {
            UnityEngine.Debug.Assert(dynamicCodeServices != null, "dynamicCodeServices must not be null");

            _dynamicCodeServices = dynamicCodeServices ?? throw new System.ArgumentNullException(nameof(dynamicCodeServices));
        }

        public async Task<ExecuteDynamicCodeResponse> ExecuteAsync(ExecuteDynamicCodeSchema parameters, CancellationToken ct)
        {
            IExecuteDynamicCodeUseCase useCase = await _dynamicCodeServices.GetExecuteDynamicCodeUseCaseAsync();
            return await useCase.ExecuteAsync(parameters, ct);
        }
    }
}
