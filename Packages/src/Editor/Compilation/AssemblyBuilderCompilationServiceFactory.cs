namespace io.github.hatayama.uLoopMCP
{
    public sealed class AssemblyBuilderCompilationServiceFactory : IDynamicCompilationServiceFactory
    {
        public IDynamicCompilationService Create(DynamicCodeSecurityLevel securityLevel)
        {
            return new AssemblyBuilderCompiler(securityLevel);
        }
    }
}
