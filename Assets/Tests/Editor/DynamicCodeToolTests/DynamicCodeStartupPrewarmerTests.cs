using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

using io.github.hatayama.UnityCliLoop.Application;
using io.github.hatayama.UnityCliLoop.FirstPartyTools;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.DynamicCodeToolTests
{
    /// <summary>
    /// Test fixture that verifies startup prewarm keeps execute-dynamic-code's first visible request warm.
    /// </summary>
    [TestFixture]
    public class DynamicCodeStartupPrewarmerTests
    {
        [SetUp]
        public void SetUp()
        {
            DynamicCodeForegroundWarmupState.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            DynamicCodeForegroundWarmupState.Reset();
        }

        [Test]
        public async Task RequestAsync_WhenStartupPrewarmSucceeds_ShouldSkipFirstForegroundWarmup()
        {
            // Tests that successful startup prewarm prevents the first user request from paying hidden warmup cost.
            FakeDynamicCodeExecutionRuntime runtime = new(
                new ExecutionResult
                {
                    Success = true,
                    Result = "warm"
                },
                new ExecutionResult
                {
                    Success = true,
                    Result = "user"
                });
            DynamicCodeStartupPrewarmer prewarmer = new(runtime, 0);
            ExecuteDynamicCodeUseCase useCase = new(runtime);

            DynamicCodeSecurityLevel previous = ULoopSettings.GetDynamicCodeSecurityLevel();
            ULoopSettings.SetDynamicCodeSecurityLevel(DynamicCodeSecurityLevel.Restricted);

            try
            {
                await prewarmer.RequestAsync(CancellationToken.None);
                ExecuteDynamicCodeResponse response = await useCase.ExecuteAsync(
                    new ExecuteDynamicCodeSchema
                    {
                        Code = "return 1;",
                        CompileOnly = false
                    },
                    CancellationToken.None);

                Assert.That(response.Success, Is.True);
                Assert.That(runtime.TryExecuteRequests, Has.Count.EqualTo(1));
                Assert.That(runtime.TryExecuteRequests[0].YieldToForegroundRequests, Is.True);
                Assert.That(runtime.Requests, Has.Count.EqualTo(1));
                Assert.That(runtime.Requests[0].Code, Is.EqualTo("return 1;"));
            }
            finally
            {
                ULoopSettings.SetDynamicCodeSecurityLevel(previous);
            }
        }

        [Test]
        public async Task RequestAsync_WhenCalledTwice_ShouldRunOnlyOnePrewarmRequest()
        {
            // Tests that repeated startup notifications do not compile duplicate warmup snippets.
            FakeDynamicCodeExecutionRuntime runtime = new(
                new ExecutionResult
                {
                    Success = true,
                    Result = "warm"
                });
            DynamicCodeStartupPrewarmer prewarmer = new(runtime, 0);

            await prewarmer.RequestAsync(CancellationToken.None);
            await prewarmer.RequestAsync(CancellationToken.None);

            Assert.That(runtime.TryExecuteRequests, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task RequestAsync_WhenForegroundWarmupAlreadyStarted_ShouldNotEnterRuntime()
        {
            // Tests that startup prewarm yields when the visible foreground warmup path already owns the warmup state.
            bool started = DynamicCodeForegroundWarmupState.TryBegin();
            Assert.That(started, Is.True);
            FakeDynamicCodeExecutionRuntime runtime = new();
            DynamicCodeStartupPrewarmer prewarmer = new(runtime, 0);

            await prewarmer.RequestAsync(CancellationToken.None);

            Assert.That(runtime.TryExecuteRequests, Is.Empty);
        }

        /// <summary>
        /// Test support runtime that records requests and returns queued results.
        /// </summary>
        private sealed class FakeDynamicCodeExecutionRuntime : IDynamicCodeExecutionRuntime
        {
            private readonly Queue<ExecutionResult> _results;

            internal FakeDynamicCodeExecutionRuntime(params ExecutionResult[] results)
            {
                _results = new Queue<ExecutionResult>(results);
            }

            internal List<DynamicCodeExecutionRequest> Requests { get; } = new List<DynamicCodeExecutionRequest>();

            internal List<DynamicCodeExecutionRequest> TryExecuteRequests { get; } = new List<DynamicCodeExecutionRequest>();

            public Task<ExecutionResult> ExecuteAsync(
                DynamicCodeExecutionRequest request,
                CancellationToken ct = default)
            {
                Requests.Add(CloneRequest(request));
                return Task.FromResult(_results.Dequeue());
            }

            public Task<(bool Entered, ExecutionResult Result)> TryExecuteIfIdleAsync(
                DynamicCodeExecutionRequest request,
                CancellationToken ct = default)
            {
                TryExecuteRequests.Add(CloneRequest(request));
                return Task.FromResult<(bool, ExecutionResult)>((true, _results.Dequeue()));
            }

            private static DynamicCodeExecutionRequest CloneRequest(DynamicCodeExecutionRequest request)
            {
                return new DynamicCodeExecutionRequest
                {
                    Code = request.Code,
                    ClassName = request.ClassName,
                    Parameters = request.Parameters,
                    CompileOnly = request.CompileOnly,
                    SecurityLevel = request.SecurityLevel,
                    YieldToForegroundRequests = request.YieldToForegroundRequests
                };
            }
        }
    }
}
