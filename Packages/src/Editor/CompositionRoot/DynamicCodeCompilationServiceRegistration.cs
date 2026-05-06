using UnityEditor;

namespace io.github.hatayama.UnityCliLoop
{
    public static class DynamicCodeCompilationServiceRegistration
    {
        private static readonly object SyncRoot = new object();

        [InitializeOnLoadMethod]
        private static void Register()
        {
            EnsureRegistered();
        }

        internal static void EnsureRegistered()
        {
            lock (SyncRoot)
            {
                DynamicCompilationRuntimeServicesRegistry.RegisterFactory(
                    new DynamicCompilationRuntimeServicesFactory());

                if (DynamicCompilationServiceRegistry.HasRegisteredFactory)
                {
                    return;
                }

                DynamicCompilationServiceRegistry.RegisterFactory(new DynamicCodeCompilationServiceFactory());
            }
        }
    }
}
