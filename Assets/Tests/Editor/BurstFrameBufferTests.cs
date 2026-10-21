using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Verifies which consecutive frames the burst sheet keeps around a mark or the largest change.
    /// Frames are numbered from 1 in these tests so the ranges read like the plan's frame numbers.
    /// </summary>
    public sealed class BurstFrameBufferTests
    {
        /// <summary>
        /// What: frames without any change give no burst and no anchor.
        /// </summary>
        [Test]
        public void Frames_WhenNothingChanges_IsEmpty()
        {
            BurstFrameBuffer buffer = new BurstFrameBuffer();

            Feed(buffer, 1, 40, 0);

            Assert.That(buffer.Frames, Is.Empty);
            Assert.That(buffer.Anchor, Is.EqualTo(BurstAnchor.none));
        }

        /// <summary>
        /// What: a spike at frame 20 keeps frames 4-20 and 21-35, 32 consecutive frames anchored on the largest change.
        /// </summary>
        [Test]
        public void Frames_WhenSpikeAtFrame20_KeepsSixteenBeforeAndFifteenAfter()
        {
            BurstFrameBuffer buffer = new BurstFrameBuffer();

            Feed(buffer, 1, 19, 0);
            Feed(buffer, 20, 20, 10);
            Feed(buffer, 21, 50, 0);

            Assert.That(Indices(buffer), Is.EqualTo(Range(4, 35)));
            Assert.That(buffer.Anchor, Is.EqualTo(BurstAnchor.largestChange));
        }

        /// <summary>
        /// What: a spike at frame 3 keeps the two frames before it and fifteen after it.
        /// </summary>
        [Test]
        public void Frames_WhenSpikeEarly_KeepsWhatIsAvailableBefore()
        {
            BurstFrameBuffer buffer = new BurstFrameBuffer();

            Feed(buffer, 1, 2, 0);
            Feed(buffer, 3, 3, 10);
            Feed(buffer, 4, 40, 0);

            Assert.That(Indices(buffer), Is.EqualTo(Range(1, 18)));
        }

        /// <summary>
        /// What: a larger spike while the tail is still being collected rebuilds the burst around the new spike.
        /// </summary>
        [Test]
        public void Frames_WhenLargerSpikeDuringTail_RebuildsAroundIt()
        {
            BurstFrameBuffer buffer = new BurstFrameBuffer();

            Feed(buffer, 1, 19, 0);
            Feed(buffer, 20, 20, 10);
            Feed(buffer, 21, 24, 0);
            Feed(buffer, 25, 25, 20);
            Feed(buffer, 26, 60, 0);

            Assert.That(Indices(buffer), Is.EqualTo(Range(9, 40)));
        }

        /// <summary>
        /// What: a later spike of the same size does not replace the first one.
        /// </summary>
        [Test]
        public void Frames_WhenEqualSpikeLater_KeepsFirst()
        {
            BurstFrameBuffer buffer = new BurstFrameBuffer();

            Feed(buffer, 1, 19, 0);
            Feed(buffer, 20, 20, 10);
            Feed(buffer, 21, 39, 0);
            Feed(buffer, 40, 40, 10);
            Feed(buffer, 41, 70, 0);

            Assert.That(Indices(buffer), Is.EqualTo(Range(4, 35)));
        }

        /// <summary>
        /// What: a mark after 20 frames keeps frames 5-20 and 21-36 and anchors on the mark.
        /// </summary>
        [Test]
        public void Frames_WhenMarkedAfterTwentyFrames_KeepsSixteenEachSide()
        {
            BurstFrameBuffer buffer = new BurstFrameBuffer();

            Feed(buffer, 1, 20, 0);
            buffer.Mark(2.0);
            Feed(buffer, 21, 50, 0);

            Assert.That(Indices(buffer), Is.EqualTo(Range(5, 36)));
            Assert.That(buffer.Anchor, Is.EqualTo(BurstAnchor.mark));
        }

        /// <summary>
        /// What: a larger change after the mark does not replace the marked burst.
        /// </summary>
        [Test]
        public void Frames_WhenSpikeAfterMark_KeepsMarkedBurst()
        {
            BurstFrameBuffer buffer = new BurstFrameBuffer();

            Feed(buffer, 1, 20, 0);
            buffer.Mark(2.0);
            Feed(buffer, 21, 25, 0);
            Feed(buffer, 26, 26, 50);
            Feed(buffer, 27, 60, 0);

            Assert.That(Indices(buffer), Is.EqualTo(Range(5, 36)));
            Assert.That(buffer.Anchor, Is.EqualTo(BurstAnchor.mark));
        }

        /// <summary>
        /// What: a mark after a largest-change burst replaces it, because the mark has priority.
        /// </summary>
        [Test]
        public void Frames_WhenMarkAfterSpike_ReplacesWithMarkedBurst()
        {
            BurstFrameBuffer buffer = new BurstFrameBuffer();

            Feed(buffer, 1, 9, 0);
            Feed(buffer, 10, 10, 30);
            Feed(buffer, 11, 40, 0);
            buffer.Mark(4.0);
            Feed(buffer, 41, 70, 0);

            Assert.That(Indices(buffer), Is.EqualTo(Range(25, 56)));
            Assert.That(buffer.Anchor, Is.EqualTo(BurstAnchor.mark));
        }

        /// <summary>
        /// What: a mark before any frame keeps only the sixteen frames after it.
        /// </summary>
        [Test]
        public void Frames_WhenMarkedBeforeAnyFrame_KeepsOnlyFramesAfter()
        {
            BurstFrameBuffer buffer = new BurstFrameBuffer();

            buffer.Mark(0.0);
            Feed(buffer, 1, 30, 0);

            Assert.That(Indices(buffer), Is.EqualTo(Range(1, 16)));
        }

        /// <summary>
        /// What: a mark followed by a single captured frame keeps just that frame.
        /// </summary>
        [Test]
        public void Frames_WhenMarkedThenOneFrame_KeepsOneFrame()
        {
            BurstFrameBuffer buffer = new BurstFrameBuffer();

            buffer.Mark(0.0);
            Feed(buffer, 1, 1, 0);

            Assert.That(buffer.Frames.Count, Is.EqualTo(1));
        }

        /// <summary>
        /// What: with two marks the later one anchors the burst and both mark times are kept.
        /// </summary>
        [Test]
        public void Frames_WhenMarkedTwice_LaterMarkWins()
        {
            BurstFrameBuffer buffer = new BurstFrameBuffer();

            Feed(buffer, 1, 20, 0);
            buffer.Mark(2.0);
            Feed(buffer, 21, 40, 0);
            buffer.Mark(4.0);
            Feed(buffer, 41, 70, 0);

            Assert.That(Indices(buffer), Is.EqualTo(Range(25, 56)));
            Assert.That(buffer.MarkSeconds, Is.EqualTo(new[] { 2.0, 4.0 }));
        }

        /// <summary>
        /// What: rewriting the caller's tile buffer after Add does not change the kept tile.
        /// </summary>
        [Test]
        public void Add_CopiesTileSoCallerCanReuseBuffer()
        {
            BurstFrameBuffer buffer = new BurstFrameBuffer();
            Color32[] tile = { new Color32(10, 0, 0, 255) };

            buffer.Add(tile, Sample(1, 10));
            tile[0] = new Color32(99, 0, 0, 255);

            Assert.That(buffer.Frames[0].Pixels[0].r, Is.EqualTo(10));
        }

        /// <summary>
        /// What: every kept tile still holds its own frame's pixels after many later frames reuse released arrays.
        /// </summary>
        [Test]
        public void Frames_AfterManyLaterFrames_KeepTheirOwnPixels()
        {
            BurstFrameBuffer buffer = new BurstFrameBuffer();

            FeedNumbered(buffer, 1, 19, 0);
            FeedNumbered(buffer, 20, 20, 10);
            FeedNumbered(buffer, 21, 100, 0);

            Assert.That(Indices(buffer), Is.EqualTo(Range(4, 35)));
            AssertTilesHoldOwnNumbers(buffer);
        }

        /// <summary>
        /// What: a marked burst keeps its own pixels while later frames reuse released arrays.
        /// </summary>
        [Test]
        public void Frames_AfterMarkAndManyLaterFrames_KeepTheirOwnPixels()
        {
            BurstFrameBuffer buffer = new BurstFrameBuffer();

            FeedNumbered(buffer, 1, 20, 0);
            buffer.Mark(2.0);
            FeedNumbered(buffer, 21, 100, 0);

            Assert.That(Indices(buffer), Is.EqualTo(Range(5, 36)));
            AssertTilesHoldOwnNumbers(buffer);
        }

        /// <summary>
        /// What: a burst rebuilt around a later, larger spike keeps its own pixels after the old burst's arrays are reused.
        /// </summary>
        [Test]
        public void Frames_WhenRebuiltAroundLaterSpike_KeepTheirOwnPixels()
        {
            BurstFrameBuffer buffer = new BurstFrameBuffer();

            FeedNumbered(buffer, 1, 19, 0);
            FeedNumbered(buffer, 20, 20, 5);
            FeedNumbered(buffer, 21, 59, 0);
            FeedNumbered(buffer, 60, 60, 10);
            FeedNumbered(buffer, 61, 150, 0);

            Assert.That(Indices(buffer), Is.EqualTo(Range(44, 75)));
            AssertTilesHoldOwnNumbers(buffer);
        }

        /// <summary>
        /// What: tile arrays dropped from the look-back window are reused, so a still recording keeps one spare array instead of allocating per frame.
        /// </summary>
        [Test]
        public void Add_WhenFramesLeaveTheWindow_ReusesTheirArrays()
        {
            BurstFrameBuffer buffer = new BurstFrameBuffer();

            FeedNumbered(buffer, 1, 50, 0);

            Assert.That(buffer.FreeArrayCount, Is.EqualTo(1));
        }

        private static void FeedNumbered(BurstFrameBuffer buffer, int from, int to, int changedCells)
        {
            for (int index = from; index <= to; index++)
            {
                buffer.Add(new[] { new Color32((byte)index, 0, 0, 255) }, Sample(index, changedCells));
            }
        }

        private static void AssertTilesHoldOwnNumbers(BurstFrameBuffer buffer)
        {
            foreach (CapturedTile frame in buffer.Frames)
            {
                Assert.That(frame.Pixels[0].r, Is.EqualTo((byte)frame.Sample.CapturedIndex),
                    "frame " + frame.Sample.CapturedIndex + " holds another frame's pixels");
            }
        }

        private static void Feed(BurstFrameBuffer buffer, int from, int to, int changedCells)
        {
            for (int index = from; index <= to; index++)
            {
                buffer.Add(new[] { new Color32(0, 0, 0, 255) }, Sample(index, changedCells));
            }
        }

        private static FrameChangeSample Sample(int index, int changedCells)
        {
            return new FrameChangeSample
            {
                CapturedIndex = index,
                EncodedFrame = index,
                Seconds = index * 0.1,
                ChangedCellsVsPrevious = changedCells
            };
        }

        private static List<int> Indices(BurstFrameBuffer buffer)
        {
            List<int> indices = new List<int>();
            foreach (CapturedTile frame in buffer.Frames)
            {
                indices.Add(frame.Sample.CapturedIndex);
            }

            return indices;
        }

        private static List<int> Range(int from, int to)
        {
            List<int> range = new List<int>();
            for (int index = from; index <= to; index++)
            {
                range.Add(index);
            }

            return range;
        }
    }
}
