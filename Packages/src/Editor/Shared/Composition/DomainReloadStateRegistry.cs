using System.Diagnostics;

namespace io.github.hatayama.uLoopMCP
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
            lock (SyncRoot)
            {
                if (_provider == null)
                {
                    return false;
                }

                return _provider.IsDomainReloadInProgress();
            }
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
