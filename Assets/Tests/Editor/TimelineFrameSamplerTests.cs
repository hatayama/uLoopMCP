using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Verifies which frames the timeline sheet keeps across a whole recording of unknown length.
    /// </summary>
    public sealed class TimelineFrameSamplerTests
    {
        /// <summary>
        /// What: frames arriving every 0.1 s are kept every half second, starting with the first.
        /// </summary>
        [Test]
        public void Add_KeepsFramesHalfASecondApart()
        {
            TimelineFrameSampler sampler = new TimelineFrameSampler();

            for (int index = 0; index <= 20; index++)
            {
                Add(sampler, index, index / 10.0);
            }

            Assert.That(Seconds(sampler), Is.EqualTo(new[] { 0.0, 0.5, 1.0, 1.5, 2.0 }));
        }

        /// <summary>
        /// What: frames arriving every 0.3 s are skipped until half a second has passed since the last kept one.
        /// </summary>
        [Test]
        public void Add_WhenFramesAreSparse_KeepsNextFrameAfterTheInterval()
        {
            TimelineFrameSampler sampler = new TimelineFrameSampler();

            for (int index = 0; index <= 5; index++)
            {
                Add(sampler, index, index * 3 / 10.0);
            }

            Assert.That(Seconds(sampler), Is.EqualTo(new[] { 0.0, 0.6, 1.2 }));
        }

        /// <summary>
        /// What: the 41st kept frame halves the sheet to 21 tiles, keeping the first, and doubles the interval.
        /// </summary>
        [Test]
        public void Add_WhenTilesExceedTheLimit_ThinsToEveryOtherAndDoublesInterval()
        {
            TimelineFrameSampler sampler = new TimelineFrameSampler();

            for (int index = 0; index <= 40; index++)
            {
                Add(sampler, index, index * 0.5);
            }

            Assert.That(sampler.Frames.Count, Is.EqualTo(21));
            Assert.That(sampler.Frames[0].Sample.Seconds, Is.EqualTo(0.0));
            Assert.That(sampler.Frames[20].Sample.Seconds, Is.EqualTo(20.0));
            Assert.That(sampler.IntervalSeconds, Is.EqualTo(1.0));
        }

        /// <summary>
        /// What: after thinning, the next frame is kept one doubled interval after the last kept frame.
        /// </summary>
        [Test]
        public void Add_AfterThinning_KeepsFramesAtTheDoubledInterval()
        {
            TimelineFrameSampler sampler = new TimelineFrameSampler();

            for (int index = 0; index <= 44; index++)
            {
                Add(sampler, index, index * 0.5);
            }

            List<double> seconds = Seconds(sampler);
            Assert.That(seconds.GetRange(seconds.Count - 3, 3), Is.EqualTo(new[] { 20.0, 21.0, 22.0 }));
        }

        /// <summary>
        /// What: a long recording stays within the tile limit and every kept tile still holds its own frame's pixels after repeated thinning.
        /// </summary>
        [Test]
        public void Add_OverALongRecording_StaysWithinLimitAndKeepsOwnPixels()
        {
            TimelineFrameSampler sampler = new TimelineFrameSampler();

            for (int index = 0; index < 2000; index++)
            {
                Add(sampler, index, index / 10.0);
            }

            Assert.That(sampler.Frames.Count, Is.LessThanOrEqualTo(TimelineFrameSampler.MaxTiles));
            Assert.That(sampler.Frames[0].Sample.CapturedIndex, Is.EqualTo(0));
            foreach (CapturedTile frame in sampler.Frames)
            {
                Assert.That(frame.Pixels[0].r, Is.EqualTo((byte)frame.Sample.CapturedIndex),
                    "frame " + frame.Sample.CapturedIndex + " holds another frame's pixels");
            }
        }

        /// <summary>
        /// What: rewriting the caller's tile buffer after Add does not change the kept tile.
        /// </summary>
        [Test]
        public void Add_CopiesTileSoCallerCanReuseBuffer()
        {
            TimelineFrameSampler sampler = new TimelineFrameSampler();
            Color32[] tile = { new Color32(10, 0, 0, 255) };

            sampler.Add(tile, Sample(0, 0.0));
            tile[0] = new Color32(99, 0, 0, 255);

            Assert.That(sampler.Frames[0].Pixels[0].r, Is.EqualTo(10));
        }

        private static void Add(TimelineFrameSampler sampler, int index, double seconds)
        {
            sampler.Add(new[] { new Color32((byte)index, 0, 0, 255) }, Sample(index, seconds));
        }

        private static FrameChangeSample Sample(int index, double seconds)
        {
            return new FrameChangeSample { CapturedIndex = index, EncodedFrame = index, Seconds = seconds };
        }

        private static List<double> Seconds(TimelineFrameSampler sampler)
        {
            List<double> seconds = new List<double>();
            foreach (CapturedTile frame in sampler.Frames)
            {
                seconds.Add(frame.Sample.Seconds);
            }

            return seconds;
        }
    }
}
