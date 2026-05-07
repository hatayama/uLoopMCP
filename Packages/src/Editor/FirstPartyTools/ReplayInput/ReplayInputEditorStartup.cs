using io.github.hatayama.UnityCliLoop.Runtime;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
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
