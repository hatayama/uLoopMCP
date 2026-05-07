using System.Threading;
using System.Threading.Tasks;

using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    public interface ICompiledAssemblyBuilder
    {
        bool SupportsAutoPrewarm();

        Task<CompiledAssemblyBuildResult> BuildAsync(
            DynamicCompilationPlan plan,
            CancellationToken ct = default);
    }
}
