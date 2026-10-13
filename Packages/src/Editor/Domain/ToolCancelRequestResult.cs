using System.Diagnostics;

namespace io.github.hatayama.UnityCliLoop.Domain
{
    /// <summary>
    /// Reports what a uloop cancel request did to the execution slot. The running-tool fields
    /// are null when nothing was running.
    /// </summary>
    internal sealed class ToolCancelRequestResult
    {
        private ToolCancelRequestResult(
            ToolCancelRequestOutcome outcome,
            string runningToolName,
            int? runningToolElapsedSeconds)
        {
            Outcome = outcome;
            RunningToolName = runningToolName;
            RunningToolElapsedSeconds = runningToolElapsedSeconds;
        }

        internal ToolCancelRequestOutcome Outcome { get; }

        internal string RunningToolName { get; }

        internal int? RunningToolElapsedSeconds { get; }

        internal static ToolCancelRequestResult NothingRunning()
        {
            return new ToolCancelRequestResult(ToolCancelRequestOutcome.NothingRunning, null, null);
        }

        internal static ToolCancelRequestResult Cancelled(string runningToolName, int runningToolElapsedSeconds)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(runningToolName), "runningToolName must not be null or whitespace");

            return new ToolCancelRequestResult(
                ToolCancelRequestOutcome.Cancelled,
                runningToolName,
                runningToolElapsedSeconds);
        }

        internal static ToolCancelRequestResult NotCancellable(string runningToolName, int runningToolElapsedSeconds)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(runningToolName), "runningToolName must not be null or whitespace");

            return new ToolCancelRequestResult(
                ToolCancelRequestOutcome.NotCancellable,
                runningToolName,
                runningToolElapsedSeconds);
        }
    }

    /// <summary>
    /// What a uloop cancel request found in the execution slot. Member names are the wire values
    /// of the cancel response's Outcome.
    /// </summary>
    internal enum ToolCancelRequestOutcome
    {
        Cancelled,
        NothingRunning,
        NotCancellable
    }
}
