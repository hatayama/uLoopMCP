using System.Runtime.CompilerServices;
using io.github.hatayama.UnityCliLoop.ToolContracts;

[assembly: InternalsVisibleTo("uLoopMCP.Tests.Editor")]

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Creates Dynamic Compilation Runtime Services instances with the dependencies required by this module.
    /// </summary>
    public sealed class DynamicCompilationRuntimeServicesFactory : IDynamicCompilationRuntimeServicesFactory
    {
        public IDynamicCodeSourcePreparationService CreateSourcePreparationService()
        {
            return new DynamicCodeSourcePreparationService();
        }

        public ICompiledAssemblyBuilder CreateAssemblyBuilder()
        {
            return new CompiledAssemblyBuilder(
                new ExternalCompilerPathResolutionService(),
                new DynamicReferenceSetBuilderService(),
                new DynamicCompilationBackend());
        }

        public void ShutdownForServerReset()
        {
            SharedRoslynCompilerWorkerHost.ShutdownForServerReset();
        }
    }

    /// <summary>
    /// Creates Dynamic Code Compilation Service instances with the dependencies required by this module.
    /// </summary>
    public sealed class DynamicCodeCompilationServiceFactory : IDynamicCompilationServiceFactory
    {
        public IDynamicCompilationService Create(DynamicCodeSecurityLevel securityLevel)
        {
            return new DynamicCodeCompiler(securityLevel);
        }
    }
}
