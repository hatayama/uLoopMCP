namespace io.github.hatayama.UnityCliLoop
{
    // Keeps bundled tool initialization inside the bundled-tool assembly.
    internal static class FirstPartyToolsEditorStartup
    {
        internal static void Initialize()
        {
            ExecuteDynamicCodeEditorStartup.Initialize();
            GetLogsEditorStartup.Initialize();
#if ULOOP_HAS_INPUT_SYSTEM
            RecordInputEditorStartup.Initialize();
            ReplayInputEditorStartup.Initialize();
            SimulateKeyboardEditorStartup.Initialize();
            SimulateMouseInputEditorStartup.Initialize();
            SimulateMouseUiEditorStartup.Initialize();
#endif
        }
    }
}
