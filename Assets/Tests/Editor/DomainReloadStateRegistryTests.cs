using NUnit.Framework;

using io.github.hatayama.UnityCliLoop.Application;
using io.github.hatayama.UnityCliLoop.CompositionRoot;
using io.github.hatayama.UnityCliLoop.Domain;
using io.github.hatayama.UnityCliLoop.FirstPartyTools;
using io.github.hatayama.UnityCliLoop.Infrastructure;
using io.github.hatayama.UnityCliLoop.Presentation;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    public class DomainReloadStateRegistryTests
    {
        private IDomainReloadStateProvider _previousProvider;

        [SetUp]
        public void SetUp()
        {
            UnityCliLoopEditorDomainReloadStateProvider.SetDomainReloadInProgressFromMainThread(false);
            _previousProvider = DomainReloadStateRegistry.SwapProviderForTests(null);
        }

        [TearDown]
        public void TearDown()
        {
            UnityCliLoopEditorDomainReloadStateProvider.SetDomainReloadInProgressFromMainThread(false);
            DomainReloadStateRegistry.SwapProviderForTests(_previousProvider);
        }

        [Test]
        public void IsDomainReloadInProgress_ReturnsFalse_WhenProviderIsMissing()
        {
            bool result = DomainReloadStateRegistry.IsDomainReloadInProgress();

            Assert.That(result, Is.False);
        }

        [Test]
        public void IsDomainReloadInProgress_ReturnsProviderValue_WhenProviderIsRegistered()
        {
            DomainReloadStateRegistry.RegisterProvider(new StubDomainReloadStateProvider(true));

            bool result = DomainReloadStateRegistry.IsDomainReloadInProgress();

            Assert.That(result, Is.True);
        }

        [Test]
        public void Provider_ReturnsUpdatedInMemoryFlag()
        {
            UnityCliLoopEditorDomainReloadStateProvider provider = new();

            try
            {
                Assert.That(provider.IsDomainReloadInProgress(), Is.False);

                UnityCliLoopEditorDomainReloadStateProvider.SetDomainReloadInProgressFromMainThread(true);
                Assert.That(provider.IsDomainReloadInProgress(), Is.True);

                UnityCliLoopEditorDomainReloadStateProvider.SetDomainReloadInProgressFromMainThread(false);
                Assert.That(provider.IsDomainReloadInProgress(), Is.False);
            }
            finally
            {
                UnityCliLoopEditorDomainReloadStateProvider.SetDomainReloadInProgressFromMainThread(false);
            }
        }

        private sealed class StubDomainReloadStateProvider : IDomainReloadStateProvider
        {
            private readonly bool _isDomainReloadInProgress;

            public StubDomainReloadStateProvider(bool isDomainReloadInProgress)
            {
                _isDomainReloadInProgress = isDomainReloadInProgress;
            }

            public bool IsDomainReloadInProgress()
            {
                return _isDomainReloadInProgress;
            }
        }
    }
}
