using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    [TestFixture]
    public class PrewarmDynamicCodeUseCaseTests
    {
        [Test]
        public async Task RequestAsync_WhenSupportedAndWarmupSucceeds_ShouldExecuteOnce()
        {
            FakePrewarmRuntime runtime = new FakePrewarmRuntime(
                true,
                new ExecutionResult { Success = true });
            PrewarmDynamicCodeUseCase useCase = new PrewarmDynamicCodeUseCase(runtime);

            await useCase.RequestAsync();
            await useCase.RequestAsync();

            Assert.That(runtime.Requests, Has.Count.EqualTo(1));
            Assert.That(runtime.Requests[0].Code, Is.EqualTo("return null;"));
            Assert.That(runtime.Requests[0].ClassName, Is.EqualTo("DynamicCodeAutoPrewarmCommand"));
            Assert.That(runtime.Requests[0].SecurityLevel, Is.EqualTo(DynamicCodeSecurityLevel.Restricted));
            Assert.That(runtime.Requests[0].CompileOnly, Is.False);
        }

        [Test]
        public async Task RequestAsync_WhenFastPathIsUnavailable_ShouldSkipExecution()
        {
            FakePrewarmRuntime runtime = new FakePrewarmRuntime(false);
            PrewarmDynamicCodeUseCase useCase = new PrewarmDynamicCodeUseCase(runtime);

            await useCase.RequestAsync();
            await useCase.RequestAsync();

            Assert.That(runtime.Requests, Is.Empty);
        }

        [Test]
        public async Task RequestAsync_WhenWarmupFails_ShouldRetryOnNextRequest()
        {
            FakePrewarmRuntime runtime = new FakePrewarmRuntime(
                true,
                new ExecutionResult
                {
                    Success = false,
                    ErrorMessage = "warmup failed"
                },
                new ExecutionResult
                {
                    Success = true
                });
            PrewarmDynamicCodeUseCase useCase = new PrewarmDynamicCodeUseCase(runtime);

            await useCase.RequestAsync();
            await useCase.RequestAsync();

            Assert.That(runtime.Requests, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task RequestAsync_WhenRuntimeIsBusy_ShouldSkipExecution()
        {
            FakePrewarmRuntime runtime = new FakePrewarmRuntime(
                true,
                false);
            PrewarmDynamicCodeUseCase useCase = new PrewarmDynamicCodeUseCase(runtime);

            await useCase.RequestAsync();

            Assert.That(runtime.Requests, Is.Empty);
        }

        [Test]
        public async Task RequestAsync_WhenCalledTwiceBeforeCompletion_ShouldReturnSameTask()
        {
            FakePrewarmRuntime runtime = new FakePrewarmRuntime(
                true,
                new ExecutionResult { Success = true });
            PrewarmDynamicCodeUseCase useCase = new PrewarmDynamicCodeUseCase(runtime);

            Task firstTask = useCase.RequestAsync();
            Task secondTask = useCase.RequestAsync();

            Assert.That(secondTask, Is.SameAs(firstTask));
            await firstTask;
            Assert.That(runtime.Requests, Has.Count.EqualTo(1));
        }

        private sealed class FakePrewarmRuntime : IDynamicCodeExecutionRuntime
        {
            private readonly Queue<ExecutionResult> _results;
            private readonly bool _supportsAutoPrewarm;
            private readonly bool _idle;

            public FakePrewarmRuntime(
                bool supportsAutoPrewarm,
                params ExecutionResult[] results)
                : this(supportsAutoPrewarm, true, results)
            {
            }

            public FakePrewarmRuntime(
                bool supportsAutoPrewarm,
                bool idle,
                params ExecutionResult[] results)
            {
                _supportsAutoPrewarm = supportsAutoPrewarm;
                _idle = idle;
                _results = new Queue<ExecutionResult>(results ?? new ExecutionResult[0]);
            }

            public List<DynamicCodeExecutionRequest> Requests { get; } = new List<DynamicCodeExecutionRequest>();

            public bool SupportsAutoPrewarm()
            {
                return _supportsAutoPrewarm;
            }

            public Task<ExecutionResult> ExecuteAsync(
                DynamicCodeExecutionRequest request,
                CancellationToken cancellationToken = default)
            {
                Requests.Add(new DynamicCodeExecutionRequest
                {
                    Code = request.Code,
                    ClassName = request.ClassName,
                    Parameters = request.Parameters,
                    CompileOnly = request.CompileOnly,
                    SecurityLevel = request.SecurityLevel
                });

                return Task.FromResult(_results.Dequeue());
            }

            public Task<(bool Entered, ExecutionResult Result)> TryExecuteIfIdleAsync(
                DynamicCodeExecutionRequest request,
                CancellationToken cancellationToken = default)
            {
                if (!_idle)
                {
                    return Task.FromResult<(bool, ExecutionResult)>((false, null));
                }

                Requests.Add(new DynamicCodeExecutionRequest
                {
                    Code = request.Code,
                    ClassName = request.ClassName,
                    Parameters = request.Parameters,
                    CompileOnly = request.CompileOnly,
                    SecurityLevel = request.SecurityLevel
                });

                return Task.FromResult<(bool, ExecutionResult)>((true, _results.Dequeue()));
            }
        }
    }
}
