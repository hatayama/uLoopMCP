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
    /// Contract of reading a rewritten PDB's documents from the compiler's own output under
    /// Library/Bee/artifacts: only a compiler dll with the same MVID whose own PDB lists every source
    /// stands in, whichever dag holds it, and anything else gives null. Each case lays out a
    /// temporary Library with copies of this project's compiled files.
    /// </summary>
    public sealed class HotReloadCompilerOutputDocumentsTests
    {
        private const string TargetAssemblyName = "UnityCLILoop.Tests.Editor.HotReload";

        private string _projectRoot;
        private string _directory;
        private string _targetDllPath;
        private string _targetPdbPath;
        private string _otherDllPath;
        private string _otherPdbPath;
        private string _scriptAssembliesDllPath;
        private string _artifactsDirectory;

        [SetUp]
        public void SetUp()
        {
            _projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            CompiledAssemblyLayout layout = CompiledAssemblyLayout.Resolve(_projectRoot);
            _targetDllPath = layout.DllPath(TargetAssemblyName);
            _targetPdbPath = layout.PdbPath(TargetAssemblyName);
            string otherAssemblyName = typeof(HotReloadSourceBaseline).Assembly.GetName().Name;
            _otherDllPath = layout.DllPath(otherAssemblyName);
            _otherPdbPath = layout.PdbPath(otherAssemblyName);

            HotReloadPdbCompilationInfo target = HotReloadPdbCompilationInfo.Read(_targetPdbPath);
            Assert.That(target.DocumentCount, Is.EqualTo(target.SourceFileCount), "Precondition: the compiled test assembly's PDB must list every source.");
            HotReloadPdbCompilationInfo other = HotReloadPdbCompilationInfo.Read(_otherPdbPath);
            Assert.That(other.DocumentCount, Is.EqualTo(other.SourceFileCount), "Precondition: the other assembly's PDB must list every source, so a guard that let its files through would return documents.");

            _directory = Path.Combine(Path.GetTempPath(), "uloop-compiler-output-documents-" + Guid.NewGuid().ToString("N"));
            _scriptAssembliesDllPath = Path.Combine(
                _directory, HotReloadConstants.ScriptAssembliesRelativeDirectory, TargetAssemblyName + ".dll");
            Directory.CreateDirectory(Path.GetDirectoryName(_scriptAssembliesDllPath));
            File.Copy(_targetDllPath, _scriptAssembliesDllPath);
            _artifactsDirectory = Path.Combine(_directory, "Library", "Bee", "artifacts");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        /// <summary>
        /// What: without a Library/Bee/artifacts folder beside the dll's Library there is nothing to read.
        /// </summary>
        [Test]
        public void TryReadEveryDocument_NoBeeFolder_ReturnsNull()
        {
            Assert.That(HotReloadCompilerOutputDocuments.TryReadEveryDocument(_scriptAssembliesDllPath), Is.Null);
        }

        /// <summary>
        /// What: a dag that holds no dll of the assembly's name gives null.
        /// </summary>
        [Test]
        public void TryReadEveryDocument_NoCompilerDllOfThatName_ReturnsNull()
        {
            Dag("test.dag");

            Assert.That(HotReloadCompilerOutputDocuments.TryReadEveryDocument(_scriptAssembliesDllPath), Is.Null);
        }

        /// <summary>
        /// What: a compiler dll of the same name but another MVID, with a complete PDB of its own, is not used.
        /// </summary>
        [Test]
        public void TryReadEveryDocument_CompilerDllOfAnotherBuild_ReturnsNull()
        {
            PutOtherBuild(Dag("test.dag"));

            Assert.That(HotReloadCompilerOutputDocuments.TryReadEveryDocument(_scriptAssembliesDllPath), Is.Null);
        }

        /// <summary>
        /// What: a compiler dll with the same MVID but no PDB beside it gives null.
        /// </summary>
        [Test]
        public void TryReadEveryDocument_NoCompilerPdb_ReturnsNull()
        {
            File.Copy(_targetDllPath, Path.Combine(Dag("test.dag"), TargetAssemblyName + ".dll"));

            Assert.That(HotReloadCompilerOutputDocuments.TryReadEveryDocument(_scriptAssembliesDllPath), Is.Null);
        }

        /// <summary>
        /// What: a compiler dll with the same MVID whose PDB belongs to another dll gives null instead of throwing.
        /// </summary>
        [Test]
        public void TryReadEveryDocument_CompilerPdbOfAnotherDll_ReturnsNull()
        {
            string dag = Dag("test.dag");
            File.Copy(_targetDllPath, Path.Combine(dag, TargetAssemblyName + ".dll"));
            File.Copy(_otherPdbPath, Path.Combine(dag, TargetAssemblyName + ".pdb"));

            Assert.That(HotReloadCompilerOutputDocuments.TryReadEveryDocument(_scriptAssembliesDllPath), Is.Null);
        }

        /// <summary>
        /// What: a compiler dll with the same MVID whose own PDB also lost documents gives null.
        /// </summary>
        [Test]
        public void TryReadEveryDocument_CompilerPdbAlsoLostDocuments_ReturnsNull()
        {
            string compilerDllPath = Path.Combine(Dag("test.dag"), TargetAssemblyName + ".dll");
            CecilPdbRewrite.Rewrite(_projectRoot, TargetAssemblyName, Path.Combine(_directory, "Work"), compilerDllPath);
            Assert.That(
                HotReloadAssemblyMetadataMvid.Read(compilerDllPath),
                Is.EqualTo(HotReloadAssemblyMetadataMvid.Read(_targetDllPath)),
                "Precondition: the rewrite must keep the MVID.");
            HotReloadPdbCompilationInfo rewritten = HotReloadPdbCompilationInfo.Read(Path.ChangeExtension(compilerDllPath, ".pdb"));
            Assert.That(rewritten.DocumentCount, Is.LessThan(rewritten.SourceFileCount), "Precondition: the rewrite must drop some document.");

            Assert.That(HotReloadCompilerOutputDocuments.TryReadEveryDocument(_scriptAssembliesDllPath), Is.Null);
        }

        /// <summary>
        /// What: a compiler dll with the same MVID and a complete PDB of its own gives that PDB's documents in table order.
        /// </summary>
        [Test]
        public void TryReadEveryDocument_CompilerOutputOfTheSameBuild_ReturnsEveryDocument()
        {
            PutSameBuild(Dag("test.dag"));

            List<HotReloadPdbDocument> documents = HotReloadCompilerOutputDocuments.TryReadEveryDocument(_scriptAssembliesDllPath);

            Assert.That(documents, Is.Not.Null);
            Assert.That(documents.Select(document => document.Url), Is.EqualTo(ExpectedUrls()));
        }

        /// <summary>
        /// What: when the first dag holds another build and the second the same one, the second dag's documents are returned.
        /// </summary>
        [Test]
        public void TryReadEveryDocument_SecondDagHoldsTheSameBuild_ReturnsItsDocuments()
        {
            PutOtherBuild(Dag("a.dag"));
            PutSameBuild(Dag("b.dag"));

            List<HotReloadPdbDocument> documents = HotReloadCompilerOutputDocuments.TryReadEveryDocument(_scriptAssembliesDllPath);

            Assert.That(documents, Is.Not.Null);
            Assert.That(documents.Select(document => document.Url), Is.EqualTo(ExpectedUrls()));
        }

        private string Dag(string name)
        {
            string dag = Path.Combine(_artifactsDirectory, name);
            Directory.CreateDirectory(dag);
            return dag;
        }

        private void PutSameBuild(string dag)
        {
            File.Copy(_targetDllPath, Path.Combine(dag, TargetAssemblyName + ".dll"));
            File.Copy(_targetPdbPath, Path.Combine(dag, TargetAssemblyName + ".pdb"));
        }

        private void PutOtherBuild(string dag)
        {
            File.Copy(_otherDllPath, Path.Combine(dag, TargetAssemblyName + ".dll"));
            File.Copy(_otherPdbPath, Path.Combine(dag, TargetAssemblyName + ".pdb"));
        }

        private IEnumerable<string> ExpectedUrls()
        {
            return HotReloadPdbDocumentTable.Read(_targetDllPath, _targetPdbPath, out int? _)
                .Select(document => document.Url);
        }
    }
}
