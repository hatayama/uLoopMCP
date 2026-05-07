namespace io.github.hatayama.UnityCliLoop
{
    // Groups application-layer Editor startup callbacks behind one facade for the composition root.
    internal static class ApplicationEditorStartup
    {
        internal static void Initialize()
        {
            UnityCliLoopEditorSettings.RecoverSettingsFileForEditorStartup();
            UnityCliLoopEditorDomainReloadStateRegistration.RegisterForEditorStartup();
            MainThreadSwitcher.InitializeForEditorStartup();
            EditorDelayManager.InitializeForEditorStartup();
            DomainReloadDetectionService.RegisterForEditorStartup();
            CompilationLockService.RegisterForEditorStartup();
            LogGetter.InitializeForEditorStartup();
#if ULOOP_HAS_INPUT_SYSTEM
            InputRecorder.InitializeForEditorStartup();
            InputReplayer.InitializeForEditorStartup();
            KeyboardKeyState.InitializeForEditorStartup();
            MouseInputState.InitializeForEditorStartup();
            MouseDragState.InitializeForEditorStartup();
#endif
            ProjectLocalCliAutoInstaller.ScheduleForEditorStartup();
        }
    }
}
