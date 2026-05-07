using io.github.hatayama.UnityCliLoop.FirstPartyTools;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools.Factory
{
    internal interface IDynamicCodeExecutorProvider
    {
        IDynamicCodeExecutor Create(DynamicCodeSecurityLevel securityLevel);
    }
}
