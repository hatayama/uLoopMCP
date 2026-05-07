using UnityEditor;

namespace io.github.hatayama.UnityCliLoop
{
    // Centralizes production Editor startup so Unity's unordered hooks do not decide boot order.
    internal static class UnityCliLoopEditorBootstrap
    {
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            UnityCliLoopApplicationRegistration.EnsureRegistered();
            ApplicationEditorStartup.Initialize();
            FirstPartyToolsEditorStartup.Initialize();
            InfrastructureEditorStartup.Initialize();
            PresentationEditorStartup.Initialize();
        }
    }
}
