using System;
using System.Collections.Generic;
using System.IO;

using io.github.hatayama.UnityCliLoop.ToolContracts;

using UnityEditor.PackageManager;

using UnityEngine;

using UnityCompilationAssembly = UnityEditor.Compilation.Assembly;
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
        /// Returns false when Unity listed no compilation assembly (as while it compiles).
        /// </summary>
        internal static bool CaptureAfterDomainReload()
        {
            System.Diagnostics.Stopwatch totalWatch = System.Diagnostics.Stopwatch.StartNew();
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            System.Diagnostics.Stopwatch getAssembliesWatch = System.Diagnostics.Stopwatch.StartNew();
            IReadOnlyList<UnityCompilationAssembly> assemblies = HotReloadCompilationAssemblies.Current();
            getAssembliesWatch.Stop();
            HotReloadSnapshotCaptureStats stats = CaptureAssemblies(
                projectRoot,
                assemblies,
                HotReloadCompileStartRecord.Read(),
                HotReloadPdbDocumentIndex.Shared);
            totalWatch.Stop();
            VibeLogger.LogInfo(
                HotReloadConstants.VibeLogSourceSnapshotBreakdown,
                "Hot reload source snapshot capture breakdown.",
                stats.ToVibeLogContext(getAssembliesWatch.ElapsedMilliseconds, totalWatch.ElapsedMilliseconds));
            return assemblies.Count > 0;
        }

        // Why separate from CaptureAfterDomainReload: the project root and the compilation assemblies come
        // from Unity, so taking them as arguments lets tests drive the per-assembly loop in a temporary root.
        // Returns how long each phase took and how much the capture did.
        internal static HotReloadSnapshotCaptureStats CaptureAssemblies(
            string projectRoot,
            IEnumerable<UnityCompilationAssembly> assemblies,
            HotReloadCompileStart compileStart,
            HotReloadPdbDocumentIndex documentIndex)
        {
            Debug.Assert(!string.IsNullOrEmpty(projectRoot), "projectRoot must not be null or empty.");
            Debug.Assert(assemblies != null, "assemblies must not be null.");
            Debug.Assert(documentIndex != null, "documentIndex must not be null.");

            string snapshotRoot = HotReloadSourceSnapshotLayout.Root(projectRoot);
            Directory.CreateDirectory(snapshotRoot);

            HotReloadSnapshotCaptureStats stats = new HotReloadSnapshotCaptureStats();
            foreach (UnityCompilationAssembly assembly in assemblies)
            {
                stats.Assemblies++;
                try
                {
                    CaptureAssemblyIfNeeded(projectRoot, snapshotRoot, assembly, compileStart, documentIndex, stats);
                }
                catch (Exception ex) when (IsSkippableAssemblyCaptureException(ex))
                {
                    // Approved deviation from the no-try-catch rule: one assembly's transient IO
                    // or Cecil read failure must not deprive every remaining assembly of a baseline.
                    // Continuing preserves the fail-closed invariant because the method-diff baseline
                    // rejects missing snapshots by PDB checksum rather than using them for an incorrect diff.
                    UnityEngine.Debug.LogWarning(
                        $"[UnityCliLoop] Snapshot capture failed for assembly {assembly.name}: {ex.Message}");
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
            string projectRoot,
            string snapshotRoot,
            UnityCompilationAssembly assembly,
            HotReloadCompileStart compileStart,
            HotReloadPdbDocumentIndex documentIndex,
            HotReloadSnapshotCaptureStats stats)
        {
            string[] sourceFiles = assembly.sourceFiles;
            if (sourceFiles == null || sourceFiles.Length == 0)
            {
                return;
            }

            System.Diagnostics.Stopwatch packageLookupWatch = System.Diagnostics.Stopwatch.StartNew();
            bool immutable = ShouldSkipImmutablePackageSources(sourceFiles);
            stats.PackageLookupTicks += packageLookupWatch.Elapsed.Ticks;
            if (immutable)
            {
                stats.AssembliesImmutable++;
                return;
            }

            // Why projectRoot stays the snapshot owner: only the compiled assemblies move to the main
            // project for a Virtual Player; its snapshots stay under its own root.
            CompiledAssemblyLayout layout = CompiledAssemblyLayout.Resolve(projectRoot);
            string dllPath = layout.DllPath(assembly.name);
            string pdbPath = layout.PdbPath(assembly.name);
            if (!File.Exists(dllPath) || !File.Exists(pdbPath))
            {
                return;
            }

            FileInfo dllInfo = new FileInfo(dllPath);
            long dllMtimeTicks = dllInfo.LastWriteTimeUtc.Ticks;
            long dllByteLength = dllInfo.Length;
            string stampPath = StampPath(snapshotRoot, assembly.name);

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
            string assemblySnapshotDirectory = HotReloadSourceSnapshotLayout.AssemblyDirectory(projectRoot, assembly.name, mvid);
            if (!Directory.Exists(assemblySnapshotDirectory))
            {
                HotReloadSnapshotSourceCheck check = new HotReloadSnapshotSourceCheck(
                    compileStart.SuspectWritesFrom(dllMtimeTicks),
                    dllPath,
                    pdbPath,
                    mvid,
                    documentIndex);
                HotReloadSourceSnapshotCopier.CaptureAtomically(
                    projectRoot,
                    assemblySnapshotDirectory,
                    sourceFiles,
                    assembly.name,
                    check,
                    stats);
                stats.AssembliesCaptured++;
                System.Diagnostics.Stopwatch cleanupWatch = System.Diagnostics.Stopwatch.StartNew();
                DeleteStaleSnapshotDirectories(snapshotRoot, assembly.name, assemblySnapshotDirectory);
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
