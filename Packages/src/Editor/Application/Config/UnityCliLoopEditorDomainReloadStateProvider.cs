using io.github.hatayama.UnityCliLoop.Domain;
using io.github.hatayama.UnityCliLoop.InternalAPIBridge;
using io.github.hatayama.UnityCliLoop.Runtime;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Application
{
    public sealed class UnityCliLoopEditorDomainReloadStateProvider : IDomainReloadStateProvider
    {
        private static volatile bool _isDomainReloadInProgress;

        public bool IsDomainReloadInProgress()
        {
            return _isDomainReloadInProgress;
        }

        public static void SetDomainReloadInProgressFromMainThread(bool isDomainReloadInProgress)
        {
            _isDomainReloadInProgress = isDomainReloadInProgress;
        }
    }

    public static class UnityCliLoopEditorDomainReloadStateRegistration
    {
        internal static void RegisterForEditorStartup()
        {
            DomainReloadStateRegistry.RegisterProvider(new UnityCliLoopEditorDomainReloadStateProvider());
        }
    }
}
