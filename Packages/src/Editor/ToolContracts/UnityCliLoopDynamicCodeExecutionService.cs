using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.UnityCliLoop
{
    /// <summary>
    /// Host capability that lets a bundled tool execute dynamic C# code through the platform runtime.
    /// </summary>
    public interface IUnityCliLoopDynamicCodeExecutionService
    {
        Task<ExecuteDynamicCodeResponse> ExecuteAsync(ExecuteDynamicCodeSchema parameters, CancellationToken ct);
    }
}
