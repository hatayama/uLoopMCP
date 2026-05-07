using System;

using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    internal interface IDynamicCodeExecutorPool : IDisposable
    {
        IDynamicCodeExecutor GetOrCreate(DynamicCodeSecurityLevel securityLevel);
    }
}
