using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Verifies writing and reading the recording analysis sidecar JSON.
    /// </summary>
    public sealed class RecordingAnalysisDocumentTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "RecordingAnalysisDocumentTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        /// <summary>
        /// What: a written document reads back with the same samples, summary, marks, and burst sheet.
        /// </summary>
        [Test]
        public void WriteThenTryRead_RoundTrips()
        {
            string path = Path.Combine(_directory, "clip_frames.json");
            RecordingAnalysisDocument document = new RecordingAnalysisDocument
            {
                VideoPath = "clip.mp4",
                Width = 1280,
                Height = 720,
                FrameRate = 30,
                TileWidth = 320,
                TileHeight = 180,
                GridColumns = 64,
                GridRows = 36,
                CellChangedThreshold = 8,
                Samples = new List<FrameChangeSample>
                {
                    new FrameChangeSample { CapturedIndex = 0, EncodedFrame = 0, Seconds = 0.0 },
                    new FrameChangeSample { CapturedIndex = 1, EncodedFrame = 2, Seconds = 0.07, ChangedCellsVsPrevious = 5, MeanVsPrevious = 0.25 }
                },
                Summary = new FrameChangeSummary
                {
                    CapturedFrameCount = 2,
                    LargestChange = new LargestFrameChange { CapturedIndex = 1, ChangedCellsVsPrevious = 5 },
                    TopAlternatingCells = new List<AlternatingCell> { new AlternatingCell { Column = 3, Row = 4, Count = 2 } }
                },
                BurstAnchor = "mark",
                MarkSeconds = new List<double> { 0.05 },
                Burst = new SheetInfo
                {
                    Path = "clip_burst.png",
                    Columns = 4,
                    Frames = new List<SheetFrame> { new SheetFrame { CapturedIndex = 1, EncodedFrame = 2, Seconds = 0.07 } }
                }
            };

            RecordingAnalysisDocument.Write(path, document);
            RecordingAnalysisDocument read = RecordingAnalysisDocument.TryRead(path);

            Assert.That(read, Is.Not.Null);
            Assert.That(read.Version, Is.EqualTo(RecordingAnalysisDocument.CurrentVersion));
            Assert.That(read.Width, Is.EqualTo(1280));
            Assert.That(read.Samples.Count, Is.EqualTo(2));
            Assert.That(read.Samples[1].ChangedCellsVsPrevious, Is.EqualTo(5));
            Assert.That(read.Samples[1].MeanVsPrevious, Is.EqualTo(0.25));
            Assert.That(read.Summary.LargestChange.CapturedIndex, Is.EqualTo(1));
            Assert.That(read.Summary.TopAlternatingCells[0].Row, Is.EqualTo(4));
            Assert.That(read.BurstAnchor, Is.EqualTo("mark"));
            Assert.That(read.MarkSeconds, Is.EqualTo(new[] { 0.05 }));
            Assert.That(read.Burst.Path, Is.EqualTo("clip_burst.png"));
            Assert.That(read.Burst.Frames[0].EncodedFrame, Is.EqualTo(2));
            Assert.That(read.Timeline, Is.Null);
        }

        /// <summary>
        /// What: a document without a burst sheet keeps Burst null after a round trip.
        /// </summary>
        [Test]
        public void WriteThenTryRead_WithoutBurst_KeepsBurstNull()
        {
            string path = Path.Combine(_directory, "clip_frames.json");

            RecordingAnalysisDocument.Write(path, new RecordingAnalysisDocument { VideoPath = "clip.mp4" });
            RecordingAnalysisDocument read = RecordingAnalysisDocument.TryRead(path);

            Assert.That(read.Burst, Is.Null);
            Assert.That(read.BurstAnchor, Is.Null);
            Assert.That(read.MarkSeconds, Is.Empty);
        }

        /// <summary>
        /// What: broken JSON reads as null instead of throwing.
        /// </summary>
        [Test]
        public void TryRead_WhenJsonIsBroken_ReturnsNull()
        {
            string path = Path.Combine(_directory, "clip_frames.json");
            File.WriteAllText(path, "{ not json");

            Assert.That(RecordingAnalysisDocument.TryRead(path), Is.Null);
        }

        /// <summary>
        /// What: a missing file reads as null.
        /// </summary>
        [Test]
        public void TryRead_WhenFileIsMissing_ReturnsNull()
        {
            Assert.That(RecordingAnalysisDocument.TryRead(Path.Combine(_directory, "missing.json")), Is.Null);
        }
    }
}
