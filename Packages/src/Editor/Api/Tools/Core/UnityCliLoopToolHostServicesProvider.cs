using System;

namespace io.github.hatayama.UnityCliLoop
{
    /// <summary>
    /// Application-side factory slot for host services supplied by the composition root.
    /// </summary>
    internal static class UnityCliLoopToolHostServicesProvider
    {
        private static Func<IUnityCliLoopToolHostServices> _factory;

        public static void RegisterFactory(Func<IUnityCliLoopToolHostServices> factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public static IUnityCliLoopToolHostServices Create()
        {
            if (_factory == null)
            {
                throw new InvalidOperationException("UnityCliLoop tool host services factory is not registered.");
            }

            return _factory();
        }
    }
}
