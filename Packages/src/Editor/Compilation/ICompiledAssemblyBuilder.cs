using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    internal interface ICompiledAssemblyBuilder
    {
        Task<CompiledAssemblyBuildResult> BuildAsync(
            DynamicCompilationPlan plan,
            CancellationToken ct = default);
    }
}
