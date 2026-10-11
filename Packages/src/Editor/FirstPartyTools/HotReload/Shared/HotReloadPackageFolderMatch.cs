using System;
using System.Collections.Generic;

using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Tells which package folder a slash-separated path lies in, and whether hot reload treats that
    /// package's sources as read-only. Does no IO and asks nothing of the Package Manager, so it can
    /// run on any thread.
    /// </summary>
    internal static class HotReloadPackageFolderMatch
    {
        /// <summary>
        /// Returns the length of <paramref name="folder"/> when <paramref name="path"/> is inside it,
        /// so that the character after it is "/"; -1 otherwise, and -1 for a null folder. Why the
        /// "/": "/a/LocalPkg2/x.cs" must not count as inside "/a/LocalPkg".
        /// </summary>
        internal static int PrefixLength(string path, string folder, StringComparison comparison)
        {
            Debug.Assert(path != null, "path must not be null.");

            if (folder == null || path.Length <= folder.Length + 1)
            {
                return -1;
            }

            if (path[folder.Length] != '/' || !path.StartsWith(folder, comparison))
            {
                return -1;
            }

            return folder.Length;
        }

        /// <summary>
        /// Returns whether the asset path lies in a package hot reload treats as read-only: the
        /// <see cref="HotReloadSnapshotPackageRoot.IsImmutable"/> of the package whose asset path
        /// contains it, or false when no package does, as for Assets. The same rule the PDB source
        /// map applies to the first source of an assembly.
        /// </summary>
        internal static bool IsInImmutablePackage(
            string slashNormalizedAssetPath,
            IReadOnlyList<HotReloadSnapshotPackageRoot> packageRoots,
            StringComparison comparison)
        {
            Debug.Assert(!string.IsNullOrEmpty(slashNormalizedAssetPath), "slashNormalizedAssetPath must not be null or empty.");
            Debug.Assert(packageRoots != null, "packageRoots must not be null.");

            int bestLength = -1;
            bool bestIsImmutable = false;
            foreach (HotReloadSnapshotPackageRoot root in packageRoots)
            {
                int length = PrefixLength(slashNormalizedAssetPath, NormalizeFolder(root.AssetPath), comparison);
                if (length > bestLength)
                {
                    bestLength = length;
                    bestIsImmutable = root.IsImmutable;
                }
            }

            return bestIsImmutable;
        }

        /// <summary>
        /// Returns the folder with forward slashes and without a trailing slash, the form
        /// <see cref="PrefixLength"/> expects.
        /// </summary>
        internal static string NormalizeFolder(string path)
        {
            return path.Replace('\\', '/').TrimEnd('/');
        }
    }
}
