using System.Diagnostics;

using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    public interface IDynamicCompilationRuntimeServicesFactory
    {
        IDynamicCodeSourcePreparationService CreateSourcePreparationService();

        ICompiledAssemblyBuilder CreateAssemblyBuilder();

        void ShutdownForServerReset();
    }

    public sealed class DynamicCompilationServiceRegistryService
    {
        private readonly object _syncRoot = new object();
        private IDynamicCompilationServiceFactory _factory;

        public DynamicCompilationServiceRegistryService(IDynamicCompilationServiceFactory factory)
        {
            RegisterFactory(factory);
        }

        public bool HasRegisteredFactory
        {
            get
            {
                lock (_syncRoot)
                {
                    return _factory != null;
                }
            }
        }

        public void RegisterFactory(IDynamicCompilationServiceFactory factory)
        {
            Debug.Assert(factory != null, "factory must not be null");

            lock (_syncRoot)
            {
                _factory = factory;
            }
        }

        public bool TryCreate(DynamicCodeSecurityLevel securityLevel, out IDynamicCompilationService service)
        {
            IDynamicCompilationServiceFactory factory;
            lock (_syncRoot)
            {
                factory = _factory;
            }

            if (factory == null)
            {
                service = null;
                return false;
            }

            service = factory.Create(securityLevel);
            return service != null;
        }

        public IDynamicCompilationServiceFactory SwapFactoryForTests(IDynamicCompilationServiceFactory factory)
        {
            lock (_syncRoot)
            {
                IDynamicCompilationServiceFactory previousFactory = _factory;
                _factory = factory;
                return previousFactory;
            }
        }
    }
}
