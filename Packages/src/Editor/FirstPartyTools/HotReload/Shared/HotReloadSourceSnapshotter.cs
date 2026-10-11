using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using io.github.hatayama.UnityCliLoop.ToolContracts;

using UnityEditor.PackageManager;

using UnityEngine;

using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// The store of byte-exact source snapshots: after a domain reload it copies the sources of each
    /// compiled project assembly, so that edited-method detection has the sources the compiler read
    /// to compare against. Readers find a snapshot through <see cref="HotReloadSourceSnapshotLayout"/>.
    /// </summary>
    internal static partial class HotReloadSourceSnapshotter
    {
        /// <summary>
        /// Captures snapshots for the project's compiled assemblies that have adjacent portable PDBs,
        /// listed from the dlls on disk and each read from its PDB.
        /// A copy of a source written since the compile started is checked against the PDB checksum
        /// when it is captured, and marked in the manifest when it does not match, so the default file
        /// selection, the sibling scan and the skip check treat that file as changed. The method-diff
        /// baseline checks every copy against the PDB when it uses one.
        /// Logs how long each phase took, even when nothing is listed.
        /// Returns false when no compiled dll with a PDB was listed.
        /// Asks Unity nothing beyond what <paramref name="inputs"/> carries, so it may run on a pool
        /// thread. Throws <see cref="OperationCanceledException"/> once
        /// <paramref name="cancellationToken"/> is cancelled.
        /// </summary>
        internal static bool CaptureAfterDomainReload(
            HotReloadSnapshotCaptureInputs inputs,
            CancellationToken cancellationToken)
        {
            Debug.Assert(inputs != null, "inputs must not be null.");

            cancellationToken.ThrowIfCancellationRequested();

            System.Diagnostics.Stopwatch totalWatch = System.Diagnostics.Stopwatch.StartNew();
            HotReloadSnapshotCaptureStats stats = CaptureAssemblies(
                inputs,
                HotReloadPdbDocumentIndex.Shared,
                HotReloadCaptureAssemblyEnumerator.ReadSources,
                CancellationToken.None);
            totalWatch.Stop();
            VibeLogger.LogInfo(
                HotReloadConstants.VibeLogSourceSnapshotBreakdown,
                "Hot reload source snapshot capture breakdown.",
                stats.ToVibeLogContext(totalWatch.ElapsedMilliseconds));
            return stats.Assemblies > 0;
        }

        // Why separate from CaptureAfterDomainReload: taking the document index and the PDB source
        // reader as arguments lets tests drive the per-assembly loop in a temporary root, without
        // writing the shared index, and see whether a PDB was read.
        // Returns how long each phase took and how much the capture did.
        internal static HotReloadSnapshotCaptureStats CaptureAssemblies(
            HotReloadSnapshotCaptureInputs inputs,
            HotReloadPdbDocumentIndex documentIndex,
            Func<HotReloadCaptureAssemblyCandidate, HotReloadSnapshotCaptureInputs, HotReloadCaptureAssembly> readSources,
            CancellationToken cancellationToken)
        {
            Debug.Assert(inputs != null, "inputs must not be null.");
            Debug.Assert(documentIndex != null, "documentIndex must not be null.");
            Debug.Assert(readSources != null, "readSources must not be null.");

            string snapshotRoot = HotReloadSourceSnapshotLayout.Root(inputs.ProjectRoot);
            Directory.CreateDirectory(snapshotRoot);

            HotReloadSnapshotCaptureStats stats = new HotReloadSnapshotCaptureStats();
            System.Diagnostics.Stopwatch listingWatch = System.Diagnostics.Stopwatch.StartNew();
            IReadOnlyList<HotReloadCaptureAssemblyCandidate> candidates =
                HotReloadCaptureAssemblyEnumerator.ListCompiled(inputs.ProjectRoot);
            stats.ListingTicks += listingWatch.Elapsed.Ticks;
            foreach (HotReloadCaptureAssemblyCandidate candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                stats.Assemblies++;
                try
                {
                    CaptureCandidateIfNeeded(inputs, snapshotRoot, candidate, documentIndex, readSources, stats);
                }
                catch (Exception ex) when (IsSkippableAssemblyCaptureException(ex))
                {
                    // Approved deviation from the no-try-catch rule: one assembly's transient IO
                    // or Cecil read failure must not deprive every remaining assembly of a baseline.
                    // Continuing preserves the fail-closed invariant because the method-diff baseline
                    // rejects missing snapshots by PDB checksum rather than using them for an incorrect diff.
                    UnityEngine.Debug.LogWarning(
                        $"[UnityCliLoop] Snapshot capture failed for assembly {candidate.Name}: {ex.Message}");
                }
            }

            return stats;
        }

        private static bool IsSkippableAssemblyCaptureException(Exception ex)
        {
            Debug.Assert(ex != null, "ex must not be null");

            return ex is IOException ||
                   ex is UnauthorizedAccessException ||
                   ex is BadImageFormatException;
        }

        // Why the stamps are checked before the PDB is read: after an ordinary edit only one dll has
        // changed, and after files are added or removed a few dozen, so only those PDBs are read.
        private static void CaptureCandidateIfNeeded(
            HotReloadSnapshotCaptureInputs inputs,
            string snapshotRoot,
            HotReloadCaptureAssemblyCandidate candidate,
            HotReloadPdbDocumentIndex documentIndex,
            Func<HotReloadCaptureAssemblyCandidate, HotReloadSnapshotCaptureInputs, HotReloadCaptureAssembly> readSources,
            HotReloadSnapshotCaptureStats stats)
        {
            FileInfo dllInfo = new FileInfo(candidate.DllPath);
            HotReloadCompiledDllStamp dll = new HotReloadCompiledDllStamp(dllInfo.LastWriteTimeUtc.Ticks, dllInfo.Length);
            string markerPath = HotReloadSourceSnapshotLayout.ImmutableMarkerPath(inputs.ProjectRoot, candidate.Name);
            string stampPath = StampPath(snapshotRoot, candidate.Name);

            // Why stamp short-circuits Cecil: a false stamp (identical mtime+length with different
            // bytes) is vanishingly rare, and even then LoadVerifiedSnapshotSource rejects via PDB
            // checksum — so stamp lies degrade to fallback, never to a wrong method diff. The marker of
            // a read-only package's assembly works the same way: a dll rebuilt after the package was
            // made editable no longer matches it.
            System.Diagnostics.Stopwatch stampWatch = System.Diagnostics.Stopwatch.StartNew();
            bool markerMatches = HasMatchingStamp(markerPath, dll.MtimeTicks, dll.ByteLength);
            bool stampMatches = !markerMatches && HasMatchingStamp(stampPath, dll.MtimeTicks, dll.ByteLength);
            stats.StampTicks += stampWatch.Elapsed.Ticks;
            if (markerMatches)
            {
                stats.AssembliesImmutable++;
                return;
            }

            if (stampMatches)
            {
                stats.AssembliesUnchanged++;
                return;
            }

            HotReloadCaptureAssembly assembly = ReadSourcesOrNull(candidate, inputs, readSources, stats);
            if (assembly == null || assembly.SourceAssetPaths.Count == 0)
            {
                // No source maps to an asset path: nothing is written, as for an assembly Unity listed
                // without sources, so the next capture reads the PDB again.
                return;
            }

            System.Diagnostics.Stopwatch packageLookupWatch = System.Diagnostics.Stopwatch.StartNew();
            bool immutable = assembly.IsImmutablePackageAssembly;
            stats.PackageLookupTicks += packageLookupWatch.Elapsed.Ticks;
            if (immutable)
            {
                System.Diagnostics.Stopwatch writeMarkerWatch = System.Diagnostics.Stopwatch.StartNew();
                WriteStamp(markerPath, ImmutableMarkerTag, dll.MtimeTicks, dll.ByteLength);
                stats.StampTicks += writeMarkerWatch.Elapsed.Ticks;
                stats.AssembliesImmutable++;
                return;
            }

            CaptureChangedAssembly(inputs, snapshotRoot, candidate, assembly, dll, stampPath, documentIndex, stats);
        }

        // The assembly's sources from its PDB, or null when the PDB belongs to another build of the
        // dll. Why that build is skipped rather than failing the capture: it happens when the capture
        // reads while a compile replaces the dll and the PDB; no stamp is written, so the next domain
        // captures that assembly.
        private static HotReloadCaptureAssembly ReadSourcesOrNull(
            HotReloadCaptureAssemblyCandidate candidate,
            HotReloadSnapshotCaptureInputs inputs,
            Func<HotReloadCaptureAssemblyCandidate, HotReloadSnapshotCaptureInputs, HotReloadCaptureAssembly> readSources,
            HotReloadSnapshotCaptureStats stats)
        {
            System.Diagnostics.Stopwatch readWatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                return readSources(candidate, inputs);
            }
            catch (InvalidOperationException)
            {
                stats.AssembliesSkippedPdbMismatch++;
                return null;
            }
            finally
            {
                stats.PdbSourcesTicks += readWatch.Elapsed.Ticks;
            }
        }

        private static void CaptureChangedAssembly(
            HotReloadSnapshotCaptureInputs inputs,
            string snapshotRoot,
            HotReloadCaptureAssemblyCandidate candidate,
            HotReloadCaptureAssembly assembly,
            HotReloadCompiledDllStamp dll,
            string stampPath,
            HotReloadPdbDocumentIndex documentIndex,
            HotReloadSnapshotCaptureStats stats)
        {
            // Why projectRoot stays the snapshot owner: only the compiled assemblies move to the main
            // project for a Virtual Player; its snapshots stay under its own root.
            string projectRoot = inputs.ProjectRoot;
            System.Diagnostics.Stopwatch mvidWatch = System.Diagnostics.Stopwatch.StartNew();
            string mvid = HotReloadAssemblyMvid.Read(candidate.DllPath);
            stats.MvidTicks += mvidWatch.Elapsed.Ticks;
            string assemblySnapshotDirectory = HotReloadSourceSnapshotLayout.AssemblyDirectory(projectRoot, assembly.Name, mvid);
            if (!Directory.Exists(assemblySnapshotDirectory))
            {
                HotReloadSnapshotSourceCheck check = new HotReloadSnapshotSourceCheck(
                    inputs.CompileStart.SuspectWritesFrom(dll.MtimeTicks),
                    candidate.DllPath,
                    candidate.PdbPath,
                    mvid,
                    documentIndex,
                    inputs.PackageRoots,
                    inputs.PathComparison);
                System.Diagnostics.Stopwatch previousWatch = System.Diagnostics.Stopwatch.StartNew();
                string previousSnapshotDirectory = FindReusablePreviousSnapshotDirectory(
                    projectRoot,
                    stampPath,
                    assembly.Name,
                    assemblySnapshotDirectory);
                stats.StampTicks += previousWatch.Elapsed.Ticks;
                if (previousSnapshotDirectory == null)
                {
                    HotReloadSourceSnapshotCopier.CaptureAtomically(
                        projectRoot,
                        assemblySnapshotDirectory,
                        assembly.SourceAssetPaths,
                        assembly.Name,
                        check,
                        stats);
                }
                else
                {
                    HotReloadSourceSnapshotCopier.CaptureIncrementally(
                        projectRoot,
                        assemblySnapshotDirectory,
                        previousSnapshotDirectory,
                        assembly.SourceAssetPaths,
                        assembly.Name,
                        check,
                        stats);
                }

                stats.AssembliesCaptured++;
                System.Diagnostics.Stopwatch cleanupWatch = System.Diagnostics.Stopwatch.StartNew();
                DeleteStaleSnapshotDirectories(snapshotRoot, assembly.Name, assemblySnapshotDirectory);
                stats.CleanupTicks += cleanupWatch.Elapsed.Ticks;
            }

            // Write the stamp even when individual sources were skipped: retrying every domain
            // reload would only repeat IO and warning spam. The partial snapshot remains fail-closed
            // because the method-diff baseline requires a PDB checksum match, and a changed DLL
            // invalidates this stamp through its mtime or length before the next capture.
            System.Diagnostics.Stopwatch writeStampWatch = System.Diagnostics.Stopwatch.StartNew();
            WriteStamp(stampPath, mvid, dll.MtimeTicks, dll.ByteLength);
            stats.StampTicks += writeStampWatch.Elapsed.Ticks;
        }

        /// <summary>
        /// Returns whether an assembly's sources belong to an immutable package and must not be
        /// snapshotted. Uses Package Manager metadata so Windows (where GetFullPath does not
        /// resolve package junctions) and macOS behave the same.
        /// </summary>
        internal static bool ShouldSkipImmutablePackageSources(string[] sourceFiles)
        {
            Debug.Assert(sourceFiles != null, "sourceFiles must not be null.");
            Debug.Assert(sourceFiles.Length > 0, "sourceFiles must not be empty.");

            // asmdef boundaries keep one assembly inside one package scope, so the first file
            // is enough to classify the whole assembly.
            PackageManagerPackageInfo packageInfo = PackageManagerPackageInfo.FindForAssetPath(sourceFiles[0]);
            if (packageInfo == null)
            {
                // Assets/ (and other non-package) scripts are editable — capture them.
                return false;
            }

            if (packageInfo.source == PackageSource.Embedded || packageInfo.source == PackageSource.Local)
            {
                // Project-owned packages remain editable via hot reload — capture them.
                return false;
            }

            // Registry / BuiltIn / Git / LocalTarball are immutable for hot-reload purposes.
            return true;
        }
    }
}
