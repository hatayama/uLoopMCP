using System;
using System.Collections.Generic;

using Mono.Cecil.Cil;

using NUnit.Framework;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// Covers how the document urls of a PDB map to the asset paths Unity uses for the sources of an
    /// assembly, and whether the assembly belongs to an immutable package. Only synthetic urls and
    /// package roots are used; nothing is read from disk.
    /// </summary>
    public sealed class HotReloadCompiledSourceMapTests
    {
        private const string ProjectRoot = "/proj";
        private const string EmbeddedResolvedPath = "/proj/Packages/src";
        private const string EmbeddedAssetPath = "Packages/com.example.embedded";
        private const string LocalResolvedPath = "/elsewhere/LocalPkg";
        private const string LocalAssetPath = "Packages/com.example.local";
        private const string RegistryResolvedPath = "/proj/Library/PackageCache/com.example.reg@1.0.0";
        private const string RegistryAssetPath = "Packages/com.example.reg";

        private static readonly HotReloadSnapshotPackageRoot[] Packages =
        {
            new HotReloadSnapshotPackageRoot(EmbeddedResolvedPath, EmbeddedAssetPath, false),
            new HotReloadSnapshotPackageRoot(LocalResolvedPath, LocalAssetPath, false),
            new HotReloadSnapshotPackageRoot(RegistryResolvedPath, RegistryAssetPath, true),
        };

        /// <summary>
        /// What: a project-relative url of an Assets source maps to its asset path, and the assembly is not immutable.
        /// </summary>
        [Test]
        public void FromDocuments_ProjectRelativeAssetsSource_MapsToTheAssetPath()
        {
            HotReloadCompiledSources sources = Map("./Assets/A.cs");

            Assert.That(sources.AssetPaths, Is.EqualTo(new[] { "Assets/A.cs" }));
            Assert.That(sources.IsImmutablePackageAssembly, Is.False);
        }

        /// <summary>
        /// What: a url written with backslashes maps to the asset path written with slashes.
        /// </summary>
        [Test]
        public void FromDocuments_BackslashUrl_MapsToASlashAssetPath()
        {
            HotReloadCompiledSources sources = Map(".\\Assets\\B.cs");

            Assert.That(sources.AssetPaths, Is.EqualTo(new[] { "Assets/B.cs" }));
        }

        /// <summary>
        /// What: an absolute url inside the compiling project, as an assembly without a pathmap records it, maps to the asset path.
        /// </summary>
        [Test]
        public void FromDocuments_AbsoluteUrlInsideTheCompilingProject_MapsToTheAssetPath()
        {
            HotReloadCompiledSources sources = Map("/proj/Assets/C.cs");

            Assert.That(sources.AssetPaths, Is.EqualTo(new[] { "Assets/C.cs" }));
        }

        /// <summary>
        /// What: a source of an embedded package whose folder differs from its name maps to the asset path under the package name.
        /// </summary>
        [Test]
        public void FromDocuments_EmbeddedPackageWhoseFolderDiffersFromItsName_MapsUnderThePackageName()
        {
            HotReloadCompiledSources sources = Map("./Packages/src/Editor/X.cs");

            Assert.That(sources.AssetPaths, Is.EqualTo(new[] { "Packages/com.example.embedded/Editor/X.cs" }));
            Assert.That(sources.IsImmutablePackageAssembly, Is.False);
        }

        /// <summary>
        /// What: a source of a local package outside the project, recorded as an absolute url, maps to the asset path under the package name.
        /// </summary>
        [Test]
        public void FromDocuments_LocalPackageOutsideTheProject_MapsUnderThePackageName()
        {
            HotReloadCompiledSources sources = Map("/elsewhere/LocalPkg/Runtime/Y.cs");

            Assert.That(sources.AssetPaths, Is.EqualTo(new[] { "Packages/com.example.local/Runtime/Y.cs" }));
            Assert.That(sources.IsImmutablePackageAssembly, Is.False);
        }

        /// <summary>
        /// What: a source of a registry package makes the assembly an immutable package assembly.
        /// </summary>
        [Test]
        public void FromDocuments_RegistryPackageSource_IsImmutable()
        {
            HotReloadCompiledSources sources = Map("./Library/PackageCache/com.example.reg@1.0.0/Z.cs");

            Assert.That(sources.IsImmutablePackageAssembly, Is.True);
            Assert.That(sources.AssetPaths, Is.EqualTo(new[] { "Packages/com.example.reg/Z.cs" }));
        }

        /// <summary>
        /// What: a relative url without a leading "./", which is how a source generator names its output, is dropped.
        /// </summary>
        [Test]
        public void FromDocuments_GeneratedDocumentName_IsDropped()
        {
            HotReloadCompiledSources sources = Map("Example.SourceGen/Gen.g.cs");

            Assert.That(sources.AssetPaths, Is.Empty);
        }

        /// <summary>
        /// What: a file inside the project but under neither Assets nor a package root is dropped.
        /// </summary>
        [Test]
        public void FromDocuments_ProjectFileOutsideAssetsAndPackages_IsDropped()
        {
            HotReloadCompiledSources sources = Map("./Library/Bee/artifacts/Gen.cs");

            Assert.That(sources.AssetPaths, Is.Empty);
        }

        /// <summary>
        /// What: a document that is not a C# source is dropped.
        /// </summary>
        [Test]
        public void FromDocuments_NonCSharpDocument_IsDropped()
        {
            HotReloadCompiledSources sources = Map("./Assets/Shader.hlsl");

            Assert.That(sources.AssetPaths, Is.Empty);
        }

        /// <summary>
        /// What: a url listed twice yields its asset path once.
        /// </summary>
        [Test]
        public void FromDocuments_DuplicateDocuments_YieldsTheAssetPathOnce()
        {
            HotReloadCompiledSources sources = Map("./Assets/A.cs", "./Assets/A.cs");

            Assert.That(sources.AssetPaths, Is.EqualTo(new[] { "Assets/A.cs" }));
        }

        /// <summary>
        /// What: for a Virtual Player, whose urls are relative to the main project, an embedded package
        /// source maps whether Package Manager resolves the package under the player's root or the main project's.
        /// </summary>
        [TestCase("/proj/Library/VP/p1/Packages/src")]
        [TestCase("/proj/Packages/src")]
        public void FromDocuments_VirtualPlayerEmbeddedPackage_MapsUnderThePackageName(string resolvedPath)
        {
            HotReloadSnapshotPackageRoot[] packages =
            {
                new HotReloadSnapshotPackageRoot(resolvedPath, EmbeddedAssetPath, false),
            };

            HotReloadCompiledSources sources = HotReloadCompiledSourceMap.FromDocuments(
                Documents("./Packages/src/X.cs"),
                ProjectRoot,
                "/proj/Library/VP/p1",
                packages,
                StringComparison.Ordinal);

            Assert.That(sources.AssetPaths, Is.EqualTo(new[] { "Packages/com.example.embedded/X.cs" }));
        }

        /// <summary>
        /// What: a folder beside a package root whose name starts with the root's name is not taken for the package.
        /// </summary>
        [Test]
        public void FromDocuments_SiblingFolderOfAPackageRoot_IsDropped()
        {
            HotReloadCompiledSources sources = Map("/elsewhere/LocalPkg2/A.cs");

            Assert.That(sources.AssetPaths, Is.Empty);
        }

        /// <summary>
        /// What: the first mapped source decides immutability, so an immutable source before a mutable one makes the assembly immutable.
        /// </summary>
        [Test]
        public void FromDocuments_FirstMappedSourceDecidesImmutability_IsImmutable()
        {
            HotReloadCompiledSources sources = Map(
                "./Library/PackageCache/com.example.reg@1.0.0/Z.cs",
                "./Assets/A.cs");

            Assert.That(sources.IsImmutablePackageAssembly, Is.True);
        }

        /// <summary>
        /// What: a dropped document before a mutable source does not decide immutability.
        /// </summary>
        [Test]
        public void FromDocuments_FirstDroppedDocumentDoesNotDecide_IsMutable()
        {
            HotReloadCompiledSources sources = Map("Example.SourceGen/Gen.g.cs", "./Assets/A.cs");

            Assert.That(sources.IsImmutablePackageAssembly, Is.False);
            Assert.That(sources.AssetPaths, Is.EqualTo(new[] { "Assets/A.cs" }));
        }

        /// <summary>
        /// What: under a case-insensitive comparison a url spelled in another case still maps, keeping the url's spelling.
        /// </summary>
        [Test]
        public void FromDocuments_IgnoreCaseComparison_MapsKeepingTheUrlSpelling()
        {
            HotReloadCompiledSources sources = HotReloadCompiledSourceMap.FromDocuments(
                Documents("./ASSETS/a.CS"),
                ProjectRoot,
                ProjectRoot,
                Packages,
                StringComparison.OrdinalIgnoreCase);

            Assert.That(sources.AssetPaths, Is.EqualTo(new[] { "ASSETS/a.CS" }));
        }

        /// <summary>
        /// What: no documents yield no asset paths and a mutable assembly.
        /// </summary>
        [Test]
        public void FromDocuments_NoDocuments_IsEmptyAndMutable()
        {
            HotReloadCompiledSources sources = Map();

            Assert.That(sources.AssetPaths, Is.Empty);
            Assert.That(sources.IsImmutablePackageAssembly, Is.False);
        }

        /// <summary>
        /// What: a Windows url with a drive letter maps to the asset path on every platform, whatever its
        /// separators and the case of its drive letter.
        /// </summary>
        [TestCase("C:/proj/Assets/A.cs")]
        [TestCase("C:\\proj\\Assets\\A.cs")]
        [TestCase("c:/proj/Assets/A.cs")]
        public void FromDocuments_WindowsDriveLetterUrl_MapsToTheAssetPath(string url)
        {
            HotReloadCompiledSources sources = HotReloadCompiledSourceMap.FromDocuments(
                Documents(url),
                "C:/proj",
                "C:/proj",
                Array.Empty<HotReloadSnapshotPackageRoot>(),
                StringComparison.OrdinalIgnoreCase);

            Assert.That(sources.AssetPaths, Is.EqualTo(new[] { "Assets/A.cs" }));
        }

        /// <summary>
        /// What: an absolute url of an embedded package inside the project maps to the asset path under the package name.
        /// </summary>
        [Test]
        public void FromDocuments_AbsoluteUrlOfAnEmbeddedPackage_MapsUnderThePackageName()
        {
            HotReloadCompiledSources sources = Map("/proj/Packages/src/Editor/X.cs");

            Assert.That(sources.AssetPaths, Is.EqualTo(new[] { "Packages/com.example.embedded/Editor/X.cs" }));
            Assert.That(sources.IsImmutablePackageAssembly, Is.False);
        }

        /// <summary>
        /// What: an absolute url of a registry package inside the project makes the assembly immutable.
        /// </summary>
        [Test]
        public void FromDocuments_AbsoluteUrlOfARegistryPackage_IsImmutable()
        {
            HotReloadCompiledSources sources = Map("/proj/Library/PackageCache/com.example.reg@1.0.0/Z.cs");

            Assert.That(sources.IsImmutablePackageAssembly, Is.True);
            Assert.That(sources.AssetPaths, Is.EqualTo(new[] { "Packages/com.example.reg/Z.cs" }));
        }

        /// <summary>
        /// What: a folder inside the project beside a package root whose name starts with the root's name is not taken for the package.
        /// </summary>
        [Test]
        public void FromDocuments_SiblingFolderOfAnInProjectPackageRoot_IsDropped()
        {
            HotReloadCompiledSources sources = Map("./Packages/src2/A.cs");

            Assert.That(sources.AssetPaths, Is.Empty);
        }

        private static HotReloadCompiledSources Map(params string[] urls)
        {
            return HotReloadCompiledSourceMap.FromDocuments(
                Documents(urls),
                ProjectRoot,
                ProjectRoot,
                Packages,
                StringComparison.Ordinal);
        }

        private static List<HotReloadPdbDocument> Documents(params string[] urls)
        {
            List<HotReloadPdbDocument> documents = new List<HotReloadPdbDocument>(urls.Length);
            foreach (string url in urls)
            {
                documents.Add(new HotReloadPdbDocument(url, DocumentHashAlgorithm.None, Array.Empty<byte>()));
            }

            return documents;
        }
    }
}
