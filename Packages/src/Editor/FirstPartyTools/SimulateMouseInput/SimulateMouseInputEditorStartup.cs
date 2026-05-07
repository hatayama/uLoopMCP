using io.github.hatayama.UnityCliLoop.Runtime;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
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
