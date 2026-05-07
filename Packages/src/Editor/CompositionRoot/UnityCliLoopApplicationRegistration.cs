namespace io.github.hatayama.UnityCliLoop
{
    public static class UnityCliLoopApplicationRegistration
    {
        private static readonly object SyncRoot = new object();
        private static bool IsRegistered;

        internal static void EnsureRegistered()
        {
            lock (SyncRoot)
            {
                if (IsRegistered)
                {
                    return;
                }

                UnityCliLoopToolRegistrar.RegisterService(CreateToolRegistrarService());
                CliSetupApplicationFacade.RegisterService(CreateCliSetupApplicationService());
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
                controllerService.InitializeForEditorStartup();
                IsRegistered = true;
            }
        }

        private static UnityCliLoopToolRegistrarService CreateToolRegistrarService()
        {
            return new UnityCliLoopToolRegistrarService();
        }

        private static CliSetupApplicationService CreateCliSetupApplicationService()
        {
            return new CliSetupApplicationService(new CliInstallationDetector());
        }
    }
}
