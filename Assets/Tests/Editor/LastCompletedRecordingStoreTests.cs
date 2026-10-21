using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Verifies SessionState round-trip for the last automatically stopped recording.
    /// </summary>
    public sealed class LastCompletedRecordingStoreTests
    {
        private LastCompletedRecording _previousRecording;
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _previousRecording = LastCompletedRecordingTestState.TakeAndClear();
            _directory = Path.Combine(Path.GetTempPath(), "uloop-last-recording-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            LastCompletedRecordingTestState.Restore(_previousRecording);
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        /// <summary>
        /// What: Save, TryRead, MarkReported, and Clear keep reported and empty states consistent.
        /// </summary>
        [Test]
        public void SaveTryReadMarkReportedClear_RoundTripsReportedAndEmptyState()
        {
            VideoRecordingSnapshot snapshot = new VideoRecordingSnapshot(
                "/tmp/gameview.webm",
                1280,
                720,
                30,
                90,
                0,
                3.0,
                RecordVideoConstants.StoppedByMaxDuration,
                false,
                RecordVideoQuality.high.ToString(),
                null,
                null);

            LastCompletedRecordingStore.Save(snapshot);
            LastCompletedRecording unread = LastCompletedRecordingStore.TryRead();
            Assert.That(unread.HasValue, Is.True);
            Assert.That(unread.IsReported, Is.False);
            Assert.That(unread.Snapshot.OutputPath, Is.EqualTo(snapshot.OutputPath));
            Assert.That(unread.Snapshot.Width, Is.EqualTo(snapshot.Width));
            Assert.That(unread.Snapshot.Height, Is.EqualTo(snapshot.Height));
            Assert.That(unread.Snapshot.FrameRate, Is.EqualTo(snapshot.FrameRate));
            Assert.That(unread.Snapshot.EncodedFrameCount, Is.EqualTo(snapshot.EncodedFrameCount));
            Assert.That(unread.Snapshot.SkippedFrameCount, Is.EqualTo(snapshot.SkippedFrameCount));
            Assert.That(unread.Snapshot.ElapsedSeconds, Is.EqualTo(snapshot.ElapsedSeconds).Within(0.001));
            Assert.That(unread.Snapshot.StoppedBy, Is.EqualTo(snapshot.StoppedBy));
            Assert.That(unread.Snapshot.Quality, Is.EqualTo(snapshot.Quality));

            LastCompletedRecordingStore.MarkReported();
            LastCompletedRecording reported = LastCompletedRecordingStore.TryRead();
            Assert.That(reported.HasValue, Is.True);
            Assert.That(reported.IsReported, Is.True);

            LastCompletedRecordingStore.Clear();
            LastCompletedRecording cleared = LastCompletedRecordingStore.TryRead();
            Assert.That(cleared.HasValue, Is.False);
        }

        /// <summary>
        /// What: a saved analysis path reads back as the analysis in that file, including its mark times.
        /// </summary>
        [Test]
        public void SaveTryRead_WithAnalysisPath_RestoresAnalysis()
        {
            string analysisPath = Path.Combine(_directory, "clip_frames.json");
            RecordingAnalysisDocument document = new RecordingAnalysisDocument
            {
                VideoPath = "clip.mp4",
                MarkSeconds = new List<double> { 0.75 },
                Summary = new FrameChangeSummary { CapturedFrameCount = 12 }
            };
            RecordingAnalysisDocument.Write(analysisPath, document);

            LastCompletedRecordingStore.Save(CreateSnapshot(new RecordingAnalysisResult(analysisPath, document)));
            LastCompletedRecording read = LastCompletedRecordingStore.TryRead();

            Assert.That(read.Snapshot.Analysis, Is.Not.Null);
            Assert.That(read.Snapshot.Analysis.Path, Is.EqualTo(analysisPath));
            Assert.That(read.Snapshot.Analysis.Document.Summary.CapturedFrameCount, Is.EqualTo(12));
            Assert.That(read.Snapshot.MarkSeconds, Is.EqualTo(new[] { 0.75 }));
        }

        /// <summary>
        /// What: a later save without analysis does not inherit the analysis path of an earlier save.
        /// </summary>
        [Test]
        public void Save_WithoutAnalysis_DoesNotInheritEarlierAnalysisPath()
        {
            string analysisPath = Path.Combine(_directory, "clip_frames.json");
            RecordingAnalysisDocument document = new RecordingAnalysisDocument { VideoPath = "clip.mp4" };
            RecordingAnalysisDocument.Write(analysisPath, document);
            LastCompletedRecordingStore.Save(CreateSnapshot(new RecordingAnalysisResult(analysisPath, document)));

            LastCompletedRecordingStore.Save(CreateSnapshot(null));

            Assert.That(LastCompletedRecordingStore.TryRead().Snapshot.Analysis, Is.Null);
        }

        /// <summary>
        /// What: an analysis path whose file is gone reads as no analysis while the other fields still read.
        /// </summary>
        [Test]
        public void TryRead_WhenAnalysisFileIsMissing_KeepsOtherFields()
        {
            string analysisPath = Path.Combine(_directory, "clip_frames.json");
            RecordingAnalysisDocument document = new RecordingAnalysisDocument { VideoPath = "clip.mp4" };
            LastCompletedRecordingStore.Save(CreateSnapshot(new RecordingAnalysisResult(analysisPath, document)));

            LastCompletedRecording read = LastCompletedRecordingStore.TryRead();

            Assert.That(read.HasValue, Is.True);
            Assert.That(read.Snapshot.Analysis, Is.Null);
            Assert.That(read.Snapshot.MarkSeconds, Is.Null);
            Assert.That(read.Snapshot.EncodedFrameCount, Is.EqualTo(90));
        }

        private static VideoRecordingSnapshot CreateSnapshot(RecordingAnalysisResult analysis)
        {
            return new VideoRecordingSnapshot(
                "/tmp/gameview.mp4",
                1280,
                720,
                30,
                90,
                0,
                3.0,
                RecordVideoConstants.StoppedByMaxDuration,
                false,
                RecordVideoQuality.high.ToString(),
                analysis,
                analysis?.Document.MarkSeconds);
        }
    }
}
