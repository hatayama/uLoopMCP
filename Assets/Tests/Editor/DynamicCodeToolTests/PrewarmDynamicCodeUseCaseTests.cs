using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace io.github.hatayama.UnityCliLoop.DynamicCodeToolTests
{
    [TestFixture]
    public class PrewarmDynamicCodeUseCaseTests
    {
        [SetUp]
        public void SetUp()
        {
            DynamicCodeStartupTelemetry.Reset();
            DynamicCodeForegroundWarmupState.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            DynamicCodeStartupTelemetry.Reset();
            DynamicCodeForegroundWarmupState.Reset();
        }

        [Test]
        public async Task RequestAsync_WhenSupportedAndWarmupSucceeds_ShouldExecuteIdleRuntime()
        {
            // Tests that first-party prewarm goes through the local runtime instead of platform host services.
            FakePrewarmRuntime runtime = new FakePrewarmRuntime(
                true,
                new RuntimeResult(true, new ExecutionResult { Success = true }));
            PrewarmDynamicCodeUseCase useCase = new PrewarmDynamicCodeUseCase(runtime);

            await useCase.RequestAsync();

            Assert.That(runtime.Requests, Has.Count.EqualTo(1));
            Assert.That(
                runtime.Requests[0].Code,
                Does.Contain("UnityEngine.Debug.Log(\"Unity CLI Loop dynamic code prewarm\");"));
            Assert.That(runtime.Requests[0].CompileOnly, Is.False);
            Assert.That(runtime.Requests[0].YieldToForegroundRequests, Is.True);
            Assert.That(DynamicCodeStartupTelemetry.CreateTimingEntries(), Has.Member("[Perf] WarmReady: True"));
        }

        [Test]
        public async Task RequestAsync_WhenFastPathIsUnavailable_ShouldSkipExecution()
        {
            // Tests that unsupported runtimes skip prewarm without touching execution.
            FakePrewarmRuntime runtime = new FakePrewarmRuntime(
                false,
                new RuntimeResult(true, new ExecutionResult { Success = true }));
            PrewarmDynamicCodeUseCase useCase = new PrewarmDynamicCodeUseCase(runtime);

            await useCase.RequestAsync();

            Assert.That(runtime.Requests, Is.Empty);
            Assert.That(
                DynamicCodeStartupTelemetry.CreateTimingEntries(),
                Has.Member("[Perf] PrewarmDetail: fast_path_unavailable"));
        }

        [Test]
        public async Task RequestAsync_WhenRuntimeIsBusy_ShouldMarkForegroundPreemption()
        {
            // Tests that a busy runtime yields to foreground work without retrying through host services.
            FakePrewarmRuntime runtime = new FakePrewarmRuntime(
                true,
                new RuntimeResult(false, new ExecutionResult { Success = false }));
            PrewarmDynamicCodeUseCase useCase = new PrewarmDynamicCodeUseCase(runtime);

            await useCase.RequestAsync();

            Assert.That(runtime.Requests, Has.Count.EqualTo(1));
            Assert.That(
                DynamicCodeStartupTelemetry.CreateTimingEntries(),
                Has.Member("[Perf] PrewarmDetail: foreground_request_preempted"));
        }

        [Test]
        public async Task RequestAsync_WhenWarmupFails_ShouldRecordFailure()
        {
            // Tests that runtime failure is reported without escaping through platform services.
            FakePrewarmRuntime runtime = new FakePrewarmRuntime(
                true,
                new RuntimeResult(true, new ExecutionResult
                {
                    Success = false,
                    ErrorMessage = "warmup failed"
                }));
            PrewarmDynamicCodeUseCase useCase = new PrewarmDynamicCodeUseCase(runtime);

            await useCase.RequestAsync();

            Assert.That(runtime.Requests, Has.Count.EqualTo(1));
            Assert.That(
                DynamicCodeStartupTelemetry.CreateTimingEntries(),
                Has.Member("[Perf] PrewarmDetail: warmup failed"));
        }

        [Test]
        public void RequestAsync_WhenLifecycleIsCancelled_ShouldStopBeforeExecution()
        {
            // Tests that cancellation prevents prewarm from entering the runtime.
            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();
            FakePrewarmRuntime runtime = new FakePrewarmRuntime(
                true,
                new RuntimeResult(true, new ExecutionResult { Success = true }));
            PrewarmDynamicCodeUseCase useCase = new PrewarmDynamicCodeUseCase(
                runtime,
                cancellationTokenSource.Token);

            Assert.ThrowsAsync<TaskCanceledException>(async () => await useCase.RequestAsync());
            Assert.That(runtime.Requests, Is.Empty);
        }

        private readonly struct RuntimeResult
        {
            public readonly bool Entered;
            public readonly ExecutionResult Result;

            public RuntimeResult(bool entered, ExecutionResult result)
            {
                Entered = entered;
                Result = result;
            }
        }

        private sealed class FakePrewarmRuntime : IDynamicCodeExecutionRuntime
        {
            private readonly bool _supportsAutoPrewarm;
            private readonly Queue<RuntimeResult> _results;

            public FakePrewarmRuntime(bool supportsAutoPrewarm, RuntimeResult result)
            {
                _supportsAutoPrewarm = supportsAutoPrewarm;
                _results = new Queue<RuntimeResult>(new[] { result });
            }

            public List<DynamicCodeExecutionRequest> Requests { get; } =
                new List<DynamicCodeExecutionRequest>();

            public bool SupportsAutoPrewarm()
            {
                return _supportsAutoPrewarm;
            }

            public Task<ExecutionResult> ExecuteAsync(
                DynamicCodeExecutionRequest request,
                CancellationToken cancellationToken = default)
            {
                Assert.Fail("Prewarm must use idle-only runtime execution.");
                return Task.FromResult(new ExecutionResult { Success = false });
            }

            public Task<(bool Entered, ExecutionResult Result)> TryExecuteIfIdleAsync(
                DynamicCodeExecutionRequest request,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Requests.Add(request);
                RuntimeResult result = _results.Dequeue();
                return Task.FromResult((result.Entered, result.Result));
            }
        }
    }
}
