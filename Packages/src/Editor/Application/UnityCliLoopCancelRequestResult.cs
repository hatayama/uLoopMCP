using io.github.hatayama.UnityCliLoop.Domain;

namespace io.github.hatayama.UnityCliLoop.Application
{
    /// <summary>
    /// What a uloop cancel request did to the execution slot, in values that layers outside Domain
    /// can read. Outcome is Cancelled, NothingRunning, or NotCancellable; the running-tool fields
    /// are null when nothing was running.
    /// </summary>
    internal sealed class UnityCliLoopCancelRequestResult
    {
        private UnityCliLoopCancelRequestResult(ToolCancelRequestResult result)
        {
            Outcome = result.Outcome.ToString();
            FiredCancellation = result.Outcome == ToolCancelRequestOutcome.Cancelled;
            RunningToolName = result.RunningToolName;
            RunningToolElapsedSeconds = result.RunningToolElapsedSeconds;
        }

        internal string Outcome { get; }

        // True only when the request fired the cancellation of a running tool.
        internal bool FiredCancellation { get; }

        internal string RunningToolName { get; }

        internal int? RunningToolElapsedSeconds { get; }

        internal static UnityCliLoopCancelRequestResult From(ToolCancelRequestResult result)
        {
            UnityEngine.Debug.Assert(result != null, "result must not be null");

            return new UnityCliLoopCancelRequestResult(result);
        }
    }
}
