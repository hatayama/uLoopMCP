#if UNITYCLILOOP_HAS_ROSLYN
using NUnit.Framework;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.DynamicCodeToolTests
{
    public class DynamicCodeExecutorFactoryTests
    {
        private IDynamicCompilationServiceFactory _previousFactory;

        [SetUp]
        public void SetUp()
        {
            DynamicCodeCompilationServiceRegistration.EnsureRegistered();
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
