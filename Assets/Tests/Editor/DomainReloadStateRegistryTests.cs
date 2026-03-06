using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP.Tests.Editor
{
    public class DomainReloadStateRegistryTests
    {
        private IDomainReloadStateProvider _previousProvider;

        [SetUp]
        public void SetUp()
        {
            _previousProvider = DomainReloadStateRegistry.SwapProviderForTests(null);
        }

        [TearDown]
        public void TearDown()
        {
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
            McpEditorDomainReloadStateProvider.SetDomainReloadInProgressFromMainThread(true);
            McpEditorDomainReloadStateProvider provider = new McpEditorDomainReloadStateProvider();

            bool result = provider.IsDomainReloadInProgress();

            Assert.That(result, Is.True);

            McpEditorDomainReloadStateProvider.SetDomainReloadInProgressFromMainThread(false);
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
