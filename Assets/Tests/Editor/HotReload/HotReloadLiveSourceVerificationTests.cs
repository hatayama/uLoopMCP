using System;
using System.IO;

using NUnit.Framework;

using UnityEngine;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// EditMode coverage for reading a source file on disk only when the compiled assembly's PDB
    /// confirms it holds the bytes the compiler read.
    /// </summary>
    public class HotReloadLiveSourceVerificationTests
    {
        private const string TestAssemblyName = "UnityCLILoop.Tests.Editor.HotReload";
        private const string BomFixtureProjectRelativePath =
            "Assets/Tests/Editor/HotReload/HotReloadLiveSourceBomFixture.cs";
        private const string BodylessFixtureProjectRelativePath =
            "Assets/Tests/Editor/HotReload/HotReloadSnapshotBodylessFixture.cs";

        private string _temporaryRoot;

        [SetUp]
        public void SetUp()
        {
            _temporaryRoot = Path.Combine(Path.GetTempPath(), "uloop-live-source-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temporaryRoot))
            {
                Directory.Delete(_temporaryRoot, recursive: true);
            }
        }

        /// <summary>
        /// What: a live file and a snapshot holding the same bytes, a byte order mark included, decode
        /// to the same text, and the mark is not part of it.
        /// </summary>
        [Test]
        public void LoadVerifiedLiveSourceOrNull_ReturnsTheSameTextAsTheSnapshotForTheSameBytes()
        {
            string dllPath = TestAssemblyDllPath();
            byte[] compiledBytes = File.ReadAllBytes(Path.Combine(ProjectRoot(), BomFixtureProjectRelativePath));
            Assert.That(compiledBytes[0], Is.EqualTo(0xEF), "Precondition: the fixture must start with a byte order mark.");
            WriteLiveFile(BomFixtureProjectRelativePath, compiledBytes);
            string snapshotDirectory = HotReloadSourceSnapshotLayout.AssemblyDirectory(
                _temporaryRoot,
                TestAssemblyName,
                HotReloadAssemblyMvid.Read(dllPath));
            Directory.CreateDirectory(snapshotDirectory);
            File.WriteAllBytes(
                HotReloadSourceSnapshotLayout.SourcePath(snapshotDirectory, BomFixtureProjectRelativePath),
                compiledBytes);

            string live = HotReloadSourceBaseline.LoadVerifiedLiveSourceAt(
                _temporaryRoot,
                BomFixtureProjectRelativePath,
                dllPath,
                HotReloadPdbDocumentIndex.Shared);
            string snapshot = HotReloadSourceBaseline.LoadVerifiedSnapshotSourceAt(
                _temporaryRoot,
                BomFixtureProjectRelativePath,
                dllPath,
                HotReloadPdbDocumentIndex.Shared);

            Assert.That(live, Is.Not.Null);
            Assert.That(live, Is.EqualTo(snapshot));
            Assert.That(live[0], Is.Not.EqualTo('﻿'));
        }

        /// <summary>
        /// What: a live file whose bytes differ from the checksum the PDB recorded is not returned.
        /// </summary>
        [Test]
        public void LoadVerifiedLiveSourceAt_WhenTheLiveBytesDifferFromThePdb_ReturnsNull()
        {
            byte[] editedBytes = File.ReadAllBytes(Path.Combine(ProjectRoot(), BomFixtureProjectRelativePath));
            editedBytes[editedBytes.Length - 1] = (byte)(editedBytes[editedBytes.Length - 1] ^ 0xFF);
            WriteLiveFile(BomFixtureProjectRelativePath, editedBytes);

            string live = HotReloadSourceBaseline.LoadVerifiedLiveSourceAt(
                _temporaryRoot,
                BomFixtureProjectRelativePath,
                TestAssemblyDllPath(),
                HotReloadPdbDocumentIndex.Shared);

            Assert.That(live, Is.Null);
        }

        /// <summary>
        /// What: a source that is not on disk is not returned.
        /// </summary>
        [Test]
        public void LoadVerifiedLiveSourceAt_WhenTheFileIsMissing_ReturnsNull()
        {
            Directory.CreateDirectory(_temporaryRoot);

            string live = HotReloadSourceBaseline.LoadVerifiedLiveSourceAt(
                _temporaryRoot,
                BomFixtureProjectRelativePath,
                TestAssemblyDllPath(),
                HotReloadPdbDocumentIndex.Shared);

            Assert.That(live, Is.Null);
        }

        /// <summary>
        /// What: a source the PDB has no document for is not returned, because nothing confirms it.
        /// </summary>
        [Test]
        public void LoadVerifiedLiveSourceAt_WhenThePdbHasNoDocument_ReturnsNull()
        {
            string live = HotReloadSourceBaseline.LoadVerifiedLiveSourceAt(
                ProjectRoot(),
                BodylessFixtureProjectRelativePath,
                TestAssemblyDllPath(),
                HotReloadPdbDocumentIndex.Shared);

            Assert.That(live, Is.Null);
        }

        private void WriteLiveFile(string projectRelativePath, byte[] bytes)
        {
            string livePath = Path.Combine(_temporaryRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(livePath));
            File.WriteAllBytes(livePath, bytes);
        }

        private static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static string TestAssemblyDllPath()
        {
            return Path.Combine(
                ProjectRoot(),
                HotReloadConstants.ScriptAssembliesRelativeDirectory,
                TestAssemblyName + HotReloadConstants.CompiledAssemblyExtension);
        }
    }
}
