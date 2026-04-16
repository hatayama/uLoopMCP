using System.Threading.Tasks;
using io.github.hatayama.uLoopMCP.Factory;
using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    [TestFixture]
    public class DynamicCodeCompilationServiceRegistrationTests
    {
        private IDynamicCompilationServiceFactory _previousFactory;

        [SetUp]
        public void SetUp()
        {
            _previousFactory = DynamicCompilationServiceRegistry.SwapFactoryForTests(null);
            DynamicCodeServices.ResetStateForTests();
        }

        [TearDown]
        public async Task TearDown()
        {
            DynamicCodeServices.ResetServerScopedServices();
            Task drainTask = DynamicCodeServices.GetServerScopedDrainTaskForTests();
            await DynamicCodeServices.AwaitDrainTaskAsync(drainTask);
            DynamicCodeServices.ResetStateForTests();
            DynamicCompilationServiceRegistry.SwapFactoryForTests(_previousFactory);
        }

        [Test]
        public void EnsureRegistered_WhenRegistryIsMissing_ShouldRegisterFactory()
        {
            Assert.That(DynamicCompilationServiceRegistry.HasRegisteredFactory, Is.False);

            DynamicCodeCompilationServiceRegistration.EnsureRegistered();

            Assert.That(DynamicCompilationServiceRegistry.HasRegisteredFactory, Is.True);
        }

        [Test]
        public async Task GetExecuteDynamicCodeUseCaseAsync_WhenRegistryIsMissing_ShouldRegisterFactory()
        {
            Assert.That(DynamicCompilationServiceRegistry.HasRegisteredFactory, Is.False);

            IExecuteDynamicCodeUseCase useCase = await DynamicCodeServices.GetExecuteDynamicCodeUseCaseAsync();
            IDynamicCodeExecutor executor = DynamicCodeExecutorFactory.Create(DynamicCodeSecurityLevel.Restricted);

            Assert.That(useCase, Is.Not.Null);
            Assert.That(DynamicCompilationServiceRegistry.HasRegisteredFactory, Is.True);
            Assert.That(executor, Is.Not.TypeOf<DynamicCodeExecutorStub>());
        }

        [Test]
        public async Task GetPrewarmDynamicCodeUseCaseAsync_WhenRegistryIsMissing_ShouldRegisterFactory()
        {
            Assert.That(DynamicCompilationServiceRegistry.HasRegisteredFactory, Is.False);

            IPrewarmDynamicCodeUseCase useCase = await DynamicCodeServices.GetPrewarmDynamicCodeUseCaseAsync();
            IDynamicCodeExecutor executor = DynamicCodeExecutorFactory.Create(DynamicCodeSecurityLevel.Restricted);

            Assert.That(useCase, Is.Not.Null);
            Assert.That(DynamicCompilationServiceRegistry.HasRegisteredFactory, Is.True);
            Assert.That(executor, Is.Not.TypeOf<DynamicCodeExecutorStub>());
        }
    }
}
