using UnityEditor;

namespace io.github.hatayama.UnityCliLoop
{
    // Centralizes production Editor startup so Unity's unordered hooks do not decide boot order.
    internal static class UnityCliLoopEditorBootstrap
    {
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            MetadataValidationEditorStartup.Initialize();
            ApplicationEditorStartup.Initialize();
            InfrastructureEditorStartup.Initialize();
            DynamicCodeCompilationServiceRegistration.EnsureRegistered();
            UnityCliLoopToolHostServicesRegistration.EnsureRegistered();
            PresentationEditorStartup.Initialize();
        }
    }
}
