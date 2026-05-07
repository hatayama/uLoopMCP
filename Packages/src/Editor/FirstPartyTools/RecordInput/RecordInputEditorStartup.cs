
namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    // Keeps recording state recovery inside the record-input tool module.
    internal static class RecordInputEditorStartup
    {
        internal static void Initialize()
        {
            InputRecorder.InitializeForEditorStartup();
        }
    }
}
