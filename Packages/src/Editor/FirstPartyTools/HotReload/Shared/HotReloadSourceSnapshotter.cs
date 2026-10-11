using System;
using System.Collections.Generic;
using System.IO;

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
        /// Captures snapshots for project assemblies that have adjacent portable PDBs.
        /// A copy of a source written since the compile started is checked against the PDB checksum
        /// when it is captured, and marked in the manifest when it does not match, so the default file
        /// selection, the sibling scan and the skip check treat that file as changed. The method-diff
        /// baseline checks every copy against the PDB when it uses one.
        /// Logs how long each phase took, even when the list is empty.
        /// Returns false when the inputs list no compilation assembly (as while Unity compiles).
        /// Asks Unity nothing beyond what <paramref name="inputs"/> carries.
        /// </summary>
        internal static bool CaptureAfterDomainReload(HotReloadSnapshotCaptureInputs inputs)
        {
            Debug.Assert(inputs != null, "inputs must not be null.");

            System.Diagnostics.Stopwatch captureWatch = System.Diagnostics.Stopwatch.StartNew();
            HotReloadSnapshotCaptureStats stats = CaptureAssemblies(inputs, HotReloadPdbDocumentIndex.Shared);
            captureWatch.Stop();
            // Why the listing time is added: the assemblies are now listed while the inputs are read,
            // before this method runs, and totalMs has always included that listing.
            VibeLogger.LogInfo(
                HotReloadConstants.VibeLogSourceSnapshotBreakdown,
                "Hot reload source snapshot capture breakdown.",
                stats.ToVibeLogContext(
                    inputs.AssemblyListingMs,
                    inputs.AssemblyListingMs + captureWatch.ElapsedMilliseconds));
            return inputs.Assemblies.Count > 0;
        }

        // Why separate from CaptureAfterDomainReload: taking the document index as an argument lets
        // tests drive the per-assembly loop in a temporary root without writing the shared index.
        // Returns how long each phase took and how much the capture did.
        internal static HotReloadSnapshotCaptureStats CaptureAssemblies(
            HotReloadSnapshotCaptureInputs inputs,
            HotReloadPdbDocumentIndex documentIndex)
        {
            Debug.Assert(inputs != null, "inputs must not be null.");
            Debug.Assert(documentIndex != null, "documentIndex must not be null.");

            string snapshotRoot = HotReloadSourceSnapshotLayout.Root(inputs.ProjectRoot);
            Directory.CreateDirectory(snapshotRoot);

            HotReloadSnapshotCaptureStats stats = new HotReloadSnapshotCaptureStats();
            foreach (HotReloadCaptureAssembly assembly in inputs.Assemblies)
            {
                stats.Assemblies++;
                try
                {
                    CaptureAssemblyIfNeeded(inputs, snapshotRoot, assembly, documentIndex, stats);
                }
                catch (Exception ex) when (IsSkippableAssemblyCaptureException(ex))
                {
                    // Approved deviation from the no-try-catch rule: one assembly's transient IO
                    // or Cecil read failure must not deprive every remaining assembly of a baseline.
                    // Continuing preserves the fail-closed invariant because the method-diff baseline
                    // rejects missing snapshots by PDB checksum rather than using them for an incorrect diff.
                    UnityEngine.Debug.LogWarning(
                        $"[UnityCliLoop] Snapshot capture failed for assembly {assembly.Name}: {ex.Message}");
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

        private static void CaptureAssemblyIfNeeded(
            HotReloadSnapshotCaptureInputs inputs,
            string snapshotRoot,
            HotReloadCaptureAssembly assembly,
            HotReloadPdbDocumentIndex documentIndex,
            HotReloadSnapshotCaptureStats stats)
        {
            IReadOnlyList<string> sourceFiles = assembly.SourceAssetPaths;
            if (sourceFiles.Count == 0)
            {
                return;
            }

            string projectRoot = inputs.ProjectRoot;
            // Why only a read: the inputs classified the assembly from the package roots, so the
            // capture no longer asks the Package Manager here.
            System.Diagnostics.Stopwatch packageLookupWatch = System.Diagnostics.Stopwatch.StartNew();
            bool immutable = assembly.IsImmutablePackageAssembly;
            stats.PackageLookupTicks += packageLookupWatch.Elapsed.Ticks;
            if (immutable)
            {
                stats.AssembliesImmutable++;
                return;
            }

            // Why projectRoot stays the snapshot owner: only the compiled assemblies move to the main
            // project for a Virtual Player; its snapshots stay under its own root.
            CompiledAssemblyLayout layout = CompiledAssemblyLayout.Resolve(projectRoot);
            string dllPath = layout.DllPath(assembly.Name);
            string pdbPath = layout.PdbPath(assembly.Name);
            if (!File.Exists(dllPath) || !File.Exists(pdbPath))
            {
                return;
            }

            FileInfo dllInfo = new FileInfo(dllPath);
            long dllMtimeTicks = dllInfo.LastWriteTimeUtc.Ticks;
            long dllByteLength = dllInfo.Length;
            string stampPath = StampPath(snapshotRoot, assembly.Name);

            // Why stamp short-circuits Cecil: a false stamp (identical mtime+length with different
            // bytes) is vanishingly rare, and even then LoadVerifiedSnapshotSource rejects via PDB
            // checksum — so stamp lies degrade to fallback, never to a wrong method diff.
            System.Diagnostics.Stopwatch stampWatch = System.Diagnostics.Stopwatch.StartNew();
            bool stampMatches = HasMatchingStamp(stampPath, dllMtimeTicks, dllByteLength);
            stats.StampTicks += stampWatch.Elapsed.Ticks;
            if (stampMatches)
            {
                stats.AssembliesUnchanged++;
                return;
            }

            System.Diagnostics.Stopwatch mvidWatch = System.Diagnostics.Stopwatch.StartNew();
            string mvid = HotReloadAssemblyMvid.Read(dllPath);
            stats.MvidTicks += mvidWatch.Elapsed.Ticks;
            string assemblySnapshotDirectory = HotReloadSourceSnapshotLayout.AssemblyDirectory(projectRoot, assembly.Name, mvid);
            if (!Directory.Exists(assemblySnapshotDirectory))
            {
                HotReloadSnapshotSourceCheck check = new HotReloadSnapshotSourceCheck(
                    inputs.CompileStart.SuspectWritesFrom(dllMtimeTicks),
                    dllPath,
                    pdbPath,
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
                        sourceFiles,
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
                        sourceFiles,
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
            WriteStamp(stampPath, mvid, dllMtimeTicks, dllByteLength);
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
