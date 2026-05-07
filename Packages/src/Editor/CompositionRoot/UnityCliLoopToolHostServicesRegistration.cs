using UnityEditor;

namespace io.github.hatayama.UnityCliLoop
{
    public static class UnityCliLoopToolHostServicesRegistration
    {
        private static readonly object SyncRoot = new object();
        private static bool IsRegistered;

        [InitializeOnLoadMethod]
        private static void Register()
        {
            EnsureRegistered();
        }

        internal static void EnsureRegistered()
        {
            lock (SyncRoot)
            {
                if (IsRegistered)
                {
                    return;
                }

                UnityCliLoopToolHostServicesProvider.RegisterFactory(CreateHostServices);
                UnityCliLoopBridgeServerInstanceFactory serverFactory =
                    new UnityCliLoopBridgeServerInstanceFactory();
                UnityCliLoopServerLifecycleRegistryService lifecycleRegistry =
                    new UnityCliLoopServerLifecycleRegistryService();
                lifecycleRegistry.RegisterSource(serverFactory);
                UnityCliLoopServerControllerService controllerService =
                    new UnityCliLoopServerControllerService(serverFactory, lifecycleRegistry);
                UnityCliLoopServerApplicationService applicationService =
                    new UnityCliLoopServerApplicationService(controllerService);
                UnityCliLoopServerApplicationFacade.RegisterService(applicationService);
                controllerService.InitializeOnLoad();
                IsRegistered = true;
            }
        }

        private static IUnityCliLoopToolHostServices CreateHostServices()
        {
            return new UnityCliLoopToolHostServices();
        }
    }
}
