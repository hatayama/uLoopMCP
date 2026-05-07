using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    public interface IDynamicCompilationServiceFactory
    {
        IDynamicCompilationService Create(DynamicCodeSecurityLevel securityLevel);
    }
}
