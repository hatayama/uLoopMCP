#if UNITYCLILOOP_HAS_ROSLYN
using NUnit.Framework;

using io.github.hatayama.UnityCliLoop.Application;
using io.github.hatayama.UnityCliLoop.CompositionRoot;
using io.github.hatayama.UnityCliLoop.Domain;
using io.github.hatayama.UnityCliLoop.FirstPartyTools;
using io.github.hatayama.UnityCliLoop.Infrastructure;
using io.github.hatayama.UnityCliLoop.Presentation;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.DynamicCodeToolTests
{
    public class DynamicCodeExecutorFactoryTests
    {
        private IDynamicCompilationServiceFactory _previousFactory;

        [SetUp]
        public void SetUp()
        {
            _previousFactory = DynamicCodeServices.SwapCompilationFactoryForTests(null);
        }

        [TearDown]
        public void TearDown()
        {
            DynamicCodeServices.SwapCompilationFactoryForTests(_previousFactory);
        }

        [Test]
        public void Create_ReturnsStub_WhenCompilationProviderIsMissing()
        {
            IDynamicCodeExecutor executor = Factory.DynamicCodeExecutorFactory.Create(DynamicCodeSecurityLevel.Restricted);

            Assert.That(executor, Is.TypeOf<DynamicCodeExecutorStub>());
        }
    }
}
#endif
