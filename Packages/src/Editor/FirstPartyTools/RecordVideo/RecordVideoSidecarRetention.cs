using System.IO;
using System.Text.RegularExpressions;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Deletes sidecar files whose video is gone. Runs after the video retention, so the sidecars
    /// follow their videos instead of being counted on their own: counting them per pattern would
    /// delete new sheets first whenever older videos without sidecars fill the limit.
    /// </summary>
    internal static class RecordVideoSidecarRetention
    {
        private static readonly Regex SidecarName = new Regex(
            @"^(?<stem>.+?)(_frames\.json|_burst\.png|_timeline\.png|_frame_\d+\.png|_sheet_\d+_\d+\.png)$",
            RegexOptions.CultureInvariant);

        internal static void DeleteOrphans(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (string path in Directory.GetFiles(directory))
            {
                string stem = TryGetStem(Path.GetFileName(path));
                if (stem == null)
                {
                    continue;
                }

                if (File.Exists(Path.Combine(directory, stem + ".mp4"))
                    || File.Exists(Path.Combine(directory, stem + ".webm")))
                {
                    continue;
                }

                File.Delete(path);
            }
        }

        /// <summary>
        /// Returns the video stem a sidecar file name belongs to, or null when the name is not a sidecar.
        /// </summary>
        internal static string TryGetStem(string fileName)
        {
            Match match = SidecarName.Match(fileName);
            return match.Success ? match.Groups["stem"].Value : null;
        }
    }
}
