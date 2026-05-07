
namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    // Keeps Console log capture initialization inside the get-logs tool module.
    internal static class GetLogsEditorStartup
    {
        internal static void Initialize()
        {
            LogGetter.InitializeForEditorStartup();
        }
    }
}
