using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    [TestFixture]
    public class DynamicCodeExecutorTests
    {
        [Test]
        public async Task ExecuteCodeAsync_WhenCompilationIsCancelled_ShouldReturnNeutralCancelledMessage()
        {
            CancelledCompilationService compiler = new CancelledCompilationService();
            CountingCompiledCommandInvoker invoker = new CountingCompiledCommandInvoker();
            DynamicCodeExecutor executor = new DynamicCodeExecutor(
                compiler,
                invoker,
                new DynamicCodeSourcePreparationService());

            ExecutionResult result = await executor.ExecuteCodeAsync(
                "return 1;",
                cancellationToken: new CancellationToken(canceled: true));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo(McpConstants.ERROR_MESSAGE_EXECUTION_CANCELLED));
            Assert.That(result.Logs, Contains.Item("Execution cancelled"));
            Assert.That(invoker.ExecuteAsyncCallCount, Is.EqualTo(0));
        }

        [Test]
        public async Task ExecuteCodeAsync_WhenCompileOnlyCompilationHasNullTimings_ShouldReturnEmptyTimingList()
        {
            NullTimingCompilationService compiler = NullTimingCompilationService.CreateSuccessful();
            CountingCompiledCommandInvoker invoker = new CountingCompiledCommandInvoker();
            DynamicCodeExecutor executor = new DynamicCodeExecutor(
                compiler,
                invoker,
                new DynamicCodeSourcePreparationService());

            ExecutionResult result = await executor.ExecuteCodeAsync(
                "return 1;",
                cancellationToken: CancellationToken.None,
                compileOnly: true);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Timings, Is.Empty);
            Assert.That(invoker.ExecuteAsyncCallCount, Is.EqualTo(0));
        }

        [Test]
        public async Task ExecuteCodeAsync_WhenCompilationFailureHasNullTimings_ShouldReturnEmptyTimingList()
        {
            NullTimingCompilationService compiler = NullTimingCompilationService.CreateFailed();
            CountingCompiledCommandInvoker invoker = new CountingCompiledCommandInvoker();
            DynamicCodeExecutor executor = new DynamicCodeExecutor(
                compiler,
                invoker,
                new DynamicCodeSourcePreparationService());

            ExecutionResult result = await executor.ExecuteCodeAsync(
                "return 1;",
                cancellationToken: CancellationToken.None);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("Compilation error occurred"));
            Assert.That(result.Timings, Is.Empty);
            Assert.That(invoker.ExecuteAsyncCallCount, Is.EqualTo(0));
        }

        private sealed class CancelledCompilationService : IDynamicCompilationService
        {
            public Task<CompilationResult> CompileAsync(CompilationRequest request, CancellationToken ct = default)
            {
                throw new OperationCanceledException(ct);
            }
        }

        private sealed class CountingCompiledCommandInvoker : ICompiledCommandInvoker
        {
            public int ExecuteAsyncCallCount { get; private set; }

            public Task<ExecutionResult> ExecuteAsync(ExecutionContext context)
            {
                ExecuteAsyncCallCount++;
                return Task.FromResult(new ExecutionResult { Success = true });
            }
        }

        private sealed class NullTimingCompilationService : IDynamicCompilationService
        {
            private readonly CompilationResult _result;

            private NullTimingCompilationService(CompilationResult result)
            {
                _result = result;
            }

            public static NullTimingCompilationService CreateSuccessful()
            {
                return new NullTimingCompilationService(new CompilationResult
                {
                    Success = true,
                    Timings = null
                });
            }

            public static NullTimingCompilationService CreateFailed()
            {
                return new NullTimingCompilationService(new CompilationResult
                {
                    Success = false,
                    Timings = null
                });
            }

            public Task<CompilationResult> CompileAsync(CompilationRequest request, CancellationToken ct = default)
            {
                return Task.FromResult(_result);
            }
        }
    }
}
