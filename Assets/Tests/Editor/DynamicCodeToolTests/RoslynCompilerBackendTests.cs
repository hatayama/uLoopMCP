using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    [TestFixture]
    public class RoslynCompilerBackendTests
    {
        [Test]
        public async Task AwaitOneShotProcessCompletionAsync_WhenProcessCompletes_ShouldReturnOutputAndExitCode()
        {
            TaskCompletionSource<string> stdoutCompletionSource =
                new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<string> stderrCompletionSource =
                new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> exitCompletionSource =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            stdoutCompletionSource.SetResult("stdout");
            stderrCompletionSource.SetResult("stderr");
            exitCompletionSource.SetResult(true);

            RoslynCompilerBackend.OneShotProcessCompletionResult result =
                await RoslynCompilerBackend.AwaitOneShotProcessCompletionAsync(
                    stdoutCompletionSource.Task,
                    stderrCompletionSource.Task,
                    exitCompletionSource.Task,
                    () => 7,
                    () => { },
                    CancellationToken.None);

            Assert.That(result.StandardOutput, Is.EqualTo("stdout"));
            Assert.That(result.StandardError, Is.EqualTo("stderr"));
            Assert.That(result.ExitCode, Is.EqualTo(7));
        }

        [Test]
        public void AwaitOneShotProcessCompletionAsync_WhenCancellationIsRequested_ShouldRequestCancellationAndThrow()
        {
            TaskCompletionSource<string> stdoutCompletionSource =
                new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<string> stderrCompletionSource =
                new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> exitCompletionSource =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            bool cancellationRequested = false;
            Task<RoslynCompilerBackend.OneShotProcessCompletionResult> completionTask =
                RoslynCompilerBackend.AwaitOneShotProcessCompletionAsync(
                    stdoutCompletionSource.Task,
                    stderrCompletionSource.Task,
                    exitCompletionSource.Task,
                    () => 0,
                    () =>
                    {
                        cancellationRequested = true;
                        stdoutCompletionSource.TrySetResult(string.Empty);
                        stderrCompletionSource.TrySetResult(string.Empty);
                        exitCompletionSource.TrySetResult(true);
                    },
                    cancellationTokenSource.Token);

            cancellationTokenSource.Cancel();

            Assert.That(async () => await completionTask, Throws.InstanceOf<OperationCanceledException>());
            Assert.That(cancellationRequested, Is.True);
        }
    }
}
