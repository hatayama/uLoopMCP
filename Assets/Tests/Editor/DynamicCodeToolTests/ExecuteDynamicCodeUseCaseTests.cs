using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    [TestFixture]
    public class ExecuteDynamicCodeUseCaseTests
    {
        [Test]
        public async Task ExecuteAsync_WhenInitialCompilationLooksLikeMissingReturn_ShouldRetryOnce()
        {
            FakeDynamicCodeExecutionRuntime runtime = new FakeDynamicCodeExecutionRuntime(
                new ExecutionResult
                {
                    Success = false,
                    CompilationErrors = new List<CompilationError>
                    {
                        new CompilationError
                        {
                            ErrorCode = "CS0161",
                            Message = "Not all code paths return a value"
                        }
                    }
                },
                new ExecutionResult
                {
                    Success = true,
                    Result = "ok"
                });
            ExecuteDynamicCodeUseCase useCase = new ExecuteDynamicCodeUseCase(runtime);

            DynamicCodeSecurityLevel previous = ULoopSettings.GetDynamicCodeSecurityLevel();
            ULoopSettings.SetDynamicCodeSecurityLevel(DynamicCodeSecurityLevel.Restricted);

            try
            {
                ExecuteDynamicCodeResponse response = await useCase.ExecuteAsync(
                    new ExecuteDynamicCodeSchema
                    {
                        Code = "int x = 1",
                        CompileOnly = false
                    },
                    CancellationToken.None);

                Assert.That(response.Success, Is.True);
                Assert.That(runtime.Requests, Has.Count.EqualTo(2));
                Assert.That(runtime.Requests[1].Code, Does.Contain("return null;"));
            }
            finally
            {
                ULoopSettings.SetDynamicCodeSecurityLevel(previous);
            }
        }

        [Test]
        public async Task ExecuteAsync_WhenInitialExecutionSucceeds_ShouldNotRetry()
        {
            FakeDynamicCodeExecutionRuntime runtime = new FakeDynamicCodeExecutionRuntime(
                new ExecutionResult
                {
                    Success = true,
                    Result = "ok"
                });
            ExecuteDynamicCodeUseCase useCase = new ExecuteDynamicCodeUseCase(runtime);

            DynamicCodeSecurityLevel previous = ULoopSettings.GetDynamicCodeSecurityLevel();
            ULoopSettings.SetDynamicCodeSecurityLevel(DynamicCodeSecurityLevel.Restricted);

            try
            {
                ExecuteDynamicCodeResponse response = await useCase.ExecuteAsync(
                    new ExecuteDynamicCodeSchema
                    {
                        Code = "return 1;",
                        CompileOnly = false
                    },
                    CancellationToken.None);

                Assert.That(response.Success, Is.True);
                Assert.That(runtime.Requests, Has.Count.EqualTo(1));
            }
            finally
            {
                ULoopSettings.SetDynamicCodeSecurityLevel(previous);
            }
        }

        [Test]
        public async Task ExecuteAsync_WhenDiagnosticLineUsesTwoDigits_ShouldAlignCaretWithRenderedPrefix()
        {
            string updatedCode = string.Join(
                "\n",
                Enumerable.Range(1, 12).Select(index => index == 10 ? "abcd" : $"line{index}"));
            FakeDynamicCodeExecutionRuntime runtime = new FakeDynamicCodeExecutionRuntime(
                new ExecutionResult
                {
                    Success = false,
                    ErrorMessage = "Compilation error occurred",
                    UpdatedCode = updatedCode,
                    CompilationErrors = new List<CompilationError>
                    {
                        new CompilationError
                        {
                            ErrorCode = "CS0103",
                            Message = "CS0103: The name 'x' does not exist in the current context",
                            Line = 10,
                            Column = 2
                        }
                    }
                });
            ExecuteDynamicCodeUseCase useCase = new ExecuteDynamicCodeUseCase(runtime);

            DynamicCodeSecurityLevel previous = ULoopSettings.GetDynamicCodeSecurityLevel();
            ULoopSettings.SetDynamicCodeSecurityLevel(DynamicCodeSecurityLevel.Restricted);

            try
            {
                ExecuteDynamicCodeResponse response = await useCase.ExecuteAsync(
                    new ExecuteDynamicCodeSchema
                    {
                        Code = "return x;"
                    },
                    CancellationToken.None);

                Assert.That(response.Diagnostics, Has.Count.EqualTo(1));

                string[] contextLines = response.Diagnostics[0].Context
                    .Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
                int targetLineIndex = System.Array.FindIndex(contextLines, line => line.StartsWith("L10:"));

                Assert.That(targetLineIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(contextLines[targetLineIndex + 1].IndexOf('^'), Is.EqualTo("L10:".Length + 1));
            }
            finally
            {
                ULoopSettings.SetDynamicCodeSecurityLevel(previous);
            }
        }

        private sealed class FakeDynamicCodeExecutionRuntime : IDynamicCodeExecutionRuntime
        {
            private readonly Queue<ExecutionResult> _results;

            public FakeDynamicCodeExecutionRuntime(params ExecutionResult[] results)
            {
                _results = new Queue<ExecutionResult>(results);
            }

            public List<DynamicCodeExecutionRequest> Requests { get; } = new List<DynamicCodeExecutionRequest>();

            public bool SupportsAutoPrewarm()
            {
                return true;
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
                return Task.FromResult<(bool, ExecutionResult)>((true, null));
            }
        }
    }
}
