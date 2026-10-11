using System;
using System.Collections.Generic;

using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Tells, during one assembly's snapshot capture, which copied sources may not be the ones the
    /// compiler read, and checks such a copy against the checksum the assembly's PDB recorded.
    /// </summary>
    internal sealed class HotReloadSnapshotSourceCheck
    {
        private readonly string _dllPath;
        private readonly string _pdbPath;
        private readonly string _moduleVersionId;
        private readonly HotReloadPdbDocumentIndex _documentIndex;
        private readonly IReadOnlyList<HotReloadSnapshotPackageRoot> _packageRoots;
        private readonly StringComparison _pathComparison;

        internal HotReloadSnapshotSourceCheck(
            long suspectWritesFromUtcTicks,
            string dllPath,
            string pdbPath,
            string moduleVersionId,
            HotReloadPdbDocumentIndex documentIndex,
            IReadOnlyList<HotReloadSnapshotPackageRoot> packageRoots,
            StringComparison pathComparison)
        {
            Debug.Assert(!string.IsNullOrEmpty(dllPath), "dllPath must not be null or empty.");
            Debug.Assert(!string.IsNullOrEmpty(pdbPath), "pdbPath must not be null or empty.");
            Debug.Assert(!string.IsNullOrEmpty(moduleVersionId), "moduleVersionId must not be null or empty.");
            Debug.Assert(documentIndex != null, "documentIndex must not be null.");
            Debug.Assert(packageRoots != null, "packageRoots must not be null.");

            SuspectWritesFromUtcTicks = suspectWritesFromUtcTicks;
            _dllPath = dllPath;
            _pdbPath = pdbPath;
            _moduleVersionId = moduleVersionId;
            _documentIndex = documentIndex;
            _packageRoots = packageRoots;
            _pathComparison = pathComparison;
        }

        /// <summary>
        /// Sources last written at or after this time (UTC ticks) may have been saved after the
        /// compiler read them.
        /// </summary>
        internal long SuspectWritesFromUtcTicks { get; }

        /// <summary>
        /// Returns whether the bytes match the checksum the compiled assembly's PDB recorded for the
        /// source. A source the PDB has no usable checksum for is not confirmed. Asks nothing of the
        /// Package Manager: the package roots are the ones the caller read on the main thread.
        /// </summary>
        internal bool IsCompiledSource(string projectRoot, string slashNormalizedRelativePath, byte[] sourceBytes)
        {
            // Why the root that compiled the assembly: a Virtual Player reads the main project's
            // assemblies, whose PDB names sources relative to the main project (see
            // HotReloadSourceBaseline.CompareWithCompiledDocument).
            string compiledProjectRoot = CompiledAssemblyLayout.Resolve(projectRoot).MainProjectRoot;
            string pdbLookupPath = HotReloadSnapshotLookupPath.ToPdbLookupPath(
                compiledProjectRoot,
                slashNormalizedRelativePath,
                _packageRoots,
                _pathComparison);
            // Why NoDocumentInPdb counts as not compiled: a file without method bodies has no document,
            // and a copy that cannot be confirmed is safer treated as edited than trusted.
            return HotReloadSourceBaseline.CompareWithCompiledDocumentAtLookupPath(
                    pdbLookupPath,
                    _dllPath,
                    _pdbPath,
                    _moduleVersionId,
                    sourceBytes,
                    _documentIndex)
                == HotReloadSnapshotMissReason.None;
        }
    }

    /// <summary>
    /// What the capture records for one copied source in the stamp manifest.
    /// </summary>
    internal enum HotReloadSnapshotCopyVerdict
    {
        // A stamp line that vouches for the copy.
        StampTrusted,
        // No line: readers compare the copy by bytes.
        NoStamp,
        // A line marked as edited after the compile: the copy is not the compiled source.
        EditedAfterCompile
    }
}
