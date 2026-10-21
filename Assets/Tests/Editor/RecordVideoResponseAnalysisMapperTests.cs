using System.Collections.Generic;
using NUnit.Framework;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Verifies which analysis fields a record-video response carries.
    /// </summary>
    public sealed class RecordVideoResponseAnalysisMapperTests
    {
        /// <summary>
        /// What: a snapshot without analysis or marks leaves every analysis field unset.
        /// </summary>
        [Test]
        public void Apply_WithoutAnalysisOrMarks_LeavesFieldsUnset()
        {
            RecordVideoResponse response = new RecordVideoResponse();

            RecordVideoResponseAnalysisMapper.Apply(response, Snapshot(null, new double[0]));

            Assert.That(response.AnalysisPath, Is.Null);
            Assert.That(response.FrameChange, Is.Null);
            Assert.That(response.BurstSheetPath, Is.Null);
            Assert.That(response.BurstSheetColumns, Is.Null);
            Assert.That(response.MarkSeconds, Is.Null);
        }

        /// <summary>
        /// What: a mark without analysis (a mark response) carries only the mark times.
        /// </summary>
        [Test]
        public void Apply_WithMarksOnly_CarriesMarkSeconds()
        {
            RecordVideoResponse response = new RecordVideoResponse();

            RecordVideoResponseAnalysisMapper.Apply(response, Snapshot(null, new[] { 1.5 }));

            Assert.That(response.MarkSeconds, Is.EqualTo(new[] { 1.5 }));
            Assert.That(response.AnalysisPath, Is.Null);
        }

        /// <summary>
        /// What: an analysis without a burst sheet carries the frame change summary but no sheet path.
        /// </summary>
        [Test]
        public void Apply_WithoutBurst_CarriesFrameChangeOnly()
        {
            RecordingAnalysisDocument document = new RecordingAnalysisDocument
            {
                Summary = new FrameChangeSummary { CapturedFrameCount = 1 }
            };
            RecordVideoResponse response = new RecordVideoResponse();

            RecordVideoResponseAnalysisMapper.Apply(response, Snapshot(new RecordingAnalysisResult("clip_frames.json", document), new double[0]));

            Assert.That(response.AnalysisPath, Is.EqualTo("clip_frames.json"));
            Assert.That(response.FrameChange.CapturedFrameCount, Is.EqualTo(1));
            Assert.That(response.BurstSheetPath, Is.Null);
            Assert.That(response.BurstAnchor, Is.Null);
        }

        /// <summary>
        /// What: a mark-anchored burst carries its 32 frames in order, the anchor, and three alternating cells.
        /// </summary>
        [Test]
        public void Apply_WithMarkedBurst_CarriesSheetFramesInOrder()
        {
            List<SheetFrame> frames = new List<SheetFrame>();
            for (int i = 0; i < 32; i++)
            {
                frames.Add(new SheetFrame { CapturedIndex = 10 + i, EncodedFrame = 20 + i, Seconds = i * 0.033 });
            }

            RecordingAnalysisDocument document = new RecordingAnalysisDocument
            {
                Summary = new FrameChangeSummary
                {
                    TopAlternatingCells = new List<AlternatingCell>
                    {
                        new AlternatingCell { Count = 5 },
                        new AlternatingCell { Count = 4 },
                        new AlternatingCell { Count = 3 }
                    }
                },
                BurstAnchor = "mark",
                MarkSeconds = new List<double> { 0.5 },
                Burst = new SheetInfo { Path = "clip_burst.png", Columns = 4, Frames = frames }
            };
            RecordVideoResponse response = new RecordVideoResponse();

            RecordVideoResponseAnalysisMapper.Apply(response, Snapshot(new RecordingAnalysisResult("clip_frames.json", document), new[] { 0.5 }));

            Assert.That(response.BurstSheetPath, Is.EqualTo("clip_burst.png"));
            Assert.That(response.BurstSheetColumns, Is.EqualTo(4));
            Assert.That(response.BurstAnchor, Is.EqualTo("mark"));
            Assert.That(response.BurstFrames.Count, Is.EqualTo(32));
            Assert.That(response.BurstFrames[0].CapturedIndex, Is.EqualTo(10));
            Assert.That(response.BurstFrames[31].CapturedIndex, Is.EqualTo(41));
            Assert.That(response.FrameChange.TopAlternatingCells.Count, Is.EqualTo(3));
            Assert.That(response.MarkSeconds, Is.EqualTo(new[] { 0.5 }));
        }

        private static VideoRecordingSnapshot Snapshot(RecordingAnalysisResult analysis, IReadOnlyList<double> markSeconds)
        {
            return new VideoRecordingSnapshot("clip.mp4", 4, 2, 30, 1, 0, 1.0, "cli", false, "medium", analysis, markSeconds);
        }
    }
}
