namespace io.github.hatayama.UnityCliLoop
{
    // Keeps replay state recovery inside the replay-input tool module.
    internal static class ReplayInputEditorStartup
    {
        internal static void Initialize()
        {
            InputReplayer.InitializeForEditorStartup();
        }
    }
}
