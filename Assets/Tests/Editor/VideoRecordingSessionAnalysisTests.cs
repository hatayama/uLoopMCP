using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Verifies what a stopped recording leaves beside the video, for each stop path and number of
    /// captured frames, with the real frame analyzer. Whatever the path, the recording stops, the
    /// encoder is disposed, the sidecar sits beside the video, and no burst is built from fewer than two frames.
    /// </summary>
    public sealed class VideoRecordingSessionAnalysisTests
    {
        // One and a half frames per tick: every tick is then due at least one frame despite floating-point
        // rounding, so each tick captures exactly once.
        private const double FrameInterval = 0.05;
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);

        private string _directory;
        private string _videoPath;
        private double _now;
        private FakeVideoFrameEncoder _encoder;
        private PaintingFrameSource _frameSource;
        private VideoRecordingSession _session;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "uloop-session-analysis-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _videoPath = Path.Combine(_directory, "clip.mp4");
            _now = 0.0;
            _encoder = new FakeVideoFrameEncoder();
            _frameSource = new PaintingFrameSource { Color = Black };
            _session = new VideoRecordingSession(
                _encoder,
                _frameSource,
                () => _now,
                30,
                60.0,
                _videoPath,
                RecordVideoQuality.medium,
                new RecordingFrameAnalyzer(_encoder.Width, _encoder.Height));
        }

        [TearDown]
        public void TearDown()
        {
            _session.Stop("teardown");
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        /// <summary>
        /// What: a recording stopped before any frame was captured writes a sidecar with no samples and no burst.
        /// </summary>
        [Test]
        public void Stop_WithNoCapturedFrames_WritesAnalysisWithoutBurst()
        {
            _session.Stop(RecordVideoConstants.StoppedByCli);

            RecordingAnalysisResult analysis = AssertStoppedWithSidecar();
            Assert.That(analysis.Document.Samples, Is.Empty);
            AssertNoBurst(analysis);
            Assert.That(analysis.Document.Timeline, Is.Null);
        }

        /// <summary>
        /// What: one captured frame gives one sample and no burst.
        /// </summary>
        [Test]
        public void Stop_WithOneCapturedFrame_HasNoBurst()
        {
            CaptureFrames(1);

            _session.Stop(RecordVideoConstants.StoppedByCli);

            RecordingAnalysisResult analysis = AssertStoppedWithSidecar();
            Assert.That(analysis.Document.Samples.Count, Is.EqualTo(1));
            AssertNoBurst(analysis);
            Assert.That(analysis.Document.Timeline.Frames.Count, Is.EqualTo(1));
            Assert.That(File.Exists(analysis.Document.Timeline.Path), Is.True);
        }

        /// <summary>
        /// What: frames that never change give measurements of zero and no burst.
        /// </summary>
        [Test]
        public void Stop_WithStaticFrames_HasNoBurst()
        {
            CaptureFrames(10);

            _session.Stop(RecordVideoConstants.StoppedByCli);

            RecordingAnalysisResult analysis = AssertStoppedWithSidecar();
            Assert.That(analysis.Document.Samples.Count, Is.EqualTo(10));
            Assert.That(analysis.Document.Summary.FramesChangedVsPrevious, Is.EqualTo(0));
            AssertNoBurst(analysis);
        }

        /// <summary>
        /// What: a still recording longer than a second writes a timeline sheet beside the video, in time order, even without a burst.
        /// </summary>
        [Test]
        public void Stop_WithStaticFrames_WritesTimelineSheetInTimeOrder()
        {
            CaptureFrames(30);

            _session.Stop(RecordVideoConstants.StoppedByCli);

            RecordingAnalysisResult analysis = AssertStoppedWithSidecar();
            AssertNoBurst(analysis);
            SheetInfo timeline = analysis.Document.Timeline;
            Assert.That(timeline.Path, Is.EqualTo(Path.Combine(_directory, "clip_timeline.png")));
            Assert.That(File.Exists(timeline.Path), Is.True);
            Assert.That(timeline.Columns, Is.EqualTo(RecordingFrameAnalyzer.TimelineColumns));
            Assert.That(timeline.Frames.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(timeline.Frames[0].CapturedIndex, Is.EqualTo(0));
            for (int i = 1; i < timeline.Frames.Count; i++)
            {
                Assert.That(timeline.Frames[i].Seconds - timeline.Frames[i - 1].Seconds,
                    Is.GreaterThanOrEqualTo(TimelineFrameSampler.BaseIntervalSeconds));
            }
        }

        /// <summary>
        /// What: a change without a mark writes a burst sheet anchored on the largest change.
        /// </summary>
        [Test]
        public void Stop_WithSpike_WritesBurstSheet()
        {
            CaptureFrames(5);
            _frameSource.Color = White;
            CaptureFrames(5);

            _session.Stop(RecordVideoConstants.StoppedByCli);

            RecordingAnalysisResult analysis = AssertStoppedWithSidecar();
            Assert.That(analysis.Document.BurstAnchor, Is.EqualTo("largest-change"));
            Assert.That(analysis.Document.Burst.Frames.Count, Is.EqualTo(10));
            Assert.That(File.Exists(analysis.Document.Burst.Path), Is.True);
            Assert.That(Path.GetDirectoryName(analysis.Document.Burst.Path), Is.EqualTo(_directory));
        }

        /// <summary>
        /// What: a mark anchors the burst, and a larger change after it does not rebuild the burst.
        /// </summary>
        [Test]
        public void Stop_AfterMark_WritesMarkAnchoredBurst()
        {
            CaptureFrames(5);
            _session.Mark();
            CaptureFrames(3);
            _frameSource.Color = White;
            CaptureFrames(3);

            _session.Stop(RecordVideoConstants.StoppedByCli);

            RecordingAnalysisResult analysis = AssertStoppedWithSidecar();
            Assert.That(analysis.Document.BurstAnchor, Is.EqualTo("mark"));
            Assert.That(analysis.Document.Burst.Frames[0].CapturedIndex, Is.EqualTo(0));
            Assert.That(analysis.Document.Burst.Frames.Count, Is.EqualTo(11));
            Assert.That(analysis.Document.MarkSeconds.Count, Is.EqualTo(1));
        }

        /// <summary>
        /// What: stopping right after a mark builds the burst from the frames captured before the mark.
        /// </summary>
        [Test]
        public void Stop_RightAfterMark_UsesRingOnly()
        {
            CaptureFrames(5);
            _session.Mark();

            _session.Stop(RecordVideoConstants.StoppedByCli);

            RecordingAnalysisResult analysis = AssertStoppedWithSidecar();
            Assert.That(analysis.Document.BurstAnchor, Is.EqualTo("mark"));
            Assert.That(analysis.Document.Burst.Frames.Count, Is.EqualTo(5));
        }

        /// <summary>
        /// What: a mark with fewer than two captured frames builds no burst but keeps the mark time.
        /// </summary>
        [Test]
        public void Stop_AfterMarkWithFewerThanTwoFrames_HasNoBurstButKeepsMarkSeconds()
        {
            CaptureFrames(1);
            _session.Mark();

            _session.Stop(RecordVideoConstants.StoppedByCli);

            RecordingAnalysisResult analysis = AssertStoppedWithSidecar();
            AssertNoBurst(analysis);
            Assert.That(analysis.Document.MarkSeconds.Count, Is.EqualTo(1));
            Assert.That(_session.Snapshot().MarkSeconds.Count, Is.EqualTo(1));
        }

        /// <summary>
        /// What: a recording stopped because its frame texture was destroyed still writes the analysis.
        /// </summary>
        [Test]
        public void Stop_AfterFrameTextureLost_StillWritesAnalysis()
        {
            CaptureFrames(3);
            UnityEngine.Object.DestroyImmediate(_session.FrameTextureForTests);
            _now += FrameInterval;

            _session.Tick();

            Assert.That(_session.Snapshot().StoppedBy, Is.EqualTo(RecordVideoConstants.StoppedByFrameTextureLost));
            RecordingAnalysisResult analysis = AssertStoppedWithSidecar();
            Assert.That(analysis.Document.Samples.Count, Is.EqualTo(3));
        }

        private void CaptureFrames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                _now += FrameInterval;
                _session.Tick();
            }
        }

        private RecordingAnalysisResult AssertStoppedWithSidecar()
        {
            VideoRecordingSnapshot snapshot = _session.Snapshot();
            Assert.That(snapshot.IsRecording, Is.False);
            Assert.That(_encoder.DisposeCallCount, Is.EqualTo(1));
            Assert.That(snapshot.Analysis, Is.Not.Null);
            Assert.That(snapshot.Analysis.Path, Is.EqualTo(Path.Combine(_directory, "clip_frames.json")));
            Assert.That(File.Exists(snapshot.Analysis.Path), Is.True);
            return snapshot.Analysis;
        }

        private void AssertNoBurst(RecordingAnalysisResult analysis)
        {
            Assert.That(analysis.Document.Burst, Is.Null);
            Assert.That(analysis.Document.BurstAnchor, Is.Null);
            Assert.That(File.Exists(Path.Combine(_directory, "clip_burst.png")), Is.False);
        }

        private sealed class FakeVideoFrameEncoder : IVideoFrameEncoder
        {
            internal int DisposeCallCount { get; private set; }

            public int Width => 2;

            public int Height => 2;

            public bool AddFrame(Texture2D texture)
            {
                return true;
            }

            public void Dispose()
            {
                DisposeCallCount++;
            }
        }

        private sealed class PaintingFrameSource : IGameViewFrameSource
        {
            internal Color32 Color { get; set; }

            public bool IsSourceClosed => false;

            public bool TryReadFrame(Texture2D destination)
            {
                Color32[] pixels = new Color32[destination.width * destination.height];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = Color;
                }

                destination.SetPixels32(pixels);
                destination.Apply();
                return true;
            }
        }
    }
}
