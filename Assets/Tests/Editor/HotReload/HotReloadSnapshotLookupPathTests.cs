using System;
using System.Collections.Generic;
using System.IO;

using NUnit.Framework;

using UnityEngine;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

using UnityCompilationAssembly = UnityEditor.Compilation.Assembly;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// Covers naming a source the way its PDB records it from package roots read beforehand, and,
    /// on this project's real sources, that the name equals the one the Package Manager gives.
    /// </summary>
    public sealed class HotReloadSnapshotLookupPathTests
    {
        private const int MaxListedMismatches = 5;

        private static readonly HotReloadSnapshotPackageRoot[] Packages =
        {
            new HotReloadSnapshotPackageRoot("/proj/Packages/src", "Packages/com.example.embedded", false),
            new HotReloadSnapshotPackageRoot("/elsewhere/LocalPkg", "Packages/com.example.local", false),
        };

        /// <summary>
        /// Verifies a source of a package inside the project is named relative to the project, one
        /// outside it by its absolute path, and an Assets source by its asset path.
        /// </summary>
        [TestCase("Packages/com.example.embedded/Editor/A.cs", "Packages/src/Editor/A.cs")]
        [TestCase("Packages/com.example.local/A.cs", "/elsewhere/LocalPkg/A.cs")]
        [TestCase("Assets/A.cs", "Assets/A.cs")]
        public void ToPdbLookupPath_NamesTheFileTheWayThePdbRecordsIt(string assetPath, string expected)
        {
            string lookupPath = HotReloadSnapshotLookupPath.ToPdbLookupPath("/proj", assetPath, Packages, StringComparison.Ordinal);

            Assert.That(lookupPath, Is.EqualTo(expected));
        }

        /// <summary>
        /// Verifies, for every source of every compilation assembly of this project, that the name
        /// built from the package roots equals the one ScriptPackageRoots.ToPhysicalPath builds by
        /// asking the Package Manager.
        /// </summary>
        [Test]
        public void PdbLookupPath_FromPackageRoots_MatchesThePackageManagerAnswerForEverySource()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string compiledProjectRoot = CompiledAssemblyLayout.Resolve(projectRoot).MainProjectRoot;
            IReadOnlyList<HotReloadSnapshotPackageRoot> packageRoots = HotReloadSnapshotPackageRoots.ReadCurrent();
            StringComparison comparison = HotReloadSnapshotCaptureInputs.CurrentPathComparison();
            List<string> mismatches = new List<string>();
            int checkedSources = 0;
            int packageSources = 0;

            foreach (UnityCompilationAssembly compilationAssembly in HotReloadCompilationAssemblies.Current())
            {
                foreach (string sourceFile in compilationAssembly.sourceFiles ?? Array.Empty<string>())
                {
                    string assetPath = sourceFile.Replace('\\', '/');
                    string expected = ScriptPackageRoots.ToPhysicalPath(compiledProjectRoot, assetPath);
                    string actual = HotReloadSnapshotLookupPath.ToPdbLookupPath(
                        compiledProjectRoot,
                        assetPath,
                        packageRoots,
                        comparison);
                    checkedSources++;
                    packageSources += expected == assetPath ? 0 : 1;
                    if (!string.Equals(actual, expected, StringComparison.Ordinal))
                    {
                        mismatches.Add(assetPath + ": package roots give " + actual + ", the Package Manager gives " + expected + ".");
                    }
                }
            }

            TestContext.WriteLine(
                "Checked " + checkedSources + " sources, " + packageSources + " renamed by a package; "
                + mismatches.Count + " mismatches.");
            Assert.That(checkedSources, Is.GreaterThan(0), "No source was found.");
            Assert.That(packageSources, Is.GreaterThan(0), "No source behind a package folder was found to compare.");
            Assert.That(
                mismatches,
                Is.Empty,
                "Checked " + checkedSources + " sources, " + packageSources + " renamed by a package; "
                + mismatches.Count + " mismatches:\n"
                + string.Join("\n", mismatches.GetRange(0, Math.Min(MaxListedMismatches, mismatches.Count))));
        }
    }
}
