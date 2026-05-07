using System.Threading.Tasks;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    internal interface ICompiledCommandInvoker
    {
        Task<ExecutionResult> ExecuteAsync(ExecutionContext context);
    }
}
