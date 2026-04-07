namespace io.github.hatayama.uLoopMCP
{
    internal sealed class CompiledAssemblyLoadService
    {
        public CompiledAssemblyLoadResult Load(
            DynamicCodeSecurityLevel securityLevel,
            byte[] assemblyBytes)
        {
            return CompiledAssemblyLoader.Load(securityLevel, assemblyBytes);
        }
    }
}
