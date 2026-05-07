using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.UnityCliLoop
{
    public interface ICompiledAssemblyBuilder
    {
        bool SupportsAutoPrewarm();

        Task<CompiledAssemblyBuildResult> BuildAsync(
            DynamicCompilationPlan plan,
            CancellationToken ct = default);
    }
}
