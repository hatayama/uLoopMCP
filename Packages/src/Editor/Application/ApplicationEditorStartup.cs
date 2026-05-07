using io.github.hatayama.UnityCliLoop.Domain;
using io.github.hatayama.UnityCliLoop.InternalAPIBridge;
using io.github.hatayama.UnityCliLoop.Runtime;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Application
{
    // Groups application-layer Editor startup callbacks behind one facade for the composition root.
    internal static class ApplicationEditorStartup
    {
        internal static void Initialize()
        {
            UnityCliLoopEditorDomainReloadStateRegistration.RegisterForEditorStartup();
            MainThreadSwitcher.InitializeForEditorStartup();
            EditorDelayManager.InitializeForEditorStartup();
            DomainReloadDetectionService.RegisterForEditorStartup();
        }
    }
}
