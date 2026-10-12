using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using NUnit.Framework;

using UnityEngine;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// Contract of reading every row of a PDB's document table: it lists sources without a method
    /// body, which the sequence-point walk of the document index leaves out, and it refuses a PDB
    /// of another build than the dll.
    /// </summary>
    public sealed class HotReloadPdbDocumentTableTests
    {
        private const string TestAssemblyName = "UnityCLILoop.Tests.Editor.HotReload";
        private const string OtherAssemblyName = "UnityCLILoop.PausePoints.Runtime";

        internal static readonly string[] BodylessFixtureAssetPaths =
        {
            "Assets/Tests/Editor/HotReload/Fixtures/PdbDocumentTable/EmptySourceFixture.cs",
            "Assets/Tests/Editor/HotReload/Fixtures/PdbDocumentTable/CommentOnlySourceFixture.cs",
            "Assets/Tests/Editor/HotReload/Fixtures/PdbDocumentTable/InterfaceOnlySourceFixture.cs",
        };

        /// <summary>
        /// What: the table lists the empty, comment-only and interface-only fixtures, none of which the
        /// sequence-point walk returns. The urls are compared by their end because an assembly under
        /// Assets has no pathmap, so its urls are absolute.
        /// </summary>
        [Test]
        public void Read_ListsSourcesWithoutAMethodBody()
        {
            string dllPath = DllPath(TestAssemblyName);
            string pdbPath = Path.ChangeExtension(dllPath, ".pdb");

            List<HotReloadPdbDocument> table = HotReloadPdbDocumentTable.Read(dllPath, pdbPath, out int? _);
            List<HotReloadPdbDocument> withSequencePoints = HotReloadPdbDocumentIndex.ReadDocuments(dllPath, pdbPath);

            foreach (string assetPath in BodylessFixtureAssetPaths)
            {
                Assert.That(
                    table.Any(document => EndsWithAssetPath(document, assetPath)),
                    Is.True,
                    "The document table must list " + assetPath + ".");
                Assert.That(
                    withSequencePoints.Any(document => EndsWithAssetPath(document, assetPath)),
                    Is.False,
                    "No sequence point may refer to " + assetPath + ".");
            }
        }

        /// <summary>
        /// What: a PDB of another assembly than the dll makes the read throw instead of returning its documents.
        /// </summary>
        [Test]
        public void Read_WhenThePdbBelongsToAnotherBuild_Throws()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "uloop-pdb-document-table-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string dllPath = Path.Combine(directory, "X.dll");
                string pdbPath = Path.Combine(directory, "X.pdb");
                File.Copy(DllPath(TestAssemblyName), dllPath);
                File.Copy(Path.ChangeExtension(DllPath(OtherAssemblyName), ".pdb"), pdbPath);

                Assert.Throws<InvalidOperationException>(() => HotReloadPdbDocumentTable.Read(dllPath, pdbPath, out int? _));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        /// <summary>
        /// What: the non-throwing read returns null, rather than documents, for a PDB of another
        /// assembly than the dll.
        /// </summary>
        [Test]
        public void TryRead_PdbOfAnotherBuild_ReturnsNull()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "uloop-pdb-document-table-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string dllPath = Path.Combine(directory, "X.dll");
                string pdbPath = Path.Combine(directory, "X.pdb");
                File.Copy(DllPath(TestAssemblyName), dllPath);
                File.Copy(Path.ChangeExtension(DllPath(OtherAssemblyName), ".pdb"), pdbPath);

                List<HotReloadPdbDocument> documents = HotReloadPdbDocumentTable.TryRead(dllPath, pdbPath, out int? sourceFileCount);

                Assert.That(documents, Is.Null);
                Assert.That(sourceFileCount, Is.Null);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        /// <summary>
        /// What: for a PDB the compiler wrote, the non-throwing read returns the same documents as
        /// Read, and the source count it records equals the number of documents.
        /// </summary>
        [Test]
        public void TryRead_CompilerPdb_ReturnsEveryDocumentAndTheSourceCount()
        {
            string dllPath = DllPath(TestAssemblyName);
            string pdbPath = Path.ChangeExtension(dllPath, ".pdb");

            List<HotReloadPdbDocument> documents = HotReloadPdbDocumentTable.TryRead(dllPath, pdbPath, out int? sourceFileCount);
            List<HotReloadPdbDocument> read = HotReloadPdbDocumentTable.Read(dllPath, pdbPath, out int? _);

            Assert.That(documents, Is.Not.Null);
            Assert.That(sourceFileCount, Is.EqualTo(documents.Count));
            Assert.That(
                documents.Select(document => document.Url),
                Is.EqualTo(read.Select(document => document.Url)));
        }

        private static bool EndsWithAssetPath(HotReloadPdbDocument document, string assetPath)
        {
            return document.Url.Replace('\\', '/').EndsWith("/" + assetPath, StringComparison.Ordinal);
        }

        private static string DllPath(string assemblyName)
        {
            return Path.Combine(
                Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                HotReloadConstants.ScriptAssembliesRelativeDirectory,
                assemblyName + HotReloadConstants.CompiledAssemblyExtension);
        }
    }
}
