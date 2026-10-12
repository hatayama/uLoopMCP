using System;
using System.IO;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    // The stamp beside an assembly's snapshot directories records the compiled DLL the snapshot was
    // taken from, so a reload whose DLL has not changed skips the capture.
    internal static partial class HotReloadSourceSnapshotter
    {
        private const string StampFileExtension = ".stamp";

        // Why a tag where a stamp has the MVID: the marker of a read-only package's assembly names
        // no snapshot directory, and reading the MVID would open a dll the capture otherwise skips.
        private const string ImmutableMarkerTag = "immutable";

        private static string StampPath(string snapshotRoot, string assemblyName)
        {
            return Path.Combine(snapshotRoot, assemblyName + StampFileExtension);
        }

        private static bool HasMatchingStamp(string stampPath, long dllMtimeTicks, long dllByteLength)
        {
            if (!File.Exists(stampPath))
            {
                return false;
            }

            string stampText = File.ReadAllText(stampPath).Trim();
            string[] parts = stampText.Split(',');
            if (parts.Length != 3)
            {
                return false;
            }

            if (string.IsNullOrEmpty(parts[0]))
            {
                return false;
            }

            if (!long.TryParse(parts[1], out long stampedMtimeTicks)
                || !long.TryParse(parts[2], out long stampedByteLength))
            {
                return false;
            }

            return stampedMtimeTicks == dllMtimeTicks && stampedByteLength == dllByteLength;
        }

        // The complete snapshot directory of the build the stamp names, when it has a manifest to reuse;
        // null when there is none, so the capture copies every source.
        private static string FindReusablePreviousSnapshotDirectory(
            string projectRoot,
            string stampPath,
            string assemblyName,
            string currentSnapshotDirectory)
        {
            if (!File.Exists(stampPath))
            {
                return null;
            }

            string[] parts = File.ReadAllText(stampPath).Trim().Split(',');
            if (parts.Length != 3)
            {
                return null;
            }

            // Why the same check as the stale-directory cleanup: only an MVID names a snapshot directory.
            if (!Guid.TryParseExact(parts[0], "N", out Guid _))
            {
                return null;
            }

            string previousSnapshotDirectory = HotReloadSourceSnapshotLayout.AssemblyDirectory(projectRoot, assemblyName, parts[0]);
            if (string.Equals(
                    Path.GetFullPath(previousSnapshotDirectory),
                    Path.GetFullPath(currentSnapshotDirectory),
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!Directory.Exists(previousSnapshotDirectory))
            {
                return null;
            }

            // A missing, malformed or older-format manifest answers no line, so nothing could be reused.
            if (HotReloadSourceStampManifest.Load(previousSnapshotDirectory).Count == 0)
            {
                return null;
            }

            return previousSnapshotDirectory;
        }

        private static void WriteStamp(string stampPath, string mvid, long dllMtimeTicks, long dllByteLength)
        {
            File.WriteAllText(stampPath, mvid + "," + dllMtimeTicks + "," + dllByteLength);
        }
    }

    /// <summary>
    /// The write time and length of a compiled dll, which the stamp and the marker beside its
    /// snapshots record.
    /// </summary>
    internal readonly struct HotReloadCompiledDllStamp
    {
        internal readonly long MtimeTicks;
        internal readonly long ByteLength;

        internal HotReloadCompiledDllStamp(long mtimeTicks, long byteLength)
        {
            MtimeTicks = mtimeTicks;
            ByteLength = byteLength;
        }
    }
}
