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

                ToolSettingsRepository toolSettingsRepository = new();
                UnityCliLoopEditorSettingsRepository editorSettingsRepository = new();
                ULoopSettingsRepository uLoopSettingsRepository = new();
                ToolSettings.RegisterService(toolSettingsRepository);
                UnityCliLoopEditorSettings.RegisterService(editorSettingsRepository);
                ULoopSettings.RegisterService(uLoopSettingsRepository);
                CompilationLockService.RegisterService(new CompilationLockFileService());
                DomainReloadDetectionService.RegisterService(new DomainReloadDetectionFileService());
                UnityCliLoopToolRegistrar.RegisterService(new UnityCliLoopToolRegistrarService(
                    new SkillInstallLayoutInternalToolNameProvider()));
                SkillSetupApplicationFacade.RegisterService(new SkillSetupApplicationService(
                    new ToolSkillSetupService()));
                CliSetupApplicationFacade.RegisterService(new CliSetupApplicationService(
                    new CliInstallationDetector(),
                    new ProjectLocalCliInstallerService(),
                    new NativeCliInstallerService()));
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
