namespace io.github.hatayama.UnityCliLoop
{
    // Groups application-layer Editor startup callbacks behind one facade for the composition root.
    internal static class ApplicationEditorStartup
    {
        internal static void Initialize()
        {
            UnityCliLoopEditorSettings.ScheduleSettingsFileRecoveryForEditorStartup();
            UnityCliLoopEditorDomainReloadStateRegistration.RegisterForEditorStartup();
            MainThreadSwitcher.InitializeForEditorStartup();
            EditorDelayManager.InitializeForEditorStartup();
            DomainReloadDetectionService.RegisterForEditorStartup();
            CompilationLockService.RegisterForEditorStartup();
        }
    }
}
