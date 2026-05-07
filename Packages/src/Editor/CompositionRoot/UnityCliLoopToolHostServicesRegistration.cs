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

                DynamicCodeServicesRegistry dynamicCodeServices =
                    DynamicCodeCompilationServiceRegistration.EnsureRegistered();
                UnityCliLoopToolRegistrar.RegisterService(CreateToolRegistrarService(dynamicCodeServices));
                CliSetupApplicationFacade.RegisterService(CreateCliSetupApplicationService());
                UnityCliLoopBridgeServerInstanceFactory serverFactory =
                    new UnityCliLoopBridgeServerInstanceFactory();
                UnityCliLoopServerLifecycleRegistryService lifecycleRegistry =
                    new UnityCliLoopServerLifecycleRegistryService();
                lifecycleRegistry.RegisterSource(serverFactory);
                UnityCliLoopServerControllerService controllerService =
                    new UnityCliLoopServerControllerService(serverFactory, lifecycleRegistry, dynamicCodeServices);
                UnityCliLoopServerApplicationService applicationService =
                    new UnityCliLoopServerApplicationService(controllerService);
                UnityCliLoopServerApplicationFacade.RegisterService(applicationService);
                controllerService.InitializeOnLoad();
                IsRegistered = true;
            }
        }

        private static IUnityCliLoopToolHostServices CreateHostServices(
            DynamicCodeServicesRegistry dynamicCodeServices)
        {
            return new UnityCliLoopToolHostServices(dynamicCodeServices);
        }

        private static UnityCliLoopToolRegistrarService CreateToolRegistrarService(
            DynamicCodeServicesRegistry dynamicCodeServices)
        {
            return new UnityCliLoopToolRegistrarService(() => CreateHostServices(dynamicCodeServices));
        }

        private static CliSetupApplicationService CreateCliSetupApplicationService()
        {
            return new CliSetupApplicationService(new CliInstallationDetector());
        }
    }
}
