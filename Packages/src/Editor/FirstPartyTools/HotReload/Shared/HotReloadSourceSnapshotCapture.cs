using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using io.github.hatayama.UnityCliLoop.ToolContracts;

using Debug = UnityEngine.Debug;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Captures the source snapshot of the current compile once per domain, off the main thread.
    /// The domain load starts the capture without waiting for it; a hot reload apply awaits it and
    /// a pause point blocks on it. At most one capture runs at a time. A capture that throws, saw
    /// no compiled assembly, or was cancelled leaves the domain uncaptured, so the next caller
    /// starts a new one.
    /// </summary>
    internal sealed class HotReloadSourceSnapshotCapture
    {
        // Why a cap on the hold: if a capture never finishes, compile must still return. Readers
        // keep waiting for the capture; only the compile completion stops waiting for it.
        private static readonly long HoldCapTicks = TimeSpan.FromSeconds(30).Ticks;

        private readonly Func<HotReloadSnapshotCaptureInputs> _readInputsOnMainThread;
        private readonly Func<HotReloadSnapshotCaptureInputs, CancellationToken, bool> _capture;
        private readonly Func<Func<bool>, Task<bool>> _runInBackground;
        private readonly Func<long> _utcNowTicks;
        // Why a lock: the capture finishes on a pool thread while the main thread asks whether it
        // runs, and a stale answer could start a second capture that collides with the first.
        private readonly object _lock = new object();
        private bool _captured;
        private Task<bool> _inFlight;
        private CancellationTokenSource _inFlightCancellation;
        private long _inFlightStartedUtcTicks;
        private bool _holdCapLogged;

        /// <param name="readInputsOnMainThread">Reads what the capture needs from Unity on the main thread.</param>
        /// <param name="capture">
        /// Captures the snapshot without touching Unity APIs and returns whether it found at least
        /// one compiled assembly.
        /// </param>
        /// <param name="runInBackground">Runs the capture off the main thread.</param>
        /// <param name="utcNowTicks">Reads the current UTC time, for the cap on the compile hold.</param>
        internal HotReloadSourceSnapshotCapture(
            Func<HotReloadSnapshotCaptureInputs> readInputsOnMainThread,
            Func<HotReloadSnapshotCaptureInputs, CancellationToken, bool> capture,
            Func<Func<bool>, Task<bool>> runInBackground,
            Func<long> utcNowTicks)
        {
            Debug.Assert(readInputsOnMainThread != null, "readInputsOnMainThread must not be null.");
            Debug.Assert(capture != null, "capture must not be null.");
            Debug.Assert(runInBackground != null, "runInBackground must not be null.");
            Debug.Assert(utcNowTicks != null, "utcNowTicks must not be null.");

            _readInputsOnMainThread = readInputsOnMainThread;
            _capture = capture;
            _runInBackground = runInBackground;
            _utcNowTicks = utcNowTicks;
        }

        /// <summary>
        /// Whether a capture is running now.
        /// </summary>
        internal bool IsInFlight
        {
            get
            {
                lock (_lock)
                {
                    return _inFlight != null;
                }
            }
        }

        /// <summary>
        /// Whether the compile completion should wait for the running capture. False once it has run
        /// for 30 seconds, which is logged once per capture. Main thread only.
        /// </summary>
        internal bool IsHoldingCompileCompletion
        {
            get
            {
                bool logCapExceeded;
                lock (_lock)
                {
                    if (_inFlight == null)
                    {
                        return false;
                    }

                    if (_utcNowTicks() - _inFlightStartedUtcTicks < HoldCapTicks)
                    {
                        return true;
                    }

                    logCapExceeded = !_holdCapLogged;
                    _holdCapLogged = true;
                }

                if (logCapExceeded)
                {
                    VibeLogger.LogWarning(
                        HotReloadConstants.VibeLogSourceSnapshotCaptureHoldCapExceeded,
                        "Hot reload stopped holding the compile completion for a source snapshot capture that has not finished.",
                        new { holdCapSeconds = TimeSpan.FromTicks(HoldCapTicks).TotalSeconds });
                }

                return false;
            }
        }

        /// <summary>
        /// Returns a completed task when this domain is captured, the running capture's task when one
        /// runs, and otherwise the task of a capture started now. Main thread only, because the
        /// inputs are read from Unity here. An exception while reading them reaches the caller and
        /// starts nothing.
        /// </summary>
        internal Task<bool> StartIfNeeded(string trigger)
        {
            Debug.Assert(!string.IsNullOrEmpty(trigger), "trigger must not be null or empty.");

            lock (_lock)
            {
                if (_captured)
                {
                    return Task.FromResult(true);
                }

                if (_inFlight != null)
                {
                    return _inFlight;
                }
            }

            Stopwatch mainThreadWatch = Stopwatch.StartNew();
            HotReloadSnapshotCaptureInputs inputs = _readInputsOnMainThread();
            CancellationTokenSource cancellation = new CancellationTokenSource();
            Stopwatch poolWatch = new Stopwatch();
            Task<bool> task = _runInBackground(() =>
            {
                poolWatch.Start();
                try
                {
                    return _capture(inputs, cancellation.Token);
                }
                finally
                {
                    poolWatch.Stop();
                }
            });
            Debug.Assert(task != null, "runInBackground must return a task.");
            lock (_lock)
            {
                _inFlight = task;
                _inFlightCancellation = cancellation;
                _inFlightStartedUtcTicks = _utcNowTicks();
                _holdCapLogged = false;
            }

            VibeLogger.LogInfo(
                HotReloadConstants.VibeLogSourceSnapshotCaptureStarted,
                "Hot reload started the source snapshot capture of this domain.",
                new { trigger, mainThreadMs = mainThreadWatch.ElapsedMilliseconds });
            // Why the continuation is attached after the task is published: a task that already
            // finished then runs it right here, and it finds the published task to clear.
            // Why ExecuteSynchronously on the default scheduler: the bookkeeping must not wait for the
            // main thread, which a pause point may be blocking on this very task.
            task.ContinueWith(
                finished => OnFinished(finished, cancellation, trigger, poolWatch),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return task;
        }

        /// <summary>
        /// Completes when this domain's capture has run, passing on the exception of a capture that
        /// failed. Main thread only.
        /// </summary>
        internal async Task EnsureCapturedAsync(string trigger)
        {
            // Why no ConfigureAwait(false): the caller continues with main-thread work.
            await StartIfNeeded(trigger);
        }

        /// <summary>
        /// Blocks the main thread until this domain's capture has run, passing on the exception of a
        /// capture that failed. For synchronous readers only. It cannot dead-lock, because the
        /// capture and its bookkeeping never wait for the main thread.
        /// </summary>
        internal void EnsureCapturedBlocking(string trigger)
        {
            StartIfNeeded(trigger).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Asks the running capture to stop, without waiting for it. Its assemblies that were not
        /// finished get no stamp, so the next capture redoes them.
        /// </summary>
        internal void CancelInFlight()
        {
            lock (_lock)
            {
                _inFlightCancellation?.Cancel();
            }
        }

        private void OnFinished(
            Task<bool> task,
            CancellationTokenSource cancellation,
            string trigger,
            Stopwatch poolWatch)
        {
            // Why the exception is read on every path that has one: the domain load and the first
            // update tick never await the task, and an exception nobody reads is reported later as
            // an unobserved task exception. Why here and not in a log argument: the VibeLogger
            // calls are conditional on ULOOP_DEBUG, so an ordinary build drops them with their
            // arguments, and a read inside one would never run.
            Exception failure = task.IsFaulted ? task.Exception.GetBaseException() : null;
            bool cancelled = task.IsCanceled
                || (failure is OperationCanceledException && cancellation.IsCancellationRequested);
            bool captured = task.Status == TaskStatus.RanToCompletion && task.Result;
            lock (_lock)
            {
                if (!ReferenceEquals(_inFlightCancellation, cancellation))
                {
                    return;
                }

                _inFlight = null;
                _inFlightCancellation = null;
                // Marked only after the capture returns true: a capture that threw may have left
                // assemblies without a snapshot, and one that found no compiled assembly ran while
                // Unity was compiling. Marking either done would keep every later reader from
                // capturing them.
                _captured = captured;
            }

            cancellation.Dispose();
            LogOutcome(trigger, captured, cancelled, failure, poolWatch.ElapsedMilliseconds);
        }

        private static void LogOutcome(string trigger, bool captured, bool cancelled, Exception failure, long captureMs)
        {
            if (captured)
            {
                VibeLogger.LogInfo(
                    HotReloadConstants.VibeLogSourceSnapshotCaptured,
                    "Hot reload captured the source snapshot of this domain.",
                    new { trigger, captureMs });
                return;
            }

            if (cancelled)
            {
                VibeLogger.LogInfo(
                    HotReloadConstants.VibeLogSourceSnapshotCaptureCancelled,
                    "Hot reload cancelled the source snapshot capture of this domain.",
                    new { trigger });
                return;
            }

            if (failure != null)
            {
                VibeLogger.LogWarning(
                    HotReloadConstants.VibeLogSourceSnapshotCaptureFailed,
                    "Hot reload failed to capture the source snapshot of this domain.",
                    new { trigger, error = failure.GetType().Name, message = failure.Message });
                return;
            }

            VibeLogger.LogInfo(
                HotReloadConstants.VibeLogSourceSnapshotCaptureEmpty,
                "Hot reload found no compiled assembly to capture the source snapshot from.",
                new { trigger });
        }
    }
}
