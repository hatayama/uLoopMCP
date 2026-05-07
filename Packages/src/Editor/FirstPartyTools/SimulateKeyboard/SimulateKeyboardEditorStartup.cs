
namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    // Keeps keyboard simulation state recovery inside the simulate-keyboard tool module.
    internal static class SimulateKeyboardEditorStartup
    {
        internal static void Initialize()
        {
            KeyboardKeyState.InitializeForEditorStartup();
        }
    }
}
