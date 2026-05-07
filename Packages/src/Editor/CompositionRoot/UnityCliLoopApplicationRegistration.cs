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

                UnityCliLoopToolRegistrar.RegisterService(new UnityCliLoopToolRegistrarService());
                CliSetupApplicationFacade.RegisterService(new CliSetupApplicationService(new CliInstallationDetector()));
                UnityCliLoopBridgeServerInstanceFactory serverFactory = new();
                UnityCliLoopServerLifecycleRegistryService lifecycleRegistry = new();
                lifecycleRegistry.RegisterSource(serverFactory);
                UnityCliLoopServerControllerService controllerService = new(serverFactory, lifecycleRegistry);
                UnityCliLoopServerApplicationService applicationService = new(controllerService);
                UnityCliLoopServerApplicationFacade.RegisterService(applicationService);
                controllerService.InitializeForEditorStartup();
                IsRegistered = true;
            }
        }
    }
}
