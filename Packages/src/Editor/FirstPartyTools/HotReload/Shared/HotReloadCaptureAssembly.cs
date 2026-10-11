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
            bool isImmutablePackageAssembly)
        {
            Debug.Assert(!string.IsNullOrEmpty(name), "name must not be null or empty.");
            Debug.Assert(sourceAssetPaths != null, "sourceAssetPaths must not be null.");

            Name = name;
            SourceAssetPaths = sourceAssetPaths;
            IsImmutablePackageAssembly = isImmutablePackageAssembly;
        }

        internal string Name { get; }

        internal IReadOnlyList<string> SourceAssetPaths { get; }

        internal bool IsImmutablePackageAssembly { get; }
    }
}
