using System.IO;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Names the files written beside a recording, all sharing the video's directory and file stem.
    /// </summary>
    internal static class RecordVideoSidecarPaths
    {
        internal const string AnalysisSuffix = "_frames.json";
        internal const string BurstSuffix = "_burst.png";
        internal const string TimelineSuffix = "_timeline.png";

        internal static string Analysis(string videoPath)
        {
            return Beside(videoPath, AnalysisSuffix);
        }

        internal static string Burst(string videoPath)
        {
            return Beside(videoPath, BurstSuffix);
        }

        internal static string Timeline(string videoPath)
        {
            return Beside(videoPath, TimelineSuffix);
        }

        private static string Beside(string videoPath, string suffix)
        {
            string directory = Path.GetDirectoryName(videoPath) ?? string.Empty;
            return Path.Combine(directory, Path.GetFileNameWithoutExtension(videoPath) + suffix);
        }
    }
}
