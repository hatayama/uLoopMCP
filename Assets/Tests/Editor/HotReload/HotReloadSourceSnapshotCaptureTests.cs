using System;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using NUnit.Framework;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// EditMode coverage for the gate that captures the source snapshot once per domain off the
    /// main thread. The pool thread is a held stand-in, so each test decides when a capture ends.
    /// </summary>
    public class HotReloadSourceSnapshotCaptureTests
    {
        private const string Trigger = HotReloadConstants.SourceSnapshotCaptureTriggerDomainLoad;
        private static readonly long HoldCapTicks = TimeSpan.FromSeconds(30).Ticks;

        private HeldBackgroundRun _background;
        private int _inputReads;
        private int _captureRuns;
        private Func<CancellationToken, bool> _captureBody;
        private long _nowTicks;

        [SetUp]
        public void SetUp()
        {
            VibeLogger.ClearMemoryLogs();
            _background = new HeldBackgroundRun();
            _inputReads = 0;
            _captureRuns = 0;
            _captureBody = _ => true;
            _nowTicks = 0;
        }

        /// <summary>
        /// What: starting reads the inputs once on the calling thread, hands the capture to the
        /// background, and returns before the capture runs, logging how long the main thread spent.
        /// </summary>
        [Test]
        public void StartIfNeeded_NotStarted_ReadsInputsOnceAndReturnsBeforeTheCaptureFinishes()
        {
            HotReloadSourceSnapshotCapture gate = CreateGate();

            Task<bool> task = gate.StartIfNeeded(Trigger);

            Assert.That(_inputReads, Is.EqualTo(1), "input reads");
            Assert.That(_captureRuns, Is.EqualTo(0), "captures run before the background ran them");
            Assert.That(task.IsCompleted, Is.False);
            Assert.That(gate.IsInFlight, Is.True);
            JObject started = HotReloadWarmUpTestDoubles.ReadSingleVibeContext(
                HotReloadConstants.VibeLogSourceSnapshotCaptureStarted);
            Assert.That((string)started["trigger"], Is.EqualTo(Trigger));
            Assert.That(started["mainThreadMs"].Type, Is.EqualTo(JTokenType.Integer));

            _background.RunHeldWork();

            Assert.That(task.Status, Is.EqualTo(TaskStatus.RanToCompletion));
            Assert.That(_captureRuns, Is.EqualTo(1));
            Assert.That(gate.IsInFlight, Is.False);
        }

        /// <summary>
        /// What: a second caller while a capture runs gets the running capture's task and starts no
        /// second capture, so two captures never touch one snapshot directory at once.
        /// </summary>
        [Test]
        public void StartIfNeeded_WhileInFlight_ReturnsTheSameTaskAndDoesNotStartAnother()
        {
            HotReloadSourceSnapshotCapture gate = CreateGate();

            Task<bool> first = gate.StartIfNeeded(Trigger);
            Task<bool> second = gate.StartIfNeeded(HotReloadConstants.SourceSnapshotCaptureTriggerFirstUpdateTick);

            Assert.That(second, Is.SameAs(first));
            Assert.That(_background.StartCount, Is.EqualTo(1));
            Assert.That(_inputReads, Is.EqualTo(1));
            _background.RunHeldWork();
        }

        /// <summary>
        /// What: after a capture that threw, the next caller starts a new one, and the failure is
        /// logged with its exception type and leaves no captured entry.
        /// </summary>
        [Test]
        public void StartIfNeeded_AfterAFailedCapture_StartsANewOne()
        {
            _captureBody = _ => throw new InvalidOperationException("The capture failed.");
            HotReloadSourceSnapshotCapture gate = CreateGate();
            Task<bool> failed = gate.StartIfNeeded(Trigger);
            _background.RunHeldWork();

            _captureBody = _ => true;
            Task<bool> retried = gate.StartIfNeeded(HotReloadConstants.SourceSnapshotCaptureTriggerFirstUpdateTick);

            Assert.That(failed.IsFaulted, Is.True);
            Assert.That(retried, Is.Not.SameAs(failed));
            Assert.That(_background.StartCount, Is.EqualTo(2));
            JObject failure = HotReloadWarmUpTestDoubles.ReadSingleVibeContext(
                HotReloadConstants.VibeLogSourceSnapshotCaptureFailed);
            Assert.That((string)failure["trigger"], Is.EqualTo(Trigger));
            Assert.That((string)failure["error"], Is.EqualTo(nameof(InvalidOperationException)));
            Assert.That(CountEntries(HotReloadConstants.VibeLogSourceSnapshotCaptured), Is.EqualTo(0));
            _background.RunHeldWork();
        }

        /// <summary>
        /// What: after a capture that found no compiled assembly, the next caller starts a new one,
        /// and the empty capture is logged as such rather than as captured.
        /// </summary>
        [Test]
        public void StartIfNeeded_AfterAnEmptyCapture_StartsANewOne()
        {
            _captureBody = _ => false;
            HotReloadSourceSnapshotCapture gate = CreateGate();
            gate.StartIfNeeded(Trigger);
            _background.RunHeldWork();

            gate.StartIfNeeded(HotReloadConstants.SourceSnapshotCaptureTriggerFirstUpdateTick);

            Assert.That(_background.StartCount, Is.EqualTo(2));
            Assert.That(CountEntries(HotReloadConstants.VibeLogSourceSnapshotCaptureEmpty), Is.EqualTo(1));
            Assert.That(CountEntries(HotReloadConstants.VibeLogSourceSnapshotCaptured), Is.EqualTo(0));
            _background.RunHeldWork();
        }

        /// <summary>
        /// What: after a cancelled capture, the next caller starts a new one, and the cancellation
        /// is logged as cancelled rather than as a failure.
        /// </summary>
        [Test]
        public void StartIfNeeded_AfterACancelledCapture_StartsANewOne()
        {
            _captureBody = cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return true;
            };
            HotReloadSourceSnapshotCapture gate = CreateGate();
            gate.StartIfNeeded(Trigger);
            gate.CancelInFlight();
            _background.RunHeldWork();

            gate.StartIfNeeded(HotReloadConstants.SourceSnapshotCaptureTriggerFirstUpdateTick);

            Assert.That(_background.StartCount, Is.EqualTo(2));
            Assert.That(CountEntries(HotReloadConstants.VibeLogSourceSnapshotCaptureCancelled), Is.EqualTo(1));
            Assert.That(CountEntries(HotReloadConstants.VibeLogSourceSnapshotCaptureFailed), Is.EqualTo(0));
            _background.RunHeldWork();
        }

        /// <summary>
        /// What: once a capture succeeded, later callers get a completed task without reading the
        /// inputs or starting anything, and the single captured entry keeps the first trigger.
        /// </summary>
        [Test]
        public void StartIfNeeded_AfterSuccess_ReturnsACompletedTaskWithoutReadingInputs()
        {
            HotReloadSourceSnapshotCapture gate = CreateGate();
            gate.StartIfNeeded(Trigger);
            _background.RunHeldWork();

            Task<bool> later = gate.StartIfNeeded(HotReloadConstants.SourceSnapshotCaptureTriggerApply);

            Assert.That(later.Status, Is.EqualTo(TaskStatus.RanToCompletion));
            Assert.That(_inputReads, Is.EqualTo(1));
            Assert.That(_background.StartCount, Is.EqualTo(1));
            JObject captured = HotReloadWarmUpTestDoubles.ReadSingleVibeContext(
                HotReloadConstants.VibeLogSourceSnapshotCaptured);
            Assert.That((string)captured["trigger"], Is.EqualTo(Trigger));
            Assert.That(captured["captureMs"].Type, Is.EqualTo(JTokenType.Integer));
        }

        /// <summary>
        /// What: an exception while reading the inputs reaches the caller, and the gate stays not
        /// started, so neither a capture nor a hold is left behind and the next caller tries again.
        /// </summary>
        [Test]
        public void StartIfNeeded_WhenReadingInputsThrows_RethrowsAndStaysNotStarted()
        {
            InvalidOperationException failure = new InvalidOperationException("The inputs could not be read.");
            bool throwOnRead = true;
            HotReloadSourceSnapshotCapture gate = new HotReloadSourceSnapshotCapture(
                () =>
                {
                    _inputReads++;
                    if (throwOnRead)
                    {
                        throw failure;
                    }

                    return HotReloadSourceSnapshotCaptureTestDoubles.FixedInputs();
                },
                RunCapture,
                _background.Run,
                () => _nowTicks);

            InvalidOperationException thrown =
                Assert.Throws<InvalidOperationException>(() => gate.StartIfNeeded(Trigger));
            Assert.That(thrown, Is.SameAs(failure));
            Assert.That(gate.IsInFlight, Is.False);
            Assert.That(gate.IsHoldingCompileCompletion, Is.False);
            Assert.That(_background.StartCount, Is.EqualTo(0));

            throwOnRead = false;
            gate.StartIfNeeded(Trigger);
            Assert.That(_background.StartCount, Is.EqualTo(1));
            _background.RunHeldWork();
        }

        /// <summary>
        /// What: an apply that arrives while a capture runs completes only after that capture does.
        /// </summary>
        [Test]
        public async Task EnsureCapturedAsync_WhileInFlight_CompletesOnlyAfterTheCapture()
        {
            HotReloadSourceSnapshotCapture gate = CreateGate();
            gate.StartIfNeeded(Trigger);

            Task ensured = gate.EnsureCapturedAsync(HotReloadConstants.SourceSnapshotCaptureTriggerApply);
            Assert.That(ensured.IsCompleted, Is.False);

            _background.RunHeldWork();
            await ensured;

            Assert.That(ensured.Status, Is.EqualTo(TaskStatus.RanToCompletion));
            Assert.That(_background.StartCount, Is.EqualTo(1));
        }

        /// <summary>
        /// What: an apply waiting on a capture that throws gets that exception, and the gate is not
        /// marked captured, so the next caller starts again.
        /// </summary>
        [Test]
        public async Task EnsureCapturedAsync_WhenTheCaptureThrows_RethrowsAndLeavesItUnmarked()
        {
            InvalidOperationException failure = new InvalidOperationException("The capture failed.");
            _captureBody = _ => throw failure;
            HotReloadSourceSnapshotCapture gate = CreateGate();
            Task ensured = gate.EnsureCapturedAsync(HotReloadConstants.SourceSnapshotCaptureTriggerApply);

            _background.RunHeldWork();
            Exception thrown = null;
            try
            {
                await ensured;
            }
            catch (InvalidOperationException exception)
            {
                thrown = exception;
            }

            Assert.That(thrown, Is.SameAs(failure));
            gate.StartIfNeeded(Trigger);
            Assert.That(_background.StartCount, Is.EqualTo(2));
            _background.RunHeldWork();
        }

        /// <summary>
        /// What: a pause point that blocks on a capture another caller started waits on that same
        /// capture instead of starting a second one. EditMode cannot finish a capture while the
        /// main thread blocks, so the capture finishes first and the call must still not start
        /// another or read the inputs again.
        /// </summary>
        [Test]
        public void EnsureCapturedBlocking_WhileInFlight_ReturnsAfterTheCaptureCompletes()
        {
            HotReloadSourceSnapshotCapture gate = CreateGate();
            gate.StartIfNeeded(Trigger);
            _background.RunHeldWork();

            gate.EnsureCapturedBlocking(HotReloadConstants.SourceSnapshotCaptureTriggerPausePoint);

            Assert.That(_background.StartCount, Is.EqualTo(1));
            Assert.That(_inputReads, Is.EqualTo(1));
        }

        /// <summary>
        /// What: a pause point that finds no capture started starts one and returns once it ran.
        /// </summary>
        [Test]
        public void EnsureCapturedBlocking_NotStarted_StartsAndWaits()
        {
            int captureRuns = 0;
            HotReloadSourceSnapshotCapture gate = HotReloadSourceSnapshotCaptureTestDoubles.CompletingSynchronously(() =>
            {
                captureRuns++;
                return true;
            });

            gate.EnsureCapturedBlocking(HotReloadConstants.SourceSnapshotCaptureTriggerPausePoint);

            Assert.That(captureRuns, Is.EqualTo(1));
            Assert.That(gate.IsInFlight, Is.False);
            Assert.That(CountEntries(HotReloadConstants.VibeLogSourceSnapshotCaptured), Is.EqualTo(1));
        }

        /// <summary>
        /// What: a blocking reader gets the exception of a capture that failed.
        /// </summary>
        [Test]
        public void EnsureCapturedBlocking_WhenTheCaptureThrows_PassesTheExceptionOn()
        {
            InvalidOperationException failure = new InvalidOperationException("The capture failed.");
            HotReloadSourceSnapshotCapture gate = HotReloadSourceSnapshotCaptureTestDoubles.CompletingSynchronously(
                () => throw failure);

            InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(
                () => gate.EnsureCapturedBlocking(HotReloadConstants.SourceSnapshotCaptureTriggerPausePoint));

            Assert.That(thrown, Is.SameAs(failure));
        }

        /// <summary>
        /// What: the compile completion is held while a capture has run for less than 30 seconds.
        /// </summary>
        [Test]
        public void IsHoldingCompileCompletion_WithinTheCap_IsTrue()
        {
            HotReloadSourceSnapshotCapture gate = CreateGate();
            gate.StartIfNeeded(Trigger);

            _nowTicks = HoldCapTicks - 1;

            Assert.That(gate.IsHoldingCompileCompletion, Is.True);
            _background.RunHeldWork();
        }

        /// <summary>
        /// What: once a capture has run for 30 seconds the compile completion is no longer held, and
        /// that is logged once however often it is asked; the capture still counts as in flight.
        /// </summary>
        [Test]
        public void IsHoldingCompileCompletion_PastTheCap_IsFalseAndLogsOnce()
        {
            HotReloadSourceSnapshotCapture gate = CreateGate();
            gate.StartIfNeeded(Trigger);

            _nowTicks = HoldCapTicks;

            Assert.That(gate.IsHoldingCompileCompletion, Is.False);
            Assert.That(gate.IsHoldingCompileCompletion, Is.False);
            Assert.That(gate.IsInFlight, Is.True);
            Assert.That(CountEntries(HotReloadConstants.VibeLogSourceSnapshotCaptureHoldCapExceeded), Is.EqualTo(1));
            _background.RunHeldWork();
        }

        /// <summary>
        /// What: nothing holds the compile completion before a capture starts, after one succeeded,
        /// after one failed, or after one was cancelled.
        /// </summary>
        [Test]
        public void IsHoldingCompileCompletion_WhenNothingRuns_IsFalse()
        {
            HotReloadSourceSnapshotCapture notStarted = CreateGate();
            Assert.That(notStarted.IsHoldingCompileCompletion, Is.False, "not started");

            HotReloadSourceSnapshotCapture succeeded = CreateGate();
            succeeded.StartIfNeeded(Trigger);
            _background.RunHeldWork();
            Assert.That(succeeded.IsHoldingCompileCompletion, Is.False, "succeeded");

            _captureBody = _ => throw new InvalidOperationException("The capture failed.");
            HotReloadSourceSnapshotCapture failed = CreateGate();
            failed.StartIfNeeded(Trigger);
            _background.RunHeldWork();
            Assert.That(failed.IsHoldingCompileCompletion, Is.False, "failed");

            _captureBody = cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return true;
            };
            HotReloadSourceSnapshotCapture cancelled = CreateGate();
            cancelled.StartIfNeeded(Trigger);
            cancelled.CancelInFlight();
            _background.RunHeldWork();
            Assert.That(cancelled.IsHoldingCompileCompletion, Is.False, "cancelled");
        }

        /// <summary>
        /// What: cancelling signals the running capture's token without waiting for it, and the
        /// capture that stops leaves the gate not captured and not in flight.
        /// </summary>
        [Test]
        public void CancelInFlight_CancelsTheTokenAndLeavesTheGateUnmarked()
        {
            CancellationToken seenToken = CancellationToken.None;
            _captureBody = cancellationToken =>
            {
                seenToken = cancellationToken;
                cancellationToken.ThrowIfCancellationRequested();
                return true;
            };
            HotReloadSourceSnapshotCapture gate = CreateGate();
            Task<bool> task = gate.StartIfNeeded(Trigger);

            gate.CancelInFlight();
            Assert.That(gate.IsInFlight, Is.True, "in flight until the capture stops");
            _background.RunHeldWork();

            Assert.That(seenToken.IsCancellationRequested, Is.True);
            Assert.That(task.IsCompleted, Is.True);
            Assert.That(task.Status, Is.Not.EqualTo(TaskStatus.RanToCompletion));
            Assert.That(gate.IsInFlight, Is.False);
            Assert.That(CountEntries(HotReloadConstants.VibeLogSourceSnapshotCaptured), Is.EqualTo(0));
        }

        private HotReloadSourceSnapshotCapture CreateGate()
        {
            return new HotReloadSourceSnapshotCapture(
                () =>
                {
                    _inputReads++;
                    return HotReloadSourceSnapshotCaptureTestDoubles.FixedInputs();
                },
                RunCapture,
                _background.Run,
                () => _nowTicks);
        }

        private bool RunCapture(HotReloadSnapshotCaptureInputs inputs, CancellationToken cancellationToken)
        {
            _captureRuns++;
            return _captureBody(cancellationToken);
        }

        private static int CountEntries(string operation)
        {
            return JArray.Parse(VibeLogger.GetLogsForAi(operation)).Count;
        }
    }
}
