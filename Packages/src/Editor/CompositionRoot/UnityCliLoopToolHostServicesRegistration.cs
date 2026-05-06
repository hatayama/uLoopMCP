using UnityEditor;

namespace io.github.hatayama.UnityCliLoop
{
    public static class UnityCliLoopToolHostServicesRegistration
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
                UnityCliLoopToolHostServicesProvider.RegisterFactory(CreateHostServices);
                UnityCliLoopServerInstanceFactoryRegistry.RegisterFactory(
                    new UnityCliLoopBridgeServerInstanceFactory());
                UnityCliLoopServerLifecycleRegistry.RegisterSource(
                    new UnityCliLoopBridgeServerLifecycleSource());
            }
        }

        private static IUnityCliLoopToolHostServices CreateHostServices()
        {
            return new UnityCliLoopToolHostServices();
        }
    }
}
