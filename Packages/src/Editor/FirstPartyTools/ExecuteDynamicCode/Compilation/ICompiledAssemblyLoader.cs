using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    public interface ICompiledAssemblyLoader
    {
        CompiledAssemblyLoadResult Load(
            DynamicCodeSecurityLevel securityLevel,
            byte[] assemblyBytes);
    }
}
