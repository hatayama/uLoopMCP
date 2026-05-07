using System.Threading.Tasks;

using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    internal interface IShutdownAwareDynamicCodeExecutionRuntime : IDynamicCodeExecutionRuntime
    {
        Task ShutdownAsync();
    }
}
