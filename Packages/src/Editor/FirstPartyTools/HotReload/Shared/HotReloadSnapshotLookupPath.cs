using System;
using System.Collections.Generic;

using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Names a source the way its assembly's PDB records it, from package roots read earlier on the
    /// main thread, so that the snapshot capture can check a copy against the PDB without asking the
    /// Package Manager.
    /// </summary>
    internal static class HotReloadSnapshotLookupPath
    {
        /// <summary>
        /// Returns the same path as ScriptPackageRoots.ToPhysicalPath: project-relative when the file
        /// is inside <paramref name="compiledProjectRoot"/>, absolute when its package lives outside
        /// it, and the input itself when no package owns the path, as for Assets. Does no IO.
        /// </summary>
        internal static string ToPdbLookupPath(
            string compiledProjectRoot,
            string slashNormalizedAssetPath,
            IReadOnlyList<HotReloadSnapshotPackageRoot> packageRoots,
            StringComparison comparison)
        {
            Debug.Assert(!string.IsNullOrEmpty(compiledProjectRoot), "compiledProjectRoot must not be null or empty.");
            Debug.Assert(!string.IsNullOrEmpty(slashNormalizedAssetPath), "slashNormalizedAssetPath must not be null or empty.");
            Debug.Assert(packageRoots != null, "packageRoots must not be null.");

            List<ScriptPackageRoot> roots = new List<ScriptPackageRoot>(packageRoots.Count);
            foreach (HotReloadSnapshotPackageRoot packageRoot in packageRoots)
            {
                roots.Add(new ScriptPackageRoot(packageRoot.ResolvedPath, packageRoot.AssetPath));
            }

            return ScriptPathNormalizer.ToPhysicalProjectRelative(
                slashNormalizedAssetPath,
                compiledProjectRoot,
                roots,
                comparison);
        }
    }
}
