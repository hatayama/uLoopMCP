using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using NUnit.Framework;

using UnityEngine;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

using UnityCompilationAssembly = UnityEditor.Compilation.Assembly;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// Cross-checks, on this project's compiled assemblies, the source list built from each PDB's
    /// document table against the source list Unity reports for the assembly. Whether the two agree
    /// decides if the snapshot capture can stop asking Unity for its assemblies.
    /// </summary>
    public sealed class HotReloadCompiledSourceCrossCheckTests
    {
        private const string TestAssemblyName = "UnityCLILoop.Tests.Editor.HotReload";
        private const int MaxListedDifferences = 10;

        /// <summary>
        /// What: for every compiled assembly with sources, a dll and a PDB, the PDB list agrees with
        /// Unity's on whether the assembly is an immutable package's, and for the others names the same sources.
        /// </summary>
        [Test]
        public void FromDocuments_MatchesTheSourceFilesUnityListsForEveryCompiledAssembly()
        {
            CompiledAssemblyLayout layout = CompiledAssemblyLayout.Resolve(ProjectRoot());
            IReadOnlyList<HotReloadSnapshotPackageRoot> packageRoots = HotReloadSnapshotPackageRoots.ReadCurrent();
            StringComparer comparer = HotReloadSourcePathNormalizer.ProjectRelativePathComparer();
            List<string> mismatches = new List<string>();
            Stopwatch readTime = new Stopwatch();
            int checkedAssemblies = 0;

            foreach (UnityCompilationAssembly assembly in HotReloadCompilationAssemblies.Current())
            {
                string[] sourceFiles = assembly.sourceFiles;
                string dllPath = layout.DllPath(assembly.name);
                string pdbPath = layout.PdbPath(assembly.name);
                if (sourceFiles == null || sourceFiles.Length == 0 || !File.Exists(dllPath) || !File.Exists(pdbPath))
                {
                    continue;
                }

                readTime.Start();
                List<HotReloadPdbDocument> documents = HotReloadPdbDocumentTable.Read(dllPath, pdbPath);
                readTime.Stop();
                HotReloadCompiledSources sources = HotReloadCompiledSourceMap.FromDocuments(
                    documents,
                    layout.MainProjectRoot,
                    layout.ProjectRoot,
                    packageRoots,
                    PathComparison());
                checkedAssemblies++;

                bool expectedImmutable = HotReloadSourceSnapshotter.ShouldSkipImmutablePackageSources(sourceFiles);
                if (sources.IsImmutablePackageAssembly != expectedImmutable)
                {
                    mismatches.Add(
                        assembly.name + ": IsImmutablePackageAssembly is " + sources.IsImmutablePackageAssembly
                        + " but Unity's first source says " + expectedImmutable + ".");
                    continue;
                }

                if (expectedImmutable)
                {
                    continue;
                }

                HashSet<string> fromPdb = new HashSet<string>(sources.AssetPaths, comparer);
                HashSet<string> fromUnity = new HashSet<string>(
                    sourceFiles.Select(HotReloadSourcePathNormalizer.ToForwardSlashes),
                    comparer);
                if (!fromPdb.SetEquals(fromUnity))
                {
                    mismatches.Add(DescribeDifference(assembly.name, fromPdb, fromUnity));
                }
            }

            TestContext.WriteLine(
                "Read " + checkedAssemblies + " PDB document tables in " + readTime.ElapsedMilliseconds + " ms.");
            Assert.That(checkedAssemblies, Is.GreaterThan(0), "No compiled assembly with a dll and a PDB was found.");
            Assert.That(mismatches, Is.Empty, string.Join("\n", mismatches));
        }

        /// <summary>
        /// What: the test assembly's list holds the fixtures without a method body, so the list is the
        /// document table and not only the documents a sequence point refers to.
        /// </summary>
        [Test]
        public void FromDocuments_IncludesTheBodylessFixtures()
        {
            CompiledAssemblyLayout layout = CompiledAssemblyLayout.Resolve(ProjectRoot());
            List<HotReloadPdbDocument> documents = HotReloadPdbDocumentTable.Read(
                layout.DllPath(TestAssemblyName),
                layout.PdbPath(TestAssemblyName));

            HotReloadCompiledSources sources = HotReloadCompiledSourceMap.FromDocuments(
                documents,
                layout.MainProjectRoot,
                layout.ProjectRoot,
                HotReloadSnapshotPackageRoots.ReadCurrent(),
                PathComparison());

            Assert.That(sources.AssetPaths, Is.SupersetOf(HotReloadPdbDocumentTableTests.BodylessFixtureAssetPaths));
        }

        private static string DescribeDifference(string assemblyName, HashSet<string> fromPdb, HashSet<string> fromUnity)
        {
            StringBuilder text = new StringBuilder();
            text.Append(assemblyName).Append(": the source lists differ.");
            AppendOnlyIn(text, "only in the PDB", fromPdb, fromUnity);
            AppendOnlyIn(text, "only in Unity's sourceFiles", fromUnity, fromPdb);
            return text.ToString();
        }

        private static void AppendOnlyIn(StringBuilder text, string label, HashSet<string> side, HashSet<string> other)
        {
            List<string> only = side.Where(path => !other.Contains(path)).OrderBy(path => path, StringComparer.Ordinal).ToList();
            text.Append("\n  ").Append(label).Append(" (").Append(only.Count).Append("):");
            foreach (string path in only.Take(MaxListedDifferences))
            {
                text.Append("\n    ").Append(path);
            }
        }

        // The comparison the snapshot paths use on this platform: case-insensitive on Windows only.
        private static StringComparison PathComparison()
        {
            return Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        }

        private static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }
    }
}
