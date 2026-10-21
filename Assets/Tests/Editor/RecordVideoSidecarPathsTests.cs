using System.IO;
using NUnit.Framework;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Verifies the names of the files written beside a recording.
    /// </summary>
    public sealed class RecordVideoSidecarPathsTests
    {
        /// <summary>
        /// What: the sidecars of an .mp4 share its directory and stem and drop the video extension.
        /// </summary>
        [Test]
        public void Paths_ForMp4_ShareDirectoryAndStem()
        {
            string video = Path.Combine("videos", "gameview_1.mp4");

            Assert.That(RecordVideoSidecarPaths.Analysis(video), Is.EqualTo(Path.Combine("videos", "gameview_1_frames.json")));
            Assert.That(RecordVideoSidecarPaths.Burst(video), Is.EqualTo(Path.Combine("videos", "gameview_1_burst.png")));
            Assert.That(RecordVideoSidecarPaths.Timeline(video), Is.EqualTo(Path.Combine("videos", "gameview_1_timeline.png")));
        }

        /// <summary>
        /// What: a .webm video gets the same sidecar names as an .mp4 with the same stem.
        /// </summary>
        [Test]
        public void Paths_ForWebm_UseSameStemRule()
        {
            string video = Path.Combine("videos", "clip.v2.webm");

            Assert.That(RecordVideoSidecarPaths.Analysis(video), Is.EqualTo(Path.Combine("videos", "clip.v2_frames.json")));
        }
    }
}
