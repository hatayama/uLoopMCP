using System;
using System.IO;
using NUnit.Framework;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Verifies that sidecar files follow their videos when old videos are deleted.
    /// </summary>
    public sealed class RecordVideoSidecarRetentionTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "uloop-sidecar-retention-" + Guid.NewGuid().ToString("N"));
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
        /// What: sidecars of a video that is still there are kept, for both .mp4 and .webm.
        /// </summary>
        [Test]
        public void DeleteOrphans_KeepsSidecarsOfExistingVideos()
        {
            Touch("a.mp4", "a_frames.json", "a_burst.png", "a_timeline.png", "b.webm", "b_frames.json");

            RecordVideoSidecarRetention.DeleteOrphans(_directory);

            Assert.That(Exists("a_frames.json"), Is.True);
            Assert.That(Exists("a_burst.png"), Is.True);
            Assert.That(Exists("a_timeline.png"), Is.True);
            Assert.That(Exists("b_frames.json"), Is.True);
        }

        /// <summary>
        /// What: sidecars whose video was deleted are removed, including the cut-out frames and sheets.
        /// </summary>
        [Test]
        public void DeleteOrphans_DeletesSidecarsWithoutVideo()
        {
            Touch("old_frames.json", "old_burst.png", "old_timeline.png", "old_frame_1230.png", "old_sheet_1200_1800.png");

            RecordVideoSidecarRetention.DeleteOrphans(_directory);

            Assert.That(Directory.GetFiles(_directory), Is.Empty);
        }

        /// <summary>
        /// What: PNG and JSON files that do not follow the sidecar naming are left alone.
        /// </summary>
        [Test]
        public void DeleteOrphans_LeavesUnrelatedFiles()
        {
            Touch("screenshot.png", "notes.json", "clip_frame_.png");

            RecordVideoSidecarRetention.DeleteOrphans(_directory);

            Assert.That(Directory.GetFiles(_directory).Length, Is.EqualTo(3));
        }

        /// <summary>
        /// What: the stem is taken by removing exactly the sidecar suffix, including numbered ones.
        /// </summary>
        [TestCase("gameview_1_frames.json", "gameview_1")]
        [TestCase("gameview_1_burst.png", "gameview_1")]
        [TestCase("gameview_1_timeline.png", "gameview_1")]
        [TestCase("gameview_1_frame_1230.png", "gameview_1")]
        [TestCase("gameview_1_sheet_1200_1800.png", "gameview_1")]
        [TestCase("gameview_1.png", null)]
        public void TryGetStem_RemovesSidecarSuffix(string fileName, string expected)
        {
            Assert.That(RecordVideoSidecarRetention.TryGetStem(fileName), Is.EqualTo(expected));
        }

        private void Touch(params string[] names)
        {
            foreach (string name in names)
            {
                File.WriteAllText(Path.Combine(_directory, name), "");
            }
        }

        private bool Exists(string name)
        {
            return File.Exists(Path.Combine(_directory, name));
        }
    }
}
