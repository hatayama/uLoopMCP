namespace io.github.hatayama.uLoopMCP
{
    public interface IDynamicCompilationServiceFactory
    {
        IDynamicCompilationService Create(DynamicCodeSecurityLevel securityLevel);
    }
}
