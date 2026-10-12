using System.Collections.Generic;

using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// One compiled assembly as the snapshot capture sees it: its name, the asset paths of its
    /// sources, and whether hot reload treats it as a read-only package's, which the capture skips.
    /// </summary>
    internal sealed class HotReloadCaptureAssembly
    {
        internal HotReloadCaptureAssembly(
            string name,
            IReadOnlyList<string> sourceAssetPaths,
            bool isImmutablePackageAssembly,
            bool hasEverySource,
            bool sourcesFromCompilerOutput)
        {
            Debug.Assert(!string.IsNullOrEmpty(name), "name must not be null or empty.");
            Debug.Assert(sourceAssetPaths != null, "sourceAssetPaths must not be null.");

            Name = name;
            SourceAssetPaths = sourceAssetPaths;
            IsImmutablePackageAssembly = isImmutablePackageAssembly;
            HasEverySource = hasEverySource;
            SourcesFromCompilerOutput = sourcesFromCompilerOutput;
        }

        internal string Name { get; }

        internal IReadOnlyList<string> SourceAssetPaths { get; }

        internal bool IsImmutablePackageAssembly { get; }

        /// <summary>
        /// True when the sources are every source the compiler was given, one each: the PDB they were
        /// read from has as many documents as the source count the compiler recorded. False when the
        /// PDB records no count.
        /// </summary>
        internal bool HasEverySource { get; }

        /// <summary>
        /// True when the sources were read from the compiler's own PDB under Library/Bee instead of
        /// the PDB beside the dll.
        /// </summary>
        internal bool SourcesFromCompilerOutput { get; }
    }
}
