using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Verifies session tick pacing, skip counting, and stop/dispose behavior.
    /// </summary>
    public sealed class VideoRecordingSessionTests
    {
        private VideoRecordingSession _session;
        private FakeVideoFrameEncoder _encoder;
        private FakeGameViewFrameSource _frameSource;
        private FakeRecordingFrameAnalyzer _analyzer;
        private double _now;

        [SetUp]
        public void SetUp()
        {
            _now = 0.0;
            _encoder = new FakeVideoFrameEncoder();
            _frameSource = new FakeGameViewFrameSource();
            _analyzer = new FakeRecordingFrameAnalyzer();
            _session = new VideoRecordingSession(
                _encoder,
                _frameSource,
                () => _now,
                30,
                60.0,
                "/tmp/gameview.mp4",
                RecordVideoQuality.medium,
                _analyzer);
        }

        [TearDown]
        public void TearDown()
        {
            if (_session == null)
            {
                return;
            }

            _session.Stop("teardown");
            _session = null;
        }

        /// <summary>
        /// What: a 0.5s tick at 30 fps encodes 15 frames when the Game View read succeeds.
        /// </summary>
        [Test]
        public void Tick_WhenHalfSecondElapsed_Encodes15Frames()
        {
            _now = 0.5;

            _session.Tick();

            Assert.That(_encoder.AddFrameCallCount, Is.EqualTo(15));
            Assert.That(_session.Snapshot().EncodedFrameCount, Is.EqualTo(15));
        }

        /// <summary>
        /// What: a failed Game View read skips the due frames without calling AddFrame.
        /// </summary>
        [Test]
        public void Tick_WhenFrameSourceFails_SkipsDueFrames()
        {
            _frameSource.ReadSucceeds = false;
            _now = 0.5;

            _session.Tick();

            VideoRecordingSnapshot snapshot = _session.Snapshot();
            Assert.That(_encoder.AddFrameCallCount, Is.EqualTo(0));
            Assert.That(snapshot.EncodedFrameCount, Is.EqualTo(0));
            Assert.That(snapshot.SkippedFrameCount, Is.EqualTo(15));
        }

        /// <summary>
        /// What: two ticks at the same clock time do not double-count already skipped slots.
        /// </summary>
        [Test]
        public void Tick_WhenFrameSourceFailsTwiceAtSameTime_DoesNotDoubleCountSkipped()
        {
            _frameSource.ReadSucceeds = false;
            _now = 0.5;

            _session.Tick();
            _session.Tick();

            VideoRecordingSnapshot snapshot = _session.Snapshot();
            Assert.That(snapshot.SkippedFrameCount, Is.EqualTo(15));
            Assert.That(snapshot.EncodedFrameCount, Is.EqualTo(0));
        }

        /// <summary>
        /// What: AddFrame false increments skipped frames and leaves encoded count unchanged.
        /// </summary>
        [Test]
        public void Tick_WhenAddFrameFails_CountsSkippedNotEncoded()
        {
            _encoder.AddFrameSucceeds = false;
            _now = 0.5;

            _session.Tick();

            VideoRecordingSnapshot snapshot = _session.Snapshot();
            Assert.That(_encoder.AddFrameCallCount, Is.EqualTo(15));
            Assert.That(snapshot.EncodedFrameCount, Is.EqualTo(0));
            Assert.That(snapshot.SkippedFrameCount, Is.EqualTo(15));
        }

        /// <summary>
        /// What: reaching max duration stops the session and disposes the encoder once.
        /// </summary>
        [Test]
        public void Tick_WhenMaxDurationReached_StopsAndDisposes()
        {
            _session.Stop("teardown");
            FakeVideoFrameEncoder encoder = new FakeVideoFrameEncoder();
            _encoder = encoder;
            _session = new VideoRecordingSession(
                encoder,
                _frameSource,
                () => _now,
                30,
                1.0,
                "/tmp/gameview.mp4",
                RecordVideoQuality.medium,
                _analyzer);
            _now = 1.0;

            _session.Tick();

            VideoRecordingSnapshot snapshot = _session.Snapshot();
            Assert.That(snapshot.IsRecording, Is.False);
            Assert.That(snapshot.StoppedBy, Is.EqualTo("max-duration"));
            Assert.That(encoder.DisposeCallCount, Is.EqualTo(1));
        }

        /// <summary>
        /// What: a closed frame source stops the session with the window-closed reason and encodes nothing.
        /// </summary>
        [Test]
        public void Tick_WhenSourceClosed_StopsWithWindowClosedReason()
        {
            _frameSource.IsSourceClosed = true;
            _now = 0.5;

            _session.Tick();

            VideoRecordingSnapshot snapshot = _session.Snapshot();
            Assert.That(snapshot.IsRecording, Is.False);
            Assert.That(snapshot.StoppedBy, Is.EqualTo("window-closed"));
            Assert.That(_encoder.AddFrameCallCount, Is.EqualTo(0));
            Assert.That(snapshot.SkippedFrameCount, Is.EqualTo(0));
        }

        /// <summary>
        /// What: max duration wins over a closed source so the existing stop reason does not change.
        /// </summary>
        [Test]
        public void Tick_WhenSourceClosedAfterMaxDuration_StopsWithMaxDurationReason()
        {
            _frameSource.IsSourceClosed = true;
            _now = 60.0;

            _session.Tick();

            Assert.That(_session.Snapshot().StoppedBy, Is.EqualTo("max-duration"));
        }

        /// <summary>
        /// What: a destroyed frame texture stops the session instead of throwing on every tick.
        /// </summary>
        [Test]
        public void Tick_WhenFrameTextureDestroyed_StopsWithFrameTextureLostReason()
        {
            UnityEngine.Object.DestroyImmediate(_session.FrameTextureForTests);
            _now = 0.5;

            Assert.DoesNotThrow(() => _session.Tick());

            VideoRecordingSnapshot snapshot = _session.Snapshot();
            Assert.That(snapshot.IsRecording, Is.False);
            Assert.That(snapshot.StoppedBy, Is.EqualTo("frame-texture-lost"));
            Assert.That(_encoder.AddFrameCallCount, Is.EqualTo(0));
        }

        /// <summary>
        /// What: the frame texture is marked HideAndDontSave so a texture created during Play Mode
        /// is not destroyed when Play Mode ends. The transition itself cannot run in an EditMode
        /// test and is covered by manual verification against a live Editor.
        /// </summary>
        [Test]
        public void Constructor_MarksFrameTextureHideAndDontSave()
        {
            Assert.That(_session.FrameTextureForTests.hideFlags, Is.EqualTo(HideFlags.HideAndDontSave));
        }

        /// <summary>
        /// What: a second Stop is a no-op so the encoder is disposed only once.
        /// </summary>
        [Test]
        public void Stop_WhenCalledTwice_DisposesOnce()
        {
            _session.Stop("cli");
            _session.Stop("cli");

            Assert.That(_encoder.DisposeCallCount, Is.EqualTo(1));
        }

        /// <summary>
        /// What: Snapshot reports the quality value passed to the constructor.
        /// </summary>
        [Test]
        public void Snapshot_ReturnsQualityPassedToConstructor()
        {
            _session.Stop("teardown");
            FakeVideoFrameEncoder encoder = new FakeVideoFrameEncoder();
            _encoder = encoder;
            _session = new VideoRecordingSession(
                encoder,
                _frameSource,
                () => _now,
                30,
                60.0,
                "/tmp/gameview.mp4",
                RecordVideoQuality.high,
                _analyzer);

            Assert.That(_session.Snapshot().Quality, Is.EqualTo("high"));
        }

        /// <summary>
        /// What: Snapshot elapsed seconds stay frozen after Stop even if the clock advances.
        /// </summary>
        [Test]
        public void Snapshot_AfterStop_KeepsElapsedSecondsFixed()
        {
            _now = 0.5;
            _session.Stop("cli");
            _now = 10.0;

            VideoRecordingSnapshot snapshot = _session.Snapshot();

            Assert.That(snapshot.ElapsedSeconds, Is.EqualTo(0.5).Within(0.0001));
            Assert.That(snapshot.IsRecording, Is.False);
        }

        /// <summary>
        /// What: a 0.5s tick that encodes 15 repeats of one captured frame analyzes that frame once, at encoded frame 0 and 0.5s.
        /// </summary>
        [Test]
        public void Tick_WhenFrameRepeats_AnalyzesCapturedFrameOnce()
        {
            _now = 0.5;

            _session.Tick();

            Assert.That(_encoder.AddFrameCallCount, Is.EqualTo(15));
            Assert.That(_analyzer.CapturedFrames.Count, Is.EqualTo(1));
            Assert.That(_analyzer.CapturedFrames[0].encodedFrame, Is.EqualTo(0));
            Assert.That(_analyzer.CapturedFrames[0].seconds, Is.EqualTo(0.5).Within(0.0001));
        }

        /// <summary>
        /// What: a tick whose frame read fails does not reach the analyzer.
        /// </summary>
        [Test]
        public void Tick_WhenFrameSourceFails_DoesNotAnalyze()
        {
            _frameSource.ReadSucceeds = false;
            _now = 0.5;

            _session.Tick();

            Assert.That(_analyzer.CapturedFrames, Is.Empty);
        }

        /// <summary>
        /// What: Mark passes the elapsed recording time to the analyzer once and appears in the snapshot.
        /// </summary>
        [Test]
        public void Mark_WhileRecording_MarksAnalyzerWithElapsedSeconds()
        {
            _now = 1.25;

            _session.Mark();

            Assert.That(_analyzer.MarkSeconds, Is.EqualTo(new[] { 1.25 }));
            Assert.That(_session.Snapshot().MarkSeconds, Is.EqualTo(new[] { 1.25 }));
        }

        /// <summary>
        /// What: Mark after Stop does nothing.
        /// </summary>
        [Test]
        public void Mark_AfterStop_DoesNothing()
        {
            _session.Stop("cli");

            _session.Mark();

            Assert.That(_analyzer.MarkSeconds, Is.Empty);
            Assert.That(_session.Snapshot().MarkSeconds, Is.Empty);
        }

        /// <summary>
        /// What: Stop finishes the analysis once with the recording's path and size, and the snapshot carries the result.
        /// </summary>
        [Test]
        public void Stop_FinishesAnalysisOnceAndSnapshotCarriesIt()
        {
            _session.Stop("cli");
            _session.Stop("cli");

            Assert.That(_analyzer.FinishCallCount, Is.EqualTo(1));
            Assert.That(_analyzer.FinishedVideoPath, Is.EqualTo("/tmp/gameview.mp4"));
            Assert.That(_session.Snapshot().Analysis, Is.SameAs(_analyzer.Result));
        }

        /// <summary>
        /// What: an analyzer that throws on Finish still lets the recording stop, with no analysis on the snapshot.
        /// </summary>
        [Test]
        public void Stop_WhenAnalyzerThrows_StillStops()
        {
            _analyzer.FinishThrows = true;

            _session.Stop("cli");

            VideoRecordingSnapshot snapshot = _session.Snapshot();
            Assert.That(snapshot.IsRecording, Is.False);
            Assert.That(snapshot.Analysis, Is.Null);
            Assert.That(_encoder.DisposeCallCount, Is.EqualTo(1));
            Assert.That(_session.FrameTextureForTests, Is.Null);
        }

        private sealed class FakeVideoFrameEncoder : IVideoFrameEncoder
        {
            internal int AddFrameCallCount { get; private set; }

            internal int DisposeCallCount { get; private set; }

            internal bool AddFrameSucceeds { get; set; } = true;

            public int Width => 2;

            public int Height => 2;

            public bool AddFrame(Texture2D texture)
            {
                AddFrameCallCount++;
                return AddFrameSucceeds;
            }

            public void Dispose()
            {
                DisposeCallCount++;
            }
        }

        private sealed class FakeRecordingFrameAnalyzer : IRecordingFrameAnalyzer
        {
            internal List<(int encodedFrame, double seconds)> CapturedFrames { get; } =
                new List<(int encodedFrame, double seconds)>();

            internal List<double> MarkSeconds { get; } = new List<double>();

            internal int FinishCallCount { get; private set; }

            internal string FinishedVideoPath { get; private set; }

            internal bool FinishThrows { get; set; }

            internal RecordingAnalysisResult Result { get; } =
                new RecordingAnalysisResult("/tmp/gameview_frames.json", new RecordingAnalysisDocument());

            public void OnCapturedFrame(Texture2D frame, int encodedFrame, double seconds)
            {
                CapturedFrames.Add((encodedFrame, seconds));
            }

            public void Mark(double seconds)
            {
                MarkSeconds.Add(seconds);
            }

            public RecordingAnalysisResult Finish(string videoPath, int width, int height, int frameRate)
            {
                FinishCallCount++;
                FinishedVideoPath = videoPath;
                if (FinishThrows)
                {
                    throw new InvalidOperationException("analysis failed");
                }

                return Result;
            }
        }

        private sealed class FakeGameViewFrameSource : IGameViewFrameSource
        {
            internal bool ReadSucceeds { get; set; } = true;

            public bool IsSourceClosed { get; set; }

            public bool TryReadFrame(Texture2D destination)
            {
                return ReadSucceeds;
            }
        }
    }
}
