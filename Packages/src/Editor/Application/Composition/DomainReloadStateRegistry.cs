using System.Diagnostics;

using io.github.hatayama.UnityCliLoop.Domain;
using io.github.hatayama.UnityCliLoop.InternalAPIBridge;
using io.github.hatayama.UnityCliLoop.Runtime;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Application
{
    public static class DomainReloadStateRegistry
    {
        private static readonly object SyncRoot = new object();
        private static IDomainReloadStateProvider _provider;

        public static void RegisterProvider(IDomainReloadStateProvider provider)
        {
            Debug.Assert(provider != null, "provider must not be null");

            lock (SyncRoot)
            {
                _provider = provider;
            }
        }

        public static bool IsDomainReloadInProgress()
        {
            IDomainReloadStateProvider provider;
            lock (SyncRoot)
            {
                provider = _provider;
            }

            if (provider == null)
            {
                return false;
            }

            return provider.IsDomainReloadInProgress();
        }

        public static IDomainReloadStateProvider SwapProviderForTests(IDomainReloadStateProvider provider)
        {
            lock (SyncRoot)
            {
                IDomainReloadStateProvider previousProvider = _provider;
                _provider = provider;
                return previousProvider;
            }
        }
    }
}
