using System.Diagnostics;

namespace io.github.hatayama.UnityCliLoop
{
    public interface IDynamicCompilationRuntimeServicesFactory
    {
        IDynamicCodeSourcePreparationService CreateSourcePreparationService();

        ICompiledAssemblyBuilder CreateAssemblyBuilder();

        void ShutdownForServerReset();
    }

    public static class DynamicCompilationRuntimeServicesRegistry
    {
        private static readonly object SyncRoot = new object();
        private static IDynamicCompilationRuntimeServicesFactory _factory;

        public static void RegisterFactory(IDynamicCompilationRuntimeServicesFactory factory)
        {
            Debug.Assert(factory != null, "factory must not be null");

            lock (SyncRoot)
            {
                _factory = factory;
            }
        }

        public static IDynamicCodeSourcePreparationService CreateSourcePreparationService()
        {
            IDynamicCompilationRuntimeServicesFactory factory = GetFactory();
            return factory.CreateSourcePreparationService();
        }

        public static ICompiledAssemblyBuilder CreateAssemblyBuilder()
        {
            IDynamicCompilationRuntimeServicesFactory factory = GetFactory();
            return factory.CreateAssemblyBuilder();
        }

        public static void ShutdownForServerReset()
        {
            IDynamicCompilationRuntimeServicesFactory factory = GetFactory();
            factory.ShutdownForServerReset();
        }

        private static IDynamicCompilationRuntimeServicesFactory GetFactory()
        {
            lock (SyncRoot)
            {
                if (_factory == null)
                {
                    throw new System.InvalidOperationException("Dynamic compilation runtime services factory is not registered.");
                }

                return _factory;
            }
        }
    }

    public static class DynamicCompilationServiceRegistry
    {
        private static readonly object SyncRoot = new object();
        private static IDynamicCompilationServiceFactory _factory;

        public static bool HasRegisteredFactory
        {
            get
            {
                lock (SyncRoot)
                {
                    return _factory != null;
                }
            }
        }

        public static void RegisterFactory(IDynamicCompilationServiceFactory factory)
        {
            Debug.Assert(factory != null, "factory must not be null");

            lock (SyncRoot)
            {
                _factory = factory;
            }
        }

        public static bool TryCreate(DynamicCodeSecurityLevel securityLevel, out IDynamicCompilationService service)
        {
            IDynamicCompilationServiceFactory factory;
            lock (SyncRoot)
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

        public static IDynamicCompilationServiceFactory SwapFactoryForTests(IDynamicCompilationServiceFactory factory)
        {
            lock (SyncRoot)
            {
                IDynamicCompilationServiceFactory previousFactory = _factory;
                _factory = factory;
                return previousFactory;
            }
        }
    }
}
