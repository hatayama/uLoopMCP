using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using NUnit.Framework;

using UnityEngine;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

using UnityCompilationAssembly = UnityEditor.Compilation.Assembly;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// Covers listing the compiled assemblies from the dlls and PDBs on disk, and, on this project's
    /// real assemblies, that the listing and the sources read from each PDB agree with what Unity's
    /// compilation pipeline and the Package Manager report.
    /// </summary>
    public sealed class HotReloadCaptureAssemblyEnumeratorTests
    {
        private const int MaxListedMismatches = 5;

        private string _projectRoot;

        [SetUp]
        public void SetUp()
        {
            _projectRoot = Path.Combine(Path.GetTempPath(), "uloop-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_projectRoot, "Assets"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_projectRoot))
            {
                Directory.Delete(_projectRoot, true);
            }
        }

        /// <summary>
        /// Verifies a dll without a PDB beside it is not listed, and the dlls that have one are listed
        /// in ordinal order with their paths.
        /// </summary>
        [Test]
        public void ListCompiled_DllWithoutPdb_IsNotListed()
        {
            // Why not Path.Combine with the "Library/ScriptAssemblies" constant: on Windows that keeps
            // the "/" inside the constant, while the listing returns paths with "\" throughout.
            string directory = CompiledAssemblyLayout.Resolve(_projectRoot).CompiledAssembliesDirectory;
            Directory.CreateDirectory(directory);
            WriteEmptyFiles(directory, "b.dll", "b.pdb", "A.dll", "A.pdb", "NoPdb.dll", "notes.txt");

            IReadOnlyList<HotReloadCaptureAssemblyCandidate> candidates =
                HotReloadCaptureAssemblyEnumerator.ListCompiled(_projectRoot);

            Assert.That(candidates.Select(candidate => candidate.Name), Is.EqualTo(new[] { "A", "b" }));
            Assert.That(candidates[0].DllPath, Is.EqualTo(Path.Combine(directory, "A.dll")));
            Assert.That(candidates[0].PdbPath, Is.EqualTo(Path.Combine(directory, "A.pdb")));
        }

        /// <summary>
        /// Verifies a project whose compiled assemblies directory does not exist lists nothing.
        /// </summary>
        [Test]
        public void ListCompiled_WithoutTheCompiledAssembliesDirectory_ListsNothing()
        {
            IReadOnlyList<HotReloadCaptureAssemblyCandidate> candidates =
                HotReloadCaptureAssemblyEnumerator.ListCompiled(_projectRoot);

            Assert.That(candidates, Is.Empty);
        }

        /// <summary>
        /// Verifies, on this project, that the listed assemblies are the compilation assemblies Unity
        /// lists with a PDB beside their dll, that each read-only classification is the Package
        /// Manager's, and that for each editable one the sources read from the PDB are Unity's source files.
        /// </summary>
        [Test]
        public void CompiledAssemblyEnumeration_ListsTheSameAssembliesAndSourcesAsGetAssemblies()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            CompiledAssemblyLayout layout = CompiledAssemblyLayout.Resolve(projectRoot);
            HotReloadSnapshotCaptureInputs inputs = HotReloadSnapshotCaptureInputs.ReadOnMainThread();
            StringComparer comparer = HotReloadSourcePathNormalizer.ProjectRelativePathComparer();
            Dictionary<string, UnityCompilationAssembly> fromUnity = new Dictionary<string, UnityCompilationAssembly>(StringComparer.Ordinal);
            foreach (UnityCompilationAssembly assembly in HotReloadCompilationAssemblies.Current())
            {
                if (File.Exists(layout.DllPath(assembly.name)) && File.Exists(layout.PdbPath(assembly.name)))
                {
                    fromUnity[assembly.name] = assembly;
                }
            }

            IReadOnlyList<HotReloadCaptureAssemblyCandidate> candidates = HotReloadCaptureAssemblyEnumerator.ListCompiled(projectRoot);
            List<string> mismatches = new List<string>();
            int immutableAssemblies = 0;
            int editableSources = 0;
            foreach (HotReloadCaptureAssemblyCandidate candidate in candidates)
            {
                if (!fromUnity.TryGetValue(candidate.Name, out UnityCompilationAssembly unityAssembly))
                {
                    mismatches.Add(candidate.Name + ": listed from disk but not by Unity.");
                    continue;
                }

                HotReloadCaptureAssembly assembly = HotReloadCaptureAssemblyEnumerator.ReadSources(candidate, inputs);
                string[] sourceFiles = unityAssembly.sourceFiles;
                bool expectedImmutable = sourceFiles.Length > 0
                    && HotReloadSourceSnapshotter.ShouldSkipImmutablePackageSources(sourceFiles);
                immutableAssemblies += expectedImmutable ? 1 : 0;
                if (assembly.IsImmutablePackageAssembly != expectedImmutable)
                {
                    mismatches.Add(candidate.Name + ": read-only is " + assembly.IsImmutablePackageAssembly
                        + " but the Package Manager says " + expectedImmutable + ".");
                    continue;
                }

                // Why the sources of a read-only package's assembly are not compared: the capture
                // copies none of them, and some such packages list files their PDB does not.
                if (expectedImmutable)
                {
                    continue;
                }

                editableSources += assembly.SourceAssetPaths.Count;
                HashSet<string> fromPdb = new HashSet<string>(assembly.SourceAssetPaths, comparer);
                if (!fromPdb.SetEquals(sourceFiles.Select(HotReloadSourcePathNormalizer.ToForwardSlashes)))
                {
                    mismatches.Add(candidate.Name + ": the sources differ from Unity's.");
                }
            }

            foreach (string name in fromUnity.Keys)
            {
                if (!candidates.Any(candidate => candidate.Name == name))
                {
                    mismatches.Add(name + ": listed by Unity but not from disk.");
                }
            }

            string summary = "Listed " + candidates.Count + " assemblies (Unity: " + fromUnity.Count + "), "
                + immutableAssemblies + " read-only, " + editableSources + " sources of editable ones; " + mismatches.Count + " mismatches";
            TestContext.WriteLine(summary + ".");
            Assert.That(candidates.Count, Is.GreaterThan(0), "No compiled assembly with a PDB was listed.");
            Assert.That(immutableAssemblies, Is.GreaterThan(0), "No read-only package assembly was found to compare.");
            Assert.That(
                mismatches,
                Is.Empty,
                summary + ":\n" + string.Join("\n", mismatches.Take(MaxListedMismatches)));
        }

        private static void WriteEmptyFiles(string directory, params string[] fileNames)
        {
            foreach (string fileName in fileNames)
            {
                File.WriteAllBytes(Path.Combine(directory, fileName), Array.Empty<byte>());
            }
        }
    }
}
