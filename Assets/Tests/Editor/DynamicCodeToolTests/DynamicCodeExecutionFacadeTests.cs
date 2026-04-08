using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using io.github.hatayama.uLoopMCP.Factory;
using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    [TestFixture]
    public class DynamicCodeExecutionFacadeTests
    {
        [Test]
        public async Task ExecuteAsync_WhenSameSecurityLevelUsedTwice_ShouldReuseExecutor()
        {
            FakeDynamicCodeExecutorProvider provider = new FakeDynamicCodeExecutorProvider();
            using DynamicCodeExecutorPool pool = new DynamicCodeExecutorPool(provider);
            using DynamicCodeExecutionFacade facade = new DynamicCodeExecutionFacade(
                new FakeCompiledAssemblyBuilder(true),
                pool);

            DynamicCodeExecutionRequest firstRequest = CreateRequest(
                DynamicCodeSecurityLevel.Restricted,
                "return 1;");
            DynamicCodeExecutionRequest secondRequest = CreateRequest(
                DynamicCodeSecurityLevel.Restricted,
                "return 2;");

            await facade.ExecuteAsync(firstRequest, CancellationToken.None);
            await facade.ExecuteAsync(secondRequest, CancellationToken.None);

            Assert.That(provider.CreateCallsBySecurityLevel[DynamicCodeSecurityLevel.Restricted], Is.EqualTo(1));
        }

        [Test]
        public async Task ExecuteAsync_WhenSecurityLevelChanges_ShouldCreateSeparateExecutors()
        {
            FakeDynamicCodeExecutorProvider provider = new FakeDynamicCodeExecutorProvider();
            using DynamicCodeExecutorPool pool = new DynamicCodeExecutorPool(provider);
            using DynamicCodeExecutionFacade facade = new DynamicCodeExecutionFacade(
                new FakeCompiledAssemblyBuilder(true),
                pool);

            await facade.ExecuteAsync(
                CreateRequest(DynamicCodeSecurityLevel.Restricted, "return 1;"),
                CancellationToken.None);
            await facade.ExecuteAsync(
                CreateRequest(DynamicCodeSecurityLevel.FullAccess, "return 2;"),
                CancellationToken.None);

            Assert.That(provider.CreateCallsBySecurityLevel[DynamicCodeSecurityLevel.Restricted], Is.EqualTo(1));
            Assert.That(provider.CreateCallsBySecurityLevel[DynamicCodeSecurityLevel.FullAccess], Is.EqualTo(1));
        }

        [Test]
        [Timeout(3000)]
        public async Task ExecuteAsync_WhenExecutionIsAlreadyRunning_ShouldFailFast()
        {
            BlockingDynamicCodeExecutorProvider provider = new BlockingDynamicCodeExecutorProvider();
            using DynamicCodeExecutorPool pool = new DynamicCodeExecutorPool(provider);
            using DynamicCodeExecutionFacade facade = new DynamicCodeExecutionFacade(
                new FakeCompiledAssemblyBuilder(true),
                pool);
            using CancellationTokenSource firstExecutionCts = new CancellationTokenSource(3000);
            using CancellationTokenSource secondExecutionCts = new CancellationTokenSource(300);

            Task<ExecutionResult> firstExecutionTask = facade.ExecuteAsync(
                CreateRequest(DynamicCodeSecurityLevel.Restricted, "return 1;"),
                firstExecutionCts.Token);

            await provider.ExecutionStarted.Task;

            ExecutionResult secondResult = await facade.ExecuteAsync(
                CreateRequest(DynamicCodeSecurityLevel.Restricted, "return 2;"),
                secondExecutionCts.Token);

            Assert.That(secondResult.Success, Is.False);
            Assert.That(secondResult.ErrorMessage, Is.EqualTo(McpConstants.ERROR_MESSAGE_EXECUTION_IN_PROGRESS));

            provider.AllowCompletion.SetResult(new ExecutionResult
            {
                Success = true,
                Result = "done"
            });

            ExecutionResult firstResult = await firstExecutionTask;
            Assert.That(firstResult.Success, Is.True);
        }

        [Test]
        public void Dispose_WhenExecutorsWereCreated_ShouldDisposeCachedExecutors()
        {
            FakeDynamicCodeExecutorProvider provider = new FakeDynamicCodeExecutorProvider();
            using DynamicCodeExecutorPool pool = new DynamicCodeExecutorPool(provider);
            DynamicCodeExecutionFacade facade = new DynamicCodeExecutionFacade(
                new FakeCompiledAssemblyBuilder(true),
                pool);

            Assert.DoesNotThrowAsync(async () =>
            {
                await facade.ExecuteAsync(
                    CreateRequest(DynamicCodeSecurityLevel.Restricted, "return 1;"),
                    CancellationToken.None);
            });

            facade.Dispose();

            Assert.That(provider.CreatedExecutors[0].DisposeCallCount, Is.EqualTo(1));
        }

        [Test]
        public void SupportsAutoPrewarm_ShouldDelegateToAssemblyBuilderCapability()
        {
            FakeDynamicCodeExecutorProvider provider = new FakeDynamicCodeExecutorProvider();
            using DynamicCodeExecutorPool pool = new DynamicCodeExecutorPool(provider);
            using DynamicCodeExecutionFacade supported = new DynamicCodeExecutionFacade(
                new FakeCompiledAssemblyBuilder(true),
                pool);
            using DynamicCodeExecutionFacade unsupported = new DynamicCodeExecutionFacade(
                new FakeCompiledAssemblyBuilder(false),
                new DynamicCodeExecutorPool(provider));

            Assert.That(supported.SupportsAutoPrewarm(), Is.True);
            Assert.That(unsupported.SupportsAutoPrewarm(), Is.False);
        }

        private static DynamicCodeExecutionRequest CreateRequest(
            DynamicCodeSecurityLevel securityLevel,
            string code)
        {
            return new DynamicCodeExecutionRequest
            {
                SecurityLevel = securityLevel,
                Code = code,
                ClassName = "FacadeTestCommand"
            };
        }

        private sealed class FakeDynamicCodeExecutorProvider : IDynamicCodeExecutorProvider
        {
            public Dictionary<DynamicCodeSecurityLevel, int> CreateCallsBySecurityLevel { get; } = new();

            public List<FakeDynamicCodeExecutor> CreatedExecutors { get; } = new();

            public IDynamicCodeExecutor Create(DynamicCodeSecurityLevel securityLevel)
            {
                if (!CreateCallsBySecurityLevel.ContainsKey(securityLevel))
                {
                    CreateCallsBySecurityLevel[securityLevel] = 0;
                }

                CreateCallsBySecurityLevel[securityLevel]++;

                FakeDynamicCodeExecutor executor = new FakeDynamicCodeExecutor();
                CreatedExecutors.Add(executor);
                return executor;
            }
        }

        private sealed class BlockingDynamicCodeExecutorProvider : IDynamicCodeExecutorProvider
        {
            public TaskCompletionSource<bool> ExecutionStarted { get; } =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource<ExecutionResult> AllowCompletion { get; } =
                new TaskCompletionSource<ExecutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            public IDynamicCodeExecutor Create(DynamicCodeSecurityLevel securityLevel)
            {
                return new BlockingDynamicCodeExecutor(ExecutionStarted, AllowCompletion);
            }
        }

        private sealed class FakeDynamicCodeExecutor : IDynamicCodeExecutor
        {
            public int DisposeCallCount { get; private set; }

            public Task<ExecutionResult> ExecuteCodeAsync(
                string code,
                string className = DynamicCodeConstants.DEFAULT_CLASS_NAME,
                object[] parameters = null,
                CancellationToken cancellationToken = default,
                bool compileOnly = false)
            {
                return Task.FromResult(new ExecutionResult
                {
                    Success = true,
                    Result = code
                });
            }

            public ExecutionStatistics GetStatistics()
            {
                return new ExecutionStatistics();
            }

            public void Dispose()
            {
                DisposeCallCount++;
            }
        }

        private sealed class BlockingDynamicCodeExecutor : IDynamicCodeExecutor
        {
            private readonly TaskCompletionSource<bool> _executionStarted;
            private readonly TaskCompletionSource<ExecutionResult> _allowCompletion;

            public BlockingDynamicCodeExecutor(
                TaskCompletionSource<bool> executionStarted,
                TaskCompletionSource<ExecutionResult> allowCompletion)
            {
                _executionStarted = executionStarted;
                _allowCompletion = allowCompletion;
            }

            public async Task<ExecutionResult> ExecuteCodeAsync(
                string code,
                string className = DynamicCodeConstants.DEFAULT_CLASS_NAME,
                object[] parameters = null,
                CancellationToken cancellationToken = default,
                bool compileOnly = false)
            {
                _executionStarted.TrySetResult(true);
                using CancellationTokenRegistration registration = cancellationToken.Register(
                    () => _allowCompletion.TrySetCanceled(cancellationToken));
                return await _allowCompletion.Task;
            }

            public ExecutionStatistics GetStatistics()
            {
                return new ExecutionStatistics();
            }

            public void Dispose()
            {
            }
        }

        private sealed class FakeCompiledAssemblyBuilder : ICompiledAssemblyBuilder
        {
            private readonly bool _supportsAutoPrewarm;

            public FakeCompiledAssemblyBuilder(bool supportsAutoPrewarm)
            {
                _supportsAutoPrewarm = supportsAutoPrewarm;
            }

            public bool SupportsAutoPrewarm()
            {
                return _supportsAutoPrewarm;
            }

            public Task<CompiledAssemblyBuildResult> BuildAsync(
                DynamicCompilationPlan plan,
                CancellationToken ct = default)
            {
                throw new NotSupportedException();
            }
        }
    }
}
