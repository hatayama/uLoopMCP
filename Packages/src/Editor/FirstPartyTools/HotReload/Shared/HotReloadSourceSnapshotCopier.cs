using System;
using System.Collections.Generic;
using System.IO;

using io.github.hatayama.UnityCliLoop.ToolContracts;

using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Copies the sources of one compiled assembly byte-exact into its snapshot directory, writing
    /// into a temporary directory first and publishing it with one Move, and decides what the
    /// manifest records for each copy.
    /// </summary>
    internal static partial class HotReloadSourceSnapshotCopier
    {
        internal static void CaptureAtomically(
            string projectRoot,
            string assemblySnapshotDirectory,
            IReadOnlyList<string> sourceFiles,
            string assemblyName,
            HotReloadSnapshotSourceCheck check,
            HotReloadSnapshotCaptureStats stats)
        {
            Debug.Assert(stats != null, "stats must not be null.");

            // Why temp + Move: Directory.Exists is the "complete" signal. Copying into the final
            // directory first would leave a partial tree on interrupt that later reloads treat as
            // done and stamp-short-circuit forever. A sibling .tmp only becomes visible as complete
            // after Move succeeds.
            string temporaryDirectory = assemblySnapshotDirectory + HotReloadSourceSnapshotLayout.IncompleteDirectorySuffix;
            System.Diagnostics.Stopwatch prepareWatch = System.Diagnostics.Stopwatch.StartNew();
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }

            Directory.CreateDirectory(temporaryDirectory);
            stats.PublishTicks += prepareWatch.Elapsed.Ticks;
            SourceCopyTally tally = new SourceCopyTally();
            List<string> manifestLines = new List<string>(sourceFiles.Count);
            foreach (string projectRelativeSourcePath in sourceFiles)
            {
                SourceCopyOutcome outcome = CopySourceFileByteExact(
                    projectRoot,
                    temporaryDirectory,
                    projectRelativeSourcePath,
                    check,
                    manifestLines,
                    stats);
                tally.Count(outcome, projectRelativeSourcePath);
            }

            System.Diagnostics.Stopwatch publishWatch = System.Diagnostics.Stopwatch.StartNew();
            HotReloadSourceStampManifest.Write(temporaryDirectory, manifestLines);
            Directory.Move(temporaryDirectory, assemblySnapshotDirectory);
            stats.PublishTicks += publishWatch.Elapsed.Ticks;
            tally.LogAndWarn(assemblyName);
        }

        private static SourceCopyOutcome CopySourceFileByteExact(
            string projectRoot,
            string assemblySnapshotDirectory,
            string projectRelativeSourcePath,
            HotReloadSnapshotSourceCheck check,
            List<string> manifestLines,
            HotReloadSnapshotCaptureStats stats)
        {
            string normalizedRelativePath = projectRelativeSourcePath.Replace('\\', '/');
            string absoluteSourcePath = Path.Combine(projectRoot, normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string fileSystemSourcePath = HotReloadFileSystemPath.GetFileSystemPath(absoluteSourcePath);
            System.Diagnostics.Stopwatch statWatch = System.Diagnostics.Stopwatch.StartNew();
            FileInfo sourceBefore = new FileInfo(fileSystemSourcePath);
            bool sourceExists = sourceBefore.Exists;
            long length = sourceExists ? sourceBefore.Length : 0;
            long lastWriteTimeUtcTicks = sourceExists ? sourceBefore.LastWriteTimeUtc.Ticks : 0;
            stats.StatTicks += statWatch.Elapsed.Ticks;
            if (!sourceExists)
            {
                return new SourceCopyOutcome(SourceCopyKind.NotFound, 0);
            }

            string snapshotFileName = HotReloadSourceSnapshotLayout.SourceFileName(normalizedRelativePath);
            if (!TryCopySource(
                    fileSystemSourcePath,
                    Path.Combine(assemblySnapshotDirectory, snapshotFileName),
                    length,
                    lastWriteTimeUtcTicks,
                    stats,
                    out byte[] bytes,
                    out bool stampHeldDuringRead))
            {
                return new SourceCopyOutcome(SourceCopyKind.Unreadable, 0);
            }

            // Why the PDB check stays outside TryCopySource's catch: that catch counts an IO failure as
            // an unreadable source, and a PDB read failure would make that warning name the wrong file.
            // The assembly-level catch in CaptureAssemblies handles it instead.
            System.Diagnostics.Stopwatch checkWatch = null;
            HotReloadSnapshotCopyVerdict verdict = JudgeCopy(
                stampHeldDuringRead,
                lastWriteTimeUtcTicks,
                check.SuspectWritesFromUtcTicks,
                () =>
                {
                    checkWatch = System.Diagnostics.Stopwatch.StartNew();
                    bool isCompiledSource = check.IsCompiledSource(projectRoot, normalizedRelativePath, bytes);
                    checkWatch.Stop();
                    return isCompiledSource;
                });
            if (verdict != HotReloadSnapshotCopyVerdict.NoStamp)
            {
                manifestLines.Add(HotReloadSourceStampManifest.FormatLine(
                    snapshotFileName,
                    length,
                    lastWriteTimeUtcTicks,
                    verdict == HotReloadSnapshotCopyVerdict.EditedAfterCompile));
            }

            if (checkWatch == null)
            {
                return new SourceCopyOutcome(SourceCopyKind.Trusted, 0);
            }

            stats.PdbCheckTicks += checkWatch.Elapsed.Ticks;
            stats.FilesChecked++;

            SourceCopyKind checkedKind = verdict == HotReloadSnapshotCopyVerdict.EditedAfterCompile
                ? SourceCopyKind.CheckedEditedAfterCompile
                : SourceCopyKind.Checked;
            return new SourceCopyOutcome(checkedKind, checkWatch.Elapsed.Ticks);
        }

        // Copies the source and reports whether its stamp held while it was read. Returns false when the
        // source could not be read or the copy could not be written.
        private static bool TryCopySource(
            string fileSystemSourcePath,
            string destinationPath,
            long length,
            long lastWriteTimeUtcTicks,
            HotReloadSnapshotCaptureStats stats,
            out byte[] bytes,
            out bool stampHeldDuringRead)
        {
            System.Diagnostics.Stopwatch readWriteWatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                bytes = File.ReadAllBytes(fileSystemSourcePath);
                File.WriteAllBytes(destinationPath, bytes);
                stats.ReadWriteTicks += readWriteWatch.Elapsed.Ticks;
                stats.FilesCopied++;
                stats.BytesCopied += bytes.Length;
                // Why the stamp is taken before the read and checked again after it: a write during the
                // read moves the stamp on, and a stamp recorded then could vouch for bytes the copy does
                // not hold. Such a source is checked against the PDB: it gets no line when it matches,
                // so readers compare it by bytes until the next capture, and a marked line when it does not.
                System.Diagnostics.Stopwatch statWatch = System.Diagnostics.Stopwatch.StartNew();
                FileInfo sourceAfter = new FileInfo(fileSystemSourcePath);
                stampHeldDuringRead = sourceAfter.Exists
                    && sourceAfter.Length == length
                    && sourceAfter.LastWriteTimeUtc.Ticks == lastWriteTimeUtcTicks;
                stats.StatTicks += statWatch.Elapsed.Ticks;
                return true;
            }
            catch (Exception ex) when (IsSkippableSourceReadException(ex))
            {
                // Approved deviation from the no-try-catch rule: source IO can fail independently
                // while an assembly is being snapshotted. Skipping preserves the fail-closed
                // invariant because the method-diff baseline rejects any missing snapshot by PDB
                // checksum at use time.
                stats.ReadWriteTicks += readWriteWatch.Elapsed.Ticks;
                bytes = null;
                stampHeldDuringRead = false;
                return false;
            }
        }

        /// <summary>
        /// Decides what the manifest records for a copied source. Only a source that may have been
        /// saved after the compiler read it is checked against the PDB: one written at or after the
        /// start of the suspect window, or one that changed while it was read.
        /// </summary>
        internal static HotReloadSnapshotCopyVerdict JudgeCopy(
            bool stampHeldDuringRead,
            long lastWriteTimeUtcTicks,
            long suspectWritesFromUtcTicks,
            Func<bool> copyIsCompiledSource)
        {
            // Why a source that changed while read is checked whatever its write time: the stat before
            // the read can be old while the copy holds bytes written during the read.
            bool suspect = !stampHeldDuringRead || lastWriteTimeUtcTicks >= suspectWritesFromUtcTicks;
            if (!suspect)
            {
                // Written before the compile started and unchanged while read: what the compiler read.
                return HotReloadSnapshotCopyVerdict.StampTrusted;
            }

            if (copyIsCompiledSource())
            {
                // Why a matching source that changed while read still gets no line: its stamp does not
                // vouch for the copy's bytes, so readers compare by bytes as before.
                return stampHeldDuringRead
                    ? HotReloadSnapshotCopyVerdict.StampTrusted
                    : HotReloadSnapshotCopyVerdict.NoStamp;
            }

            return HotReloadSnapshotCopyVerdict.EditedAfterCompile;
        }

        private static bool IsSkippableSourceReadException(Exception ex)
        {
            Debug.Assert(ex != null, "ex must not be null");

            return ex is IOException ||
                   ex is UnauthorizedAccessException;
        }

        private enum SourceCopyKind
        {
            NotFound,
            Unreadable,
            // Copied and recorded without a PDB check.
            Trusted,
            // Checked against the PDB and confirmed.
            Checked,
            // Checked against the PDB and marked as edited after the compile.
            CheckedEditedAfterCompile
        }

        private readonly struct SourceCopyOutcome
        {
            internal readonly SourceCopyKind Kind;
            internal readonly long CheckTicks;

            internal SourceCopyOutcome(SourceCopyKind kind, long checkTicks)
            {
                Kind = kind;
                CheckTicks = checkTicks;
            }
        }

        // Counts the outcomes of one assembly's copies, then logs the PDB checks and warns about the
        // sources that could not be read.
        private sealed class SourceCopyTally
        {
            private int _skippedSourceCount;
            private string _firstSkippedSourcePath;
            private int _suspectSourceCount;
            private int _editedAfterCompileCount;
            private long _checkTicks;

            internal void Count(SourceCopyOutcome outcome, string projectRelativeSourcePath)
            {
                if (outcome.Kind == SourceCopyKind.Unreadable)
                {
                    _skippedSourceCount++;
                    _firstSkippedSourcePath ??= projectRelativeSourcePath;
                    return;
                }

                if (outcome.Kind == SourceCopyKind.Checked || outcome.Kind == SourceCopyKind.CheckedEditedAfterCompile)
                {
                    _suspectSourceCount++;
                    _checkTicks += outcome.CheckTicks;
                }

                if (outcome.Kind == SourceCopyKind.CheckedEditedAfterCompile)
                {
                    _editedAfterCompileCount++;
                }
            }

            internal void LogAndWarn(string assemblyName)
            {
                if (_suspectSourceCount > 0)
                {
                    VibeLogger.LogInfo(
                        HotReloadConstants.VibeLogSourceSnapshotChecked,
                        "Hot reload checked the sources written since the compile started against the PDB.",
                        new
                        {
                            assemblyName,
                            suspect = _suspectSourceCount,
                            editedAfterCompile = _editedAfterCompileCount,
                            checkMs = (long)TimeSpan.FromTicks(_checkTicks).TotalMilliseconds
                        });
                }

                if (_skippedSourceCount > 0)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[UnityCliLoop] Skipped {_skippedSourceCount} unreadable source(s) while snapshotting " +
                        $"{assemblyName}: {_firstSkippedSourcePath}");
                }
            }
        }
    }
}
