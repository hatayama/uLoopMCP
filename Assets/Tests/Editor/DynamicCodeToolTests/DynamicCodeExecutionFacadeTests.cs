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
        [Timeout(3000)]
        public async Task ExecuteAsync_WhenBackgroundPrewarmIsRunning_ShouldCancelItAndRunForegroundRequest()
        {
            SequencedBlockingDynamicCodeExecutorProvider provider = new SequencedBlockingDynamicCodeExecutorProvider();
            using DynamicCodeExecutorPool pool = new DynamicCodeExecutorPool(provider);
            using DynamicCodeExecutionFacade facade = new DynamicCodeExecutionFacade(
                new FakeCompiledAssemblyBuilder(true),
                pool);

            DynamicCodeExecutionRequest prewarmRequest = CreateRequest(
                DynamicCodeSecurityLevel.Restricted,
                "return 1;");
            prewarmRequest.YieldToForegroundRequests = true;
            Task<(bool Entered, ExecutionResult Result)> prewarmTask = facade.TryExecuteIfIdleAsync(
                prewarmRequest,
                CancellationToken.None);

            await provider.FirstExecutionStarted.Task;

            Task<ExecutionResult> foregroundTask = facade.ExecuteAsync(
                CreateRequest(DynamicCodeSecurityLevel.Restricted, "return 2;"),
                CancellationToken.None);

            await provider.SecondExecutionStarted.Task;
            provider.CompleteSecondExecution(new ExecutionResult
            {
                Success = true,
                Result = "foreground"
            });

            ExecutionResult foregroundResult = await foregroundTask;
            (bool entered, ExecutionResult result) = await prewarmTask;

            Assert.That(entered, Is.True);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo(McpConstants.ERROR_MESSAGE_EXECUTION_CANCELLED));
            Assert.That(foregroundResult.Success, Is.True);
            Assert.That(foregroundResult.Result, Is.EqualTo("foreground"));
        }

        [Test]
        [Timeout(3000)]
        public async Task ExecuteAsync_WhenForegroundArrivesAfterBackgroundStatePublished_ShouldPreemptPrewarm()
        {
            SequencedBlockingDynamicCodeExecutorProvider provider = new SequencedBlockingDynamicCodeExecutorProvider();
            using DynamicCodeExecutorPool pool = new DynamicCodeExecutorPool(provider);
            using DynamicCodeExecutionFacade facade = new DynamicCodeExecutionFacade(
                new FakeCompiledAssemblyBuilder(true),
                pool);
            TaskCompletionSource<bool> backgroundStatePublished =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> allowBackgroundToProceed =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            DynamicCodeExecutionFacade.AfterBackgroundExecutionStatePublishedForTests = async () =>
            {
                backgroundStatePublished.TrySetResult(true);
                await allowBackgroundToProceed.Task;
            };

            try
            {
                DynamicCodeExecutionRequest prewarmRequest = CreateRequest(
                    DynamicCodeSecurityLevel.Restricted,
                    "return 1;");
                prewarmRequest.YieldToForegroundRequests = true;
                Task<(bool Entered, ExecutionResult Result)> prewarmTask = facade.TryExecuteIfIdleAsync(
                    prewarmRequest,
                    CancellationToken.None);

                await backgroundStatePublished.Task;

                Task<ExecutionResult> foregroundTask = facade.ExecuteAsync(
                    CreateRequest(DynamicCodeSecurityLevel.Restricted, "return 2;"),
                    CancellationToken.None);

                allowBackgroundToProceed.TrySetResult(true);

                await provider.SecondExecutionStarted.Task;
                provider.CompleteSecondExecution(new ExecutionResult
                {
                    Success = true,
                    Result = "foreground"
                });

                ExecutionResult foregroundResult = await foregroundTask;
                (bool entered, ExecutionResult result) = await prewarmTask;

                Assert.That(entered, Is.True);
                Assert.That(result.Success, Is.False);
                Assert.That(result.ErrorMessage, Is.EqualTo(McpConstants.ERROR_MESSAGE_EXECUTION_CANCELLED));
                Assert.That(foregroundResult.Success, Is.True);
                Assert.That(foregroundResult.Result, Is.EqualTo("foreground"));
            }
            finally
            {
                allowBackgroundToProceed.TrySetResult(true);
                DynamicCodeExecutionFacade.AfterBackgroundExecutionStatePublishedForTests = null;
            }
        }

        [Test]
        [Timeout(3000)]
        public async Task ExecuteAsync_WhenExecutionCompletesAfterBusyProbe_ShouldRetryBeforeReturningBusy()
        {
            BlockingDynamicCodeExecutorProvider provider = new BlockingDynamicCodeExecutorProvider();
            using DynamicCodeExecutorPool pool = new DynamicCodeExecutorPool(provider);
            using DynamicCodeExecutionFacade facade = new DynamicCodeExecutionFacade(
                new FakeCompiledAssemblyBuilder(true),
                pool);

            Task<ExecutionResult> firstExecutionTask = facade.ExecuteAsync(
                CreateRequest(DynamicCodeSecurityLevel.Restricted, "return 1;"),
                CancellationToken.None);

            await provider.ExecutionStarted.Task;

            DynamicCodeExecutionFacade.AfterBusySemaphoreProbeFailedForTests = async () =>
            {
                provider.AllowCompletion.TrySetResult(new ExecutionResult
                {
                    Success = true,
                    Result = "first"
                });
                await firstExecutionTask;
            };

            try
            {
                ExecutionResult secondResult = await facade.ExecuteAsync(
                    CreateRequest(DynamicCodeSecurityLevel.Restricted, "return 2;"),
                    CancellationToken.None);
                ExecutionResult firstResult = await firstExecutionTask;

                Assert.That(firstResult.Success, Is.True);
                Assert.That(secondResult.Success, Is.True);
                Assert.That(secondResult.ErrorMessage, Is.Null);
            }
            finally
            {
                DynamicCodeExecutionFacade.AfterBusySemaphoreProbeFailedForTests = null;
            }
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
        public void ExecuteAsync_WhenDisposeRunsAfterSemaphoreAcquire_ShouldStillDisposeCachedExecutors()
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

            DynamicCodeExecutionFacade.AfterSemaphoreEnteredForTests = facade.Dispose;

            try
            {
                Assert.That(
                    async () => await facade.ExecuteAsync(
                        CreateRequest(DynamicCodeSecurityLevel.Restricted, "return 2;"),
                        CancellationToken.None),
                    Throws.InstanceOf<ObjectDisposedException>());
            }
            finally
            {
                DynamicCodeExecutionFacade.AfterSemaphoreEnteredForTests = null;
            }

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

        [Test]
        [Timeout(3000)]
        public async Task ExecuteAsync_WhenCompileOnlyExecutionIsAlreadyRunning_ShouldFailFast()
        {
            BlockingDynamicCodeExecutorProvider provider = new BlockingDynamicCodeExecutorProvider();
            using DynamicCodeExecutorPool pool = new DynamicCodeExecutorPool(provider);
            using DynamicCodeExecutionFacade facade = new DynamicCodeExecutionFacade(
                new FakeCompiledAssemblyBuilder(true),
                pool);
            using CancellationTokenSource firstExecutionCts = new CancellationTokenSource(3000);
            using CancellationTokenSource secondExecutionCts = new CancellationTokenSource(300);

            DynamicCodeExecutionRequest firstRequest = CreateRequest(
                DynamicCodeSecurityLevel.Restricted,
                "return 1;");
            firstRequest.CompileOnly = true;
            Task<ExecutionResult> firstExecutionTask = facade.ExecuteAsync(
                firstRequest,
                firstExecutionCts.Token);

            await provider.ExecutionStarted.Task;

            DynamicCodeExecutionRequest secondRequest = CreateRequest(
                DynamicCodeSecurityLevel.Restricted,
                "return 2;");
            secondRequest.CompileOnly = true;
            ExecutionResult secondResult = await facade.ExecuteAsync(
                secondRequest,
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

        private sealed class SequencedBlockingDynamicCodeExecutorProvider : IDynamicCodeExecutorProvider
        {
            public TaskCompletionSource<bool> FirstExecutionStarted { get; } =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource<bool> SecondExecutionStarted { get; } =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<ExecutionResult> _secondExecutionCompletion =
                new TaskCompletionSource<ExecutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            public IDynamicCodeExecutor Create(DynamicCodeSecurityLevel securityLevel)
            {
                return new SequencedBlockingDynamicCodeExecutor(
                    FirstExecutionStarted,
                    SecondExecutionStarted,
                    _secondExecutionCompletion);
            }

            public void CompleteSecondExecution(ExecutionResult result)
            {
                _secondExecutionCompletion.TrySetResult(result);
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

        private sealed class SequencedBlockingDynamicCodeExecutor : IDynamicCodeExecutor
        {
            private readonly TaskCompletionSource<bool> _firstExecutionStarted;
            private readonly TaskCompletionSource<bool> _secondExecutionStarted;
            private readonly TaskCompletionSource<ExecutionResult> _secondExecutionCompletion;
            private int _executionCount;

            public SequencedBlockingDynamicCodeExecutor(
                TaskCompletionSource<bool> firstExecutionStarted,
                TaskCompletionSource<bool> secondExecutionStarted,
                TaskCompletionSource<ExecutionResult> secondExecutionCompletion)
            {
                _firstExecutionStarted = firstExecutionStarted;
                _secondExecutionStarted = secondExecutionStarted;
                _secondExecutionCompletion = secondExecutionCompletion;
            }

            public Task<ExecutionResult> ExecuteCodeAsync(
                string code,
                string className = DynamicCodeConstants.DEFAULT_CLASS_NAME,
                object[] parameters = null,
                CancellationToken cancellationToken = default,
                bool compileOnly = false)
            {
                _executionCount++;
                if (_executionCount == 1)
                {
                    _firstExecutionStarted.TrySetResult(true);
                    return WaitForCancellationAsync(cancellationToken);
                }

                _secondExecutionStarted.TrySetResult(true);
                return _secondExecutionCompletion.Task;
            }

            private static async Task<ExecutionResult> WaitForCancellationAsync(CancellationToken cancellationToken)
            {
                TaskCompletionSource<ExecutionResult> completion =
                    new TaskCompletionSource<ExecutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                using CancellationTokenRegistration registration = cancellationToken.Register(
                    () => completion.TrySetCanceled(cancellationToken));
                return await completion.Task;
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
