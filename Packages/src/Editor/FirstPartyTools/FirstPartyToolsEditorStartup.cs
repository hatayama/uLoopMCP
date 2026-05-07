namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    // Keeps bundled tool initialization inside the bundled-tool assembly.
    public static class FirstPartyToolsEditorStartup
    {
        public static void Initialize()
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
