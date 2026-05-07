using System;

namespace io.github.hatayama.UnityCliLoop
{
    internal sealed class UnityCliLoopToolHostServicesFactoryRegistry
    {
        private Func<IUnityCliLoopToolHostServices> _factory;

        public void RegisterFactory(Func<IUnityCliLoopToolHostServices> factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public IUnityCliLoopToolHostServices Create()
        {
            if (_factory == null)
            {
                throw new InvalidOperationException("UnityCliLoop tool host services factory is not registered.");
            }

            return _factory();
        }
    }

    /// <summary>
    /// Application-side factory slot for host services supplied by the composition root.
    /// </summary>
    internal static class UnityCliLoopToolHostServicesProvider
    {
        private static readonly UnityCliLoopToolHostServicesFactoryRegistry RegistryValue =
            new UnityCliLoopToolHostServicesFactoryRegistry();

        public static void RegisterFactory(Func<IUnityCliLoopToolHostServices> factory)
        {
            RegistryValue.RegisterFactory(factory);
        }

        public static IUnityCliLoopToolHostServices Create()
        {
            return RegistryValue.Create();
        }
    }
}
