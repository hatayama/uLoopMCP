using System.Threading.Tasks;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    internal interface IShutdownAwareDynamicCodeExecutionRuntime : IDynamicCodeExecutionRuntime
    {
        Task ShutdownAsync();
    }
}
