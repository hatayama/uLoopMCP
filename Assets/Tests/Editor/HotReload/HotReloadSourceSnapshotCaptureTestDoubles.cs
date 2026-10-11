using System;
using System.Threading.Tasks;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// Ways to build the source snapshot capture gate for tests, so no test runs a capture on a
    /// pool thread.
    /// </summary>
    internal static class HotReloadSourceSnapshotCaptureTestDoubles
    {
        /// <summary>
        /// A gate that runs <paramref name="capture"/> on the calling thread the moment it starts, so
        /// the capture has finished when the call that started it returns.
        /// </summary>
        internal static HotReloadSourceSnapshotCapture CompletingSynchronously(Func<bool> capture)
        {
            return new HotReloadSourceSnapshotCapture(
                FixedInputs,
                (inputs, cancellationToken) => capture(),
                RunNow,
                () => 0);
        }

        /// <summary>Inputs that no test capture reads.</summary>
        internal static HotReloadSnapshotCaptureInputs FixedInputs()
        {
            return new HotReloadSnapshotCaptureInputs();
        }

        // Why the exception goes into the task: Task.Run does the same, and the gate must see a
        // failed task rather than a throwing start.
        private static Task<bool> RunNow(Func<bool> work)
        {
            try
            {
                return Task.FromResult(work());
            }
            catch (Exception exception)
            {
                return Task.FromException<bool>(exception);
            }
        }
    }

    /// <summary>
    /// A stand-in for the pool thread that holds the started work until the test runs it, so a
    /// test controls when a capture is in flight and when it finishes.
    /// </summary>
    internal sealed class HeldBackgroundRun
    {
        private Func<bool> _work;
        private TaskCompletionSource<bool> _completion;

        internal int StartCount { get; private set; }

        internal Task<bool> Run(Func<bool> work)
        {
            StartCount++;
            _work = work;
            _completion = new TaskCompletionSource<bool>();
            return _completion.Task;
        }

        /// <summary>
        /// Runs the held work on the calling thread and finishes its task with the outcome, the way
        /// Task.Run would: a thrown exception, cancellation included, faults the task.
        /// </summary>
        internal void RunHeldWork()
        {
            NUnit.Framework.Assert.That(_work, NUnit.Framework.Is.Not.Null, "no work was started");
            Func<bool> work = _work;
            TaskCompletionSource<bool> completion = _completion;
            _work = null;
            _completion = null;
            bool result;
            try
            {
                result = work();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
                return;
            }

            completion.SetResult(result);
        }
    }
}
