using System;
using System.IO;

using Mono.Cecil;
using Mono.Cecil.Cil;

using NUnit.Framework;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// Contract of telling a PDB that names no document for some top-level type, which is how a
    /// newly added interface-only or enum-only file looks in a PDB rewritten after the compile.
    /// Whether real PDBs are flagged correctly is checked across this project's assemblies in
    /// HotReloadCompilationAssemblyReconstructionCrossCheckTests.
    /// </summary>
    public sealed class HotReloadPdbTypeDocumentCoverageTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "uloop-pdb-type-documents-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_directory, true);
        }

        /// <summary>
        /// What: an interface with no document in the PDB is reported.
        /// </summary>
        [Test]
        public void HasTypeWithoutDocument_InterfaceWithoutADocument_ReturnsTrue()
        {
            string dllPath = WriteModule("Sample", "IOnlyType", TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);

            Assert.That(HotReloadPdbTypeDocumentCoverage.HasTypeWithoutDocument(dllPath, Path.ChangeExtension(dllPath, ".pdb")), Is.True);
        }

        /// <summary>
        /// What: a compiler-generated type, which has no source, is not reported.
        /// </summary>
        [Test]
        public void HasTypeWithoutDocument_OnlyACompilerGeneratedType_ReturnsFalse()
        {
            string dllPath = WriteModule("Sample", "<PrivateImplementationDetails>", TypeAttributes.NotPublic | TypeAttributes.Sealed);

            Assert.That(HotReloadPdbTypeDocumentCoverage.HasTypeWithoutDocument(dllPath, Path.ChangeExtension(dllPath, ".pdb")), Is.False);
        }

        private string WriteModule(string moduleName, string typeName, TypeAttributes attributes)
        {
            string dllPath = Path.Combine(_directory, moduleName + ".dll");
            using (ModuleDefinition module = ModuleDefinition.CreateModule(moduleName, ModuleKind.Dll))
            {
                module.Types.Add(new TypeDefinition(string.Empty, typeName, attributes));
                module.Write(dllPath, new WriterParameters
                {
                    WriteSymbols = true,
                    SymbolWriterProvider = new PortablePdbWriterProvider(),
                });
            }

            Assert.That(File.Exists(Path.ChangeExtension(dllPath, ".pdb")), Is.True, "Cecil did not write the portable PDB.");
            return dllPath;
        }
    }
}
