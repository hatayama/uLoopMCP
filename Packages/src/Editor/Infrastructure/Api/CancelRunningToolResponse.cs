using io.github.hatayama.UnityCliLoop.Application;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Infrastructure
{
    /// <summary>
    /// Payload of the cancel-running-tool command behind uloop cancel. Outcome is Cancelled,
    /// NothingRunning, or NotCancellable; the running-tool fields are null when nothing was running.
    /// </summary>
    public class CancelRunningToolResponse : UnityCliLoopToolResponse
    {
        internal CancelRunningToolResponse(UnityCliLoopCancelRequestResult result)
        {
            Outcome = result.Outcome;
            RunningToolName = result.RunningToolName;
            RunningToolElapsedSeconds = result.RunningToolElapsedSeconds;
            // Why NotCancellable is a failure: the CLI's exit code follows Success, and the caller
            // asked to stop a tool that is still running.
            Success = result.Outcome != NotCancellableOutcome;
        }

        private const string NotCancellableOutcome = "NotCancellable";

        public string Outcome { get; }

        public string RunningToolName { get; }

        public int? RunningToolElapsedSeconds { get; }
    }
}
