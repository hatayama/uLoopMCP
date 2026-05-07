namespace io.github.hatayama.UnityCliLoop
{
    // Keeps mouse input simulation state recovery inside the simulate-mouse-input tool module.
    internal static class SimulateMouseInputEditorStartup
    {
        internal static void Initialize()
        {
            MouseInputState.InitializeForEditorStartup();
        }
    }
}
