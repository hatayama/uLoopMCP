using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using NUnit.Framework;

using UnityEngine;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// EditMode coverage for the capture's PDB check of sources written since the compile started,
    /// run against this test assembly's own DLL and PDB and copies of its real fixture sources.
    /// </summary>
    public sealed class HotReloadSnapshotEditedDuringCompileTests
    {
        private const string TestAssemblyName = "UnityCLILoop.Tests.Editor.HotReload";
        private const string FixtureProjectRelativePath =
            "Assets/Tests/Editor/HotReload/HotReloadE2EFixtures.cs";
        // A file without method bodies, so the PDB has no document for it.
        private const string BodylessFixtureProjectRelativePath =
            "Assets/Tests/Editor/HotReload/HotReloadSnapshotBodylessFixture.cs";
        private const string PausePointsRuntimeAssemblyName = "UnityCLILoop.PausePoints.Runtime";
        // A file of this repository's embedded package, whose folder differs from its package name,
        // so its asset path and the path its PDB records differ.
        private const string PackageSourceAssetPath =
            "Packages/io.github.hatayama.uloopmcp/Runtime/PausePoints/UloopPausePoint.cs";
        private const string PackageSourcePhysicalPath = "Packages/src/Runtime/PausePoints/UloopPausePoint.cs";

        private static readonly long CompileStartUtcTicks = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        private static readonly DateTime SinceTheStartUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime BeforeTheStartUtc = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private string _realProjectRoot;
        private string _tempRoot;
        private string _snapshotDirectory;

        [SetUp]
        public void SetUp()
        {
            _realProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _tempRoot = Path.Combine(Path.GetTempPath(), "uloop-test-" + Guid.NewGuid().ToString("N"));
            _snapshotDirectory = Path.Combine(_tempRoot, "Snapshot");
            Directory.CreateDirectory(_tempRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }
        }

        /// <summary>
        /// Verifies that a source written since the compile started whose bytes match the PDB gets an
        /// unmarked stamp.
        /// </summary>
        [Test]
        public void CaptureAtomically_ASourceWrittenSinceTheStartThatMatchesThePdb_RecordsAnUnmarkedStamp()
        {
            PlantSource(FixtureProjectRelativePath, RealBytes(FixtureProjectRelativePath), SinceTheStartUtc);

            HotReloadSourceStampManifest manifest = Capture(FixtureProjectRelativePath, TestAssemblyName, HotReloadSnapshotPackageRoots.ReadCurrent());

            string snapshotFileName = SnapshotFileName(FixtureProjectRelativePath);
            Assert.That(manifest.IsEditedAfterCompile(snapshotFileName), Is.False);
            Assert.That(manifest.TryGetStamp(snapshotFileName, out long _, out long _), Is.True);
        }

        /// <summary>
        /// Verifies that a source of an embedded package written since the compile started whose bytes
        /// match the PDB gets an unmarked stamp: the check names it by the folder behind the package
        /// path, as the PDB records it, from the package roots it was given. The PDB records the
        /// package's sources relative to the compiling project, so the package root is moved under
        /// the temporary root, as the real one lies under the real project.
        /// </summary>
        [Test]
        public void CaptureAtomically_APackageSourceWrittenSinceTheStartThatMatchesThePdb_RecordsAnUnmarkedStamp()
        {
            byte[] realBytes = File.ReadAllBytes(Path.Combine(_realProjectRoot, PackageSourcePhysicalPath));
            PlantSource(PackageSourceAssetPath, realBytes, SinceTheStartUtc);

            HotReloadSourceStampManifest manifest = Capture(
                PackageSourceAssetPath,
                PausePointsRuntimeAssemblyName,
                PackageRootsMovedUnderTheTemporaryRoot());

            string snapshotFileName = SnapshotFileName(PackageSourceAssetPath);
            Assert.That(manifest.IsEditedAfterCompile(snapshotFileName), Is.False);
            Assert.That(manifest.TryGetStamp(snapshotFileName, out long _, out long _), Is.True);
        }

        /// <summary>
        /// Verifies that a source written since the compile started whose bytes differ from the PDB is
        /// marked, and that its copy is still written with the edited bytes.
        /// </summary>
        [Test]
        public void CaptureAtomically_ASourceWrittenSinceTheStartThatDiffersFromThePdb_MarksItAndStillCopiesIt()
        {
            byte[] editedBytes = EditedBytes(FixtureProjectRelativePath);
            PlantSource(FixtureProjectRelativePath, editedBytes, SinceTheStartUtc);

            HotReloadSourceStampManifest manifest = Capture(FixtureProjectRelativePath, TestAssemblyName, HotReloadSnapshotPackageRoots.ReadCurrent());

            string snapshotFileName = SnapshotFileName(FixtureProjectRelativePath);
            string snapshotPath = Path.Combine(_snapshotDirectory, snapshotFileName);
            Assert.That(manifest.IsEditedAfterCompile(snapshotFileName), Is.True);
            Assert.That(File.Exists(snapshotPath), Is.True);
            Assert.That(File.ReadAllBytes(snapshotPath), Is.EqualTo(editedBytes));
        }

        /// <summary>
        /// Verifies that a source written before the compile started is not checked against the PDB,
        /// so even bytes the PDB does not match stay unmarked.
        /// </summary>
        [Test]
        public void CaptureAtomically_ASourceWrittenBeforeTheStart_IsNotCheckedAgainstThePdb()
        {
            PlantSource(FixtureProjectRelativePath, EditedBytes(FixtureProjectRelativePath), BeforeTheStartUtc);

            HotReloadSourceStampManifest manifest = Capture(FixtureProjectRelativePath, TestAssemblyName, HotReloadSnapshotPackageRoots.ReadCurrent());

            Assert.That(manifest.IsEditedAfterCompile(SnapshotFileName(FixtureProjectRelativePath)), Is.False);
        }

        /// <summary>
        /// Verifies that a source written since the compile started that the PDB has no document for is
        /// marked, because it cannot be confirmed as the compiled source.
        /// </summary>
        [Test]
        public void CaptureAtomically_ASourceThePdbHasNoDocumentFor_MarksIt()
        {
            PlantSource(
                BodylessFixtureProjectRelativePath,
                RealBytes(BodylessFixtureProjectRelativePath),
                SinceTheStartUtc);

            HotReloadSourceStampManifest manifest = Capture(BodylessFixtureProjectRelativePath, TestAssemblyName, HotReloadSnapshotPackageRoots.ReadCurrent());

            Assert.That(manifest.IsEditedAfterCompile(SnapshotFileName(BodylessFixtureProjectRelativePath)), Is.True);
        }

        private HotReloadSourceStampManifest Capture(
            string projectRelativePath,
            string assemblyName,
            IReadOnlyList<HotReloadSnapshotPackageRoot> packageRoots)
        {
            string dllPath = Path.Combine(
                _realProjectRoot,
                HotReloadConstants.ScriptAssembliesRelativeDirectory,
                assemblyName + HotReloadConstants.CompiledAssemblyExtension);
            HotReloadSnapshotSourceCheck check = new HotReloadSnapshotSourceCheck(
                CompileStartUtcTicks,
                dllPath,
                Path.ChangeExtension(dllPath, ".pdb"),
                HotReloadAssemblyMvid.Read(dllPath),
                new HotReloadPdbDocumentIndex(Path.Combine(_tempRoot, "PdbDocuments")),
                packageRoots,
                HotReloadSnapshotCaptureInputs.CurrentPathComparison());

            HotReloadSourceSnapshotCopier.CaptureAtomically(
                _tempRoot,
                _snapshotDirectory,
                new[] { projectRelativePath },
                assemblyName,
                check,
                new HotReloadSnapshotCaptureStats());

            return HotReloadSourceStampManifest.Load(_snapshotDirectory);
        }

        // The registered packages, with every folder under the real project moved to the same place
        // under the temporary root.
        private List<HotReloadSnapshotPackageRoot> PackageRootsMovedUnderTheTemporaryRoot()
        {
            string realRootPrefix = _realProjectRoot.Replace('\\', '/').TrimEnd('/') + "/";
            List<HotReloadSnapshotPackageRoot> roots = new List<HotReloadSnapshotPackageRoot>();
            foreach (HotReloadSnapshotPackageRoot root in HotReloadSnapshotPackageRoots.ReadCurrent())
            {
                string resolvedPath = root.ResolvedPath.Replace('\\', '/');
                if (resolvedPath.StartsWith(realRootPrefix, StringComparison.Ordinal))
                {
                    resolvedPath = Path.Combine(_tempRoot, resolvedPath.Substring(realRootPrefix.Length));
                }

                roots.Add(new HotReloadSnapshotPackageRoot(resolvedPath, root.AssetPath, root.IsImmutable));
            }

            return roots;
        }

        private byte[] RealBytes(string projectRelativePath)
        {
            return File.ReadAllBytes(Path.Combine(_realProjectRoot, projectRelativePath));
        }

        private byte[] EditedBytes(string projectRelativePath)
        {
            return RealBytes(projectRelativePath).Concat(Encoding.UTF8.GetBytes("// edited\n")).ToArray();
        }

        // Why a write time in whole seconds: SetLastWriteTimeUtc stores microseconds while
        // LastWriteTimeUtc reports 100 ns ticks, so only such a value is read back exactly.
        private void PlantSource(string projectRelativePath, byte[] bytes, DateTime lastWriteTimeUtc)
        {
            string path = Path.Combine(_tempRoot, projectRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, bytes);
            File.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
        }

        private static string SnapshotFileName(string projectRelativePath)
        {
            return HotReloadSourceSnapshotLayout.SourceFileName(projectRelativePath);
        }
    }
}
