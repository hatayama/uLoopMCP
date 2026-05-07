using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    public interface IDynamicCompilationService
    {
        Task<CompilationResult> CompileAsync(CompilationRequest request, CancellationToken ct = default);
    }
}
