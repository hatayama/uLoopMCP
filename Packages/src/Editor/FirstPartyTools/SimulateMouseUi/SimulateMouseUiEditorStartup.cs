namespace io.github.hatayama.UnityCliLoop
{
    // Keeps UI mouse simulation drag recovery inside the simulate-mouse-ui tool module.
    internal static class SimulateMouseUiEditorStartup
    {
        internal static void Initialize()
        {
            MouseDragState.InitializeForEditorStartup();
        }
    }
}
