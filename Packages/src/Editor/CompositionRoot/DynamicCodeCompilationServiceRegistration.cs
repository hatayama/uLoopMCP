using UnityEditor;

namespace io.github.hatayama.UnityCliLoop
{
    public static class DynamicCodeCompilationServiceRegistration
    {
        private static readonly object SyncRoot = new object();
        private static bool IsRegistered;

        [InitializeOnLoadMethod]
        private static void Register()
        {
            EnsureRegistered();
        }

        internal static DynamicCodeServicesRegistry EnsureRegistered()
        {
            lock (SyncRoot)
            {
                if (IsRegistered)
                {
                    return DynamicCodeServices.GetRegistry();
                }

                DynamicCompilationServiceRegistryService compilationServiceRegistry =
                    new DynamicCompilationServiceRegistryService(new DynamicCodeCompilationServiceFactory());
                DynamicCodeServicesRegistry dynamicCodeServicesRegistry =
                    new DynamicCodeServicesRegistry(
                        new DynamicCompilationRuntimeServicesFactory(),
                        compilationServiceRegistry);
                DynamicCodeServices.RegisterRegistry(dynamicCodeServicesRegistry);
                IsRegistered = true;
                return dynamicCodeServicesRegistry;
            }
        }
    }
}
