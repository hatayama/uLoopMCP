#if ULOOPMCP_HAS_ROSLYN
namespace io.github.hatayama.uLoopMCP
{
    public sealed class RoslynDynamicCompilationServiceFactory : IDynamicCompilationServiceFactory
    {
        public IDynamicCompilationService Create(DynamicCodeSecurityLevel securityLevel)
        {
            return new RoslynCompiler(securityLevel);
        }
    }
}
#endif
