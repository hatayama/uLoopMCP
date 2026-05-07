using System.Runtime.CompilerServices;
using io.github.hatayama.UnityCliLoop.ToolContracts;

[assembly: InternalsVisibleTo("uLoopMCP.Tests.Editor")]

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
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

    public sealed class DynamicCodeCompilationServiceFactory : IDynamicCompilationServiceFactory
    {
        public IDynamicCompilationService Create(DynamicCodeSecurityLevel securityLevel)
        {
            return new DynamicCodeCompiler(securityLevel);
        }
    }
}
