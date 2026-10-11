using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    // Builds an assembly's new snapshot from the previous build's: it moves the previous directory to
    // the new temporary name, keeps the copies whose sources have not changed since, and writes only
    // the rest again.
    internal static partial class HotReloadSourceSnapshotCopier
    {
        internal static void CaptureIncrementally(
            string projectRoot,
            string assemblySnapshotDirectory,
            string previousSnapshotDirectory,
            string[] sourceFiles,
            string assemblyName,
            HotReloadSnapshotSourceCheck check,
            HotReloadSnapshotCaptureStats stats)
        {
            Debug.Assert(stats != null, "stats must not be null.");

            string temporaryDirectory = assemblySnapshotDirectory + HotReloadSourceSnapshotLayout.IncompleteDirectorySuffix;
            System.Diagnostics.Stopwatch adoptWatch = System.Diagnostics.Stopwatch.StartNew();
            // Why the leftover goes first: moving the previous directory onto an existing one throws.
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }

            stats.PublishTicks += adoptWatch.Elapsed.Ticks;
            System.Diagnostics.Stopwatch manifestWatch = System.Diagnostics.Stopwatch.StartNew();
            // Read before the move: the manifest moves with the directory and is rewritten there.
            HotReloadSourceStampManifest previousManifest = HotReloadSourceStampManifest.Load(previousSnapshotDirectory);
            stats.StampTicks += manifestWatch.Elapsed.Ticks;
            adoptWatch.Restart();
            bool adopted = TryAdoptPreviousSnapshot(previousSnapshotDirectory, temporaryDirectory);
            stats.PublishTicks += adoptWatch.Elapsed.Ticks;
            if (!adopted)
            {
                CaptureAtomically(projectRoot, assemblySnapshotDirectory, sourceFiles, assemblyName, check, stats);
                return;
            }

            HashSet<string> listedNames = new HashSet<string>(StringComparer.Ordinal);
            SourceCopyTally tally = new SourceCopyTally();
            List<string> manifestLines = new List<string>(sourceFiles.Length);
            foreach (string projectRelativeSourcePath in sourceFiles)
            {
                string normalizedRelativePath = projectRelativeSourcePath.Replace('\\', '/');
                string snapshotFileName = HotReloadSourceSnapshotLayout.SourceFileName(normalizedRelativePath);
                listedNames.Add(snapshotFileName);
                string copyPath = Path.Combine(temporaryDirectory, snapshotFileName);
                if (TryReusePreviousCopy(
                        projectRoot,
                        normalizedRelativePath,
                        snapshotFileName,
                        copyPath,
                        previousManifest,
                        check.SuspectWritesFromUtcTicks,
                        manifestLines,
                        stats))
                {
                    stats.FilesReused++;
                    continue;
                }

                // Why the previous copy goes before the copy: a source that is missing or unreadable now
                // gets no copy, and a stale copy left in its place would make readers that compare bytes
                // see a change that is not there.
                System.Diagnostics.Stopwatch deleteWatch = System.Diagnostics.Stopwatch.StartNew();
                File.Delete(copyPath);
                stats.ReadWriteTicks += deleteWatch.Elapsed.Ticks;
                SourceCopyOutcome outcome = CopySourceFileByteExact(
                    projectRoot,
                    temporaryDirectory,
                    projectRelativeSourcePath,
                    check,
                    manifestLines,
                    stats);
                tally.Count(outcome, projectRelativeSourcePath);
            }

            System.Diagnostics.Stopwatch cleanupWatch = System.Diagnostics.Stopwatch.StartNew();
            DeleteUnlistedCopies(temporaryDirectory, listedNames);
            stats.CleanupTicks += cleanupWatch.Elapsed.Ticks;
            System.Diagnostics.Stopwatch publishWatch = System.Diagnostics.Stopwatch.StartNew();
            HotReloadSourceStampManifest.Write(temporaryDirectory, manifestLines);
            Directory.Move(temporaryDirectory, assemblySnapshotDirectory);
            stats.PublishTicks += publishWatch.Elapsed.Ticks;
            tally.LogAndWarn(assemblyName);
        }

        // Keeps the previous copy and its line when the source has not been written since the previous
        // capture recorded it. Returns false when the source must be copied again.
        private static bool TryReusePreviousCopy(
            string projectRoot,
            string normalizedRelativePath,
            string snapshotFileName,
            string copyPath,
            HotReloadSourceStampManifest previousManifest,
            long suspectWritesFromUtcTicks,
            List<string> manifestLines,
            HotReloadSnapshotCaptureStats stats)
        {
            // Why an unmarked line with the same stat lets the copy stand: the previous capture wrote
            // that line only when the source's stat held while it read it and the bytes were the ones
            // the previous compile read, or matched the previous PDB. A source with the same length and
            // write time has not been written since, so this compile, which started after that capture,
            // read the same bytes.
            if (!previousManifest.TryGetStamp(snapshotFileName, out long length, out long lastWriteTimeUtcTicks))
            {
                return false;
            }

            if (previousManifest.IsEditedAfterCompile(snapshotFileName))
            {
                return false;
            }

            System.Diagnostics.Stopwatch statWatch = System.Diagnostics.Stopwatch.StartNew();
            string absoluteSourcePath = Path.Combine(projectRoot, normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar));
            FileInfo source = new FileInfo(HotReloadFileSystemPath.GetFileSystemPath(absoluteSourcePath));
            bool unchanged = source.Exists
                && source.Length == length
                && source.LastWriteTimeUtc.Ticks == lastWriteTimeUtcTicks;
            bool copyExists = unchanged && File.Exists(copyPath);
            stats.StatTicks += statWatch.Elapsed.Ticks;
            if (!unchanged)
            {
                return false;
            }

            // A defense in depth: a source written inside this compile's suspect window is checked
            // against this build's PDB instead of being trusted.
            if (lastWriteTimeUtcTicks >= suspectWritesFromUtcTicks)
            {
                return false;
            }

            if (!copyExists)
            {
                return false;
            }

            manifestLines.Add(HotReloadSourceStampManifest.FormatLine(snapshotFileName, length, lastWriteTimeUtcTicks, false));
            return true;
        }

        private static bool TryAdoptPreviousSnapshot(string previousSnapshotDirectory, string temporaryDirectory)
        {
            try
            {
                Directory.Move(previousSnapshotDirectory, temporaryDirectory);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Approved deviation from the no-try-catch rule: on Windows another process holding a
                // previous copy open stops the move. The previous directory then still sits whole under
                // its own name, so the capture copies every source instead.
                return false;
            }
        }

        // Deletes the copies of sources the assembly no longer lists, so no reader finds a copy of a
        // source this build did not compile.
        private static void DeleteUnlistedCopies(string temporaryDirectory, HashSet<string> listedNames)
        {
            foreach (string path in Directory.GetFiles(temporaryDirectory))
            {
                string fileName = Path.GetFileName(path);
                // The manifest is written again right after this.
                if (string.Equals(fileName, HotReloadConstants.SourceStampManifestFileName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (listedNames.Contains(fileName))
                {
                    continue;
                }

                File.Delete(path);
            }
        }
    }
}
