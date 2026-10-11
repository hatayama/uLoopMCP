using System;
using System.Collections.Generic;
using System.IO;

using NUnit.Framework;

using UnityEditor.Compilation;

using UnityEngine;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

using UnityCompilationAssembly = UnityEditor.Compilation.Assembly;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// Covers the values the snapshot capture reads from Unity before it starts: how one of Unity's
    /// compilation assemblies is classified from the package roots, and, on this project's real
    /// assemblies, that the classification agrees with the Package Manager's answer.
    /// </summary>
    public sealed class HotReloadSnapshotCaptureInputsTests
    {
        private const int MaxListedMismatches = 5;

        private static readonly HotReloadSnapshotPackageRoot[] Packages =
        {
            new HotReloadSnapshotPackageRoot("/proj/Packages/src", "Packages/com.example.embedded", false),
            new HotReloadSnapshotPackageRoot("/elsewhere/LocalPkg", "Packages/com.example.local", false),
            new HotReloadSnapshotPackageRoot("/proj/Library/PackageCache/com.example.reg@1.0.0", "Packages/com.example.reg", true),
        };

        /// <summary>
        /// Verifies an assembly whose first source lies in an immutable package is classified as
        /// immutable, and keeps its name and sources.
        /// </summary>
        [Test]
        public void ToCaptureAssembly_FirstSourceInAnImmutablePackage_IsImmutable()
        {
            HotReloadCaptureAssembly assembly = Classify("Packages/com.example.reg/Runtime/A.cs", "Assets/B.cs");

            Assert.That(assembly.IsImmutablePackageAssembly, Is.True);
            Assert.That(assembly.Name, Is.EqualTo("Fixture"));
            Assert.That(assembly.SourceAssetPaths, Is.EqualTo(new[] { "Packages/com.example.reg/Runtime/A.cs", "Assets/B.cs" }));
        }

        /// <summary>
        /// Verifies sources of an embedded package, a local package and Assets are editable.
        /// </summary>
        [TestCase("Packages/com.example.embedded/Editor/A.cs")]
        [TestCase("Packages/com.example.local/A.cs")]
        [TestCase("Assets/A.cs")]
        public void ToCaptureAssembly_FirstSourceEditable_IsNotImmutable(string firstSource)
        {
            HotReloadCaptureAssembly assembly = Classify(firstSource);

            Assert.That(assembly.IsImmutablePackageAssembly, Is.False);
        }

        /// <summary>
        /// Verifies a package whose name only starts with an immutable package's name is not taken for it.
        /// </summary>
        [Test]
        public void ToCaptureAssembly_PackageNameExtendingAnImmutableOne_IsNotImmutable()
        {
            HotReloadCaptureAssembly assembly = Classify("Packages/com.example.reg2/A.cs");

            Assert.That(assembly.IsImmutablePackageAssembly, Is.False);
        }

        /// <summary>
        /// Verifies an assembly without sources is classified as editable with an empty source list.
        /// </summary>
        [Test]
        public void ToCaptureAssembly_WithoutSources_IsNotImmutableAndHasNoSources()
        {
            HotReloadCaptureAssembly assembly = Classify();

            Assert.That(assembly.IsImmutablePackageAssembly, Is.False);
            Assert.That(assembly.SourceAssetPaths, Is.Empty);
        }

        /// <summary>
        /// Verifies, for every compilation assembly of this project that has sources, that the
        /// classification from the package roots equals the Package Manager's answer the capture
        /// asked before.
        /// </summary>
        [Test]
        public void ToCaptureAssembly_FromPackageRoots_MatchesThePackageManagerAnswerForEveryAssembly()
        {
            IReadOnlyList<HotReloadSnapshotPackageRoot> packageRoots = HotReloadSnapshotPackageRoots.ReadCurrent();
            StringComparison comparison = HotReloadSnapshotCaptureInputs.CurrentPathComparison();
            List<string> mismatches = new List<string>();
            int checkedAssemblies = 0;
            int immutableAssemblies = 0;

            foreach (UnityCompilationAssembly compilationAssembly in HotReloadCompilationAssemblies.Current())
            {
                string[] sourceFiles = compilationAssembly.sourceFiles;
                if (sourceFiles == null || sourceFiles.Length == 0)
                {
                    continue;
                }

                checkedAssemblies++;
                bool expected = HotReloadSourceSnapshotter.ShouldSkipImmutablePackageSources(sourceFiles);
                immutableAssemblies += expected ? 1 : 0;
                bool actual = HotReloadSnapshotCaptureInputs
                    .ToCaptureAssembly(compilationAssembly, packageRoots, comparison)
                    .IsImmutablePackageAssembly;
                if (actual != expected)
                {
                    mismatches.Add(compilationAssembly.name + " (" + sourceFiles[0] + "): package roots say "
                        + actual + ", the Package Manager says " + expected + ".");
                }
            }

            TestContext.WriteLine(
                "Checked " + checkedAssemblies + " assemblies, " + immutableAssemblies + " immutable; "
                + mismatches.Count + " mismatches.");
            Assert.That(checkedAssemblies, Is.GreaterThan(0), "No compilation assembly with sources was found.");
            Assert.That(immutableAssemblies, Is.GreaterThan(0), "No immutable package assembly was found to compare.");
            Assert.That(
                mismatches,
                Is.Empty,
                "Checked " + checkedAssemblies + " assemblies, " + immutableAssemblies + " immutable; "
                + mismatches.Count + " mismatches:\n"
                + string.Join("\n", mismatches.GetRange(0, Math.Min(MaxListedMismatches, mismatches.Count))));
        }

        /// <summary>
        /// Verifies the inputs read on the main thread name this project's root and carry one entry
        /// for each compilation assembly Unity lists.
        /// </summary>
        [Test]
        public void ReadOnMainThread_CarriesTheProjectRootAndEveryCompilationAssembly()
        {
            HotReloadSnapshotCaptureInputs inputs = HotReloadSnapshotCaptureInputs.ReadOnMainThread();

            Assert.That(inputs.ProjectRoot, Is.EqualTo(Path.GetFullPath(Path.Combine(Application.dataPath, ".."))));
            Assert.That(inputs.Assemblies.Count, Is.EqualTo(HotReloadCompilationAssemblies.Current().Count));
            Assert.That(inputs.PackageRoots, Is.Not.Empty);
        }

        private static HotReloadCaptureAssembly Classify(params string[] sourceFiles)
        {
            UnityCompilationAssembly compilationAssembly = new UnityCompilationAssembly(
                "Fixture",
                "Library/ScriptAssemblies/Fixture.dll",
                sourceFiles,
                Array.Empty<string>(),
                Array.Empty<UnityCompilationAssembly>(),
                Array.Empty<string>(),
                AssemblyFlags.EditorAssembly);
            return HotReloadSnapshotCaptureInputs.ToCaptureAssembly(compilationAssembly, Packages, StringComparison.Ordinal);
        }
    }
}
