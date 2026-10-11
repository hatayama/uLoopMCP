using System;
using System.Collections.Generic;

using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// The sources a compiled assembly was built from, named by the asset paths Unity uses for them,
    /// and whether the assembly belongs to a package hot reload treats as read-only.
    /// </summary>
    internal sealed class HotReloadCompiledSources
    {
        internal HotReloadCompiledSources(IReadOnlyList<string> assetPaths, bool isImmutablePackageAssembly)
        {
            Debug.Assert(assetPaths != null, "assetPaths must not be null.");
            AssetPaths = assetPaths;
            IsImmutablePackageAssembly = isImmutablePackageAssembly;
        }

        /// <summary>Each asset path once, in the order the PDB lists its document.</summary>
        internal IReadOnlyList<string> AssetPaths { get; }

        /// <summary>True when the first source that maps to an asset path is in an immutable package.</summary>
        internal bool IsImmutablePackageAssembly { get; }
    }

    /// <summary>
    /// Turns the document urls of a PDB into the asset paths Unity lists as the assembly's sources.
    /// A url takes one of three forms: "./" and a path relative to the compiling project, which an
    /// assembly compiled with a pathmap records; an absolute path, which an assembly without a
    /// pathmap records for every source and any assembly records for a package outside the project;
    /// or a relative name without "./", which is a source generator's output. Urls of files that are
    /// not C# sources, not under Assets and not under a package root are left out.
    /// </summary>
    internal static class HotReloadCompiledSourceMap
    {
        private const string CSharpExtension = ".cs";
        private const string ProjectRelativePrefix = "./";
        private const string AssetsPrefix = "Assets/";

        // A package root in every form a url can name its folder by. A relative form is null when
        // the folder is not under that root.
        private readonly struct NormalizedPackageRoot
        {
            public readonly string ResolvedPath;
            public readonly string RelativeToCompilingRoot;
            public readonly string RelativeToOwnRoot;
            public readonly string AssetPath;
            public readonly bool IsImmutable;

            public NormalizedPackageRoot(
                string resolvedPath,
                string relativeToCompilingRoot,
                string relativeToOwnRoot,
                string assetPath,
                bool isImmutable)
            {
                ResolvedPath = resolvedPath;
                RelativeToCompilingRoot = relativeToCompilingRoot;
                RelativeToOwnRoot = relativeToOwnRoot;
                AssetPath = assetPath;
                IsImmutable = isImmutable;
            }
        }

        private readonly struct MappedSource
        {
            public readonly string AssetPath;
            public readonly bool IsImmutable;

            public MappedSource(string assetPath, bool isImmutable)
            {
                AssetPath = assetPath;
                IsImmutable = isImmutable;
            }
        }

        /// <summary>
        /// Maps the documents of one assembly's PDB. <paramref name="compilingProjectRoot"/> is the
        /// project whose compile wrote the PDB, which is the main project for a Virtual Player, and
        /// <paramref name="projectRoot"/> is this Editor's own project. <paramref name="pathComparison"/>
        /// is Ordinal or OrdinalIgnoreCase. Does no IO.
        /// </summary>
        internal static HotReloadCompiledSources FromDocuments(
            IReadOnlyList<HotReloadPdbDocument> documents,
            string compilingProjectRoot,
            string projectRoot,
            IReadOnlyList<HotReloadSnapshotPackageRoot> packageRoots,
            StringComparison pathComparison)
        {
            Debug.Assert(documents != null, "documents must not be null.");
            Debug.Assert(!string.IsNullOrEmpty(compilingProjectRoot), "compilingProjectRoot must not be null or empty.");
            Debug.Assert(!string.IsNullOrEmpty(projectRoot), "projectRoot must not be null or empty.");
            Debug.Assert(packageRoots != null, "packageRoots must not be null.");
            Debug.Assert(
                pathComparison == StringComparison.Ordinal || pathComparison == StringComparison.OrdinalIgnoreCase,
                "pathComparison must be Ordinal or OrdinalIgnoreCase.");

            string compilingRoot = HotReloadPackageFolderMatch.NormalizeFolder(compilingProjectRoot);
            string ownRoot = HotReloadPackageFolderMatch.NormalizeFolder(projectRoot);
            List<NormalizedPackageRoot> packages = NormalizePackages(packageRoots, compilingRoot, ownRoot, pathComparison);
            List<string> assetPaths = new List<string>();
            HashSet<string> seen = new HashSet<string>(
                pathComparison == StringComparison.OrdinalIgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            bool? firstIsImmutable = null;
            foreach (HotReloadPdbDocument document in documents)
            {
                if (!TryMap(document.Url, compilingRoot, packages, pathComparison, out MappedSource mapped))
                {
                    continue;
                }

                if (!seen.Add(mapped.AssetPath))
                {
                    continue;
                }

                // Why the first mapped source: it keeps the rule of ShouldSkipImmutablePackageSources,
                // which classifies an assembly by its first source because an asmdef keeps one
                // assembly inside one package.
                if (firstIsImmutable == null)
                {
                    firstIsImmutable = mapped.IsImmutable;
                }

                assetPaths.Add(mapped.AssetPath);
            }

            return new HotReloadCompiledSources(assetPaths, firstIsImmutable ?? false);
        }

        private static bool TryMap(
            string documentUrl,
            string compilingRoot,
            List<NormalizedPackageRoot> packages,
            StringComparison comparison,
            out MappedSource mapped)
        {
            mapped = default(MappedSource);
            string url = documentUrl.Replace('\\', '/');
            if (!url.EndsWith(CSharpExtension, comparison))
            {
                return false;
            }

            if (url.StartsWith(ProjectRelativePrefix, StringComparison.Ordinal))
            {
                return TryMapProjectRelative(url.Substring(ProjectRelativePrefix.Length), packages, comparison, out mapped);
            }

            if (IsRooted(url))
            {
                return TryMapAbsolute(url, compilingRoot, packages, comparison, out mapped);
            }

            // A relative name without "./" is a generated source, which has no asset path.
            return false;
        }

        private static bool TryMapProjectRelative(
            string relativePath,
            List<NormalizedPackageRoot> packages,
            StringComparison comparison,
            out MappedSource mapped)
        {
            if (relativePath.StartsWith(AssetsPrefix, comparison))
            {
                mapped = new MappedSource(relativePath, false);
                return true;
            }

            // Why both relative forms: a Virtual Player's urls are relative to the main project,
            // while the Package Manager may resolve an embedded package under the player's root.
            int bestLength = -1;
            NormalizedPackageRoot best = default(NormalizedPackageRoot);
            foreach (NormalizedPackageRoot package in packages)
            {
                int length = Math.Max(
                    HotReloadPackageFolderMatch.PrefixLength(relativePath, package.RelativeToCompilingRoot, comparison),
                    HotReloadPackageFolderMatch.PrefixLength(relativePath, package.RelativeToOwnRoot, comparison));
                if (length > bestLength)
                {
                    bestLength = length;
                    best = package;
                }
            }

            return TryMapUnderPackage(relativePath, best, bestLength, out mapped);
        }

        private static bool TryMapAbsolute(
            string absolutePath,
            string compilingRoot,
            List<NormalizedPackageRoot> packages,
            StringComparison comparison,
            out MappedSource mapped)
        {
            int rootLength = HotReloadPackageFolderMatch.PrefixLength(absolutePath, compilingRoot, comparison);
            if (rootLength >= 0)
            {
                return TryMapProjectRelative(absolutePath.Substring(rootLength + 1), packages, comparison, out mapped);
            }

            int bestLength = -1;
            NormalizedPackageRoot best = default(NormalizedPackageRoot);
            foreach (NormalizedPackageRoot package in packages)
            {
                int length = HotReloadPackageFolderMatch.PrefixLength(absolutePath, package.ResolvedPath, comparison);
                if (length > bestLength)
                {
                    bestLength = length;
                    best = package;
                }
            }

            return TryMapUnderPackage(absolutePath, best, bestLength, out mapped);
        }

        // path minus its first prefixLength characters and the "/" after them, under the package's asset path.
        private static bool TryMapUnderPackage(
            string path,
            NormalizedPackageRoot package,
            int prefixLength,
            out MappedSource mapped)
        {
            if (prefixLength < 0)
            {
                mapped = default(MappedSource);
                return false;
            }

            mapped = new MappedSource(package.AssetPath + "/" + path.Substring(prefixLength + 1), package.IsImmutable);
            return true;
        }

        // Why not Path.IsPathRooted: on macOS and Linux it does not take "C:/proj/..." for rooted,
        // so a PDB written on Windows would lose its sources there.
        private static bool IsRooted(string url)
        {
            if (url.Length > 0 && url[0] == '/')
            {
                return true;
            }

            return url.Length >= 3 && IsAsciiLetter(url[0]) && url[1] == ':' && url[2] == '/';
        }

        private static bool IsAsciiLetter(char character)
        {
            return (character >= 'A' && character <= 'Z') || (character >= 'a' && character <= 'z');
        }

        private static List<NormalizedPackageRoot> NormalizePackages(
            IReadOnlyList<HotReloadSnapshotPackageRoot> packageRoots,
            string compilingRoot,
            string ownRoot,
            StringComparison comparison)
        {
            List<NormalizedPackageRoot> packages = new List<NormalizedPackageRoot>(packageRoots.Count);
            foreach (HotReloadSnapshotPackageRoot root in packageRoots)
            {
                string resolvedPath = HotReloadPackageFolderMatch.NormalizeFolder(root.ResolvedPath);
                packages.Add(new NormalizedPackageRoot(
                    resolvedPath,
                    RelativeTo(resolvedPath, compilingRoot, comparison),
                    RelativeTo(resolvedPath, ownRoot, comparison),
                    HotReloadPackageFolderMatch.NormalizeFolder(root.AssetPath),
                    root.IsImmutable));
            }

            return packages;
        }

        // The rest of path after root and "/", or null when path is not inside root.
        private static string RelativeTo(string path, string root, StringComparison comparison)
        {
            int length = HotReloadPackageFolderMatch.PrefixLength(path, root, comparison);
            return length < 0 ? null : path.Substring(length + 1);
        }
    }
}
