using System.Collections.Generic;

using UnityEditor.PackageManager;

using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// One registered package as the snapshot capture sees it: the folder its files live in, the
    /// virtual path Unity names them by, and whether hot reload treats its sources as read-only.
    /// </summary>
    internal readonly struct HotReloadSnapshotPackageRoot
    {
        public readonly string ResolvedPath;
        public readonly string AssetPath;
        public readonly bool IsImmutable;

        public HotReloadSnapshotPackageRoot(string resolvedPath, string assetPath, bool isImmutable)
        {
            ResolvedPath = resolvedPath;
            AssetPath = assetPath;
            IsImmutable = isImmutable;
        }
    }

    /// <summary>
    /// Asks the Package Manager for the registered packages and whether each is immutable for hot
    /// reload. Why not an extension of ScriptPackageRoots.ReadCurrent: that type is shared with
    /// pause points, and adding the immutability to it would change their code too.
    /// </summary>
    internal static class HotReloadSnapshotPackageRoots
    {
        /// <summary>
        /// Lists every registered package that has both a folder on disk and a virtual path. Reads
        /// Package Manager state, so it must be called on the Unity main thread.
        /// </summary>
        internal static IReadOnlyList<HotReloadSnapshotPackageRoot> ReadCurrent()
        {
            PackageManagerPackageInfo[] packages = PackageManagerPackageInfo.GetAllRegisteredPackages();
            List<HotReloadSnapshotPackageRoot> roots = new List<HotReloadSnapshotPackageRoot>(packages.Length);
            foreach (PackageManagerPackageInfo package in packages)
            {
                if (string.IsNullOrEmpty(package.resolvedPath) || string.IsNullOrEmpty(package.assetPath))
                {
                    continue;
                }

                // The same rule as HotReloadSourceSnapshotter.ShouldSkipImmutablePackageSources:
                // only project-owned packages stay editable through hot reload.
                bool isImmutable = !(package.source == PackageSource.Embedded || package.source == PackageSource.Local);
                roots.Add(new HotReloadSnapshotPackageRoot(package.resolvedPath, package.assetPath, isImmutable));
            }

            return roots;
        }
    }
}
