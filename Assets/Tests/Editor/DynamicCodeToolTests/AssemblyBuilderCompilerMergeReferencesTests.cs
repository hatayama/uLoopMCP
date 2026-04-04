using NUnit.Framework;
using System.Collections.Generic;
using System.IO;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    [TestFixture]
    public class AssemblyBuilderCompilerMergeReferencesTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"MergeRefsTest_{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Test]
        public void Should_KeepFirstOccurrence_When_SameAssemblyNameExistsInBothBaseAndAdditional()
        {
            string basePath = Path.Combine(_tempDir, "mono", "System.Threading.dll");
            string additionalPath = Path.Combine(_tempDir, "netstandard", "System.Threading.dll");
            CreateDummyFile(basePath);
            CreateDummyFile(additionalPath);

            string[] baseRefs = { basePath };
            List<string> additionalRefs = new() { additionalPath };

            string[] result = AssemblyBuilderCompiler.MergeReferencesByAssemblyName(baseRefs, additionalRefs);

            Assert.That(result, Has.Length.EqualTo(1));
            Assert.That(result[0], Is.EqualTo(basePath));
        }

        [Test]
        public void Should_IncludeBoth_When_DifferentAssemblyNames()
        {
            string threadingPath = Path.Combine(_tempDir, "System.Threading.dll");
            string debugPath = Path.Combine(_tempDir, "System.Diagnostics.Debug.dll");
            CreateDummyFile(threadingPath);
            CreateDummyFile(debugPath);

            string[] baseRefs = { threadingPath };
            List<string> additionalRefs = new() { debugPath };

            string[] result = AssemblyBuilderCompiler.MergeReferencesByAssemblyName(baseRefs, additionalRefs);

            Assert.That(result, Has.Length.EqualTo(2));
            Assert.That(result, Does.Contain(threadingPath));
            Assert.That(result, Does.Contain(debugPath));
        }

        [Test]
        public void Should_DeduplicateBaseReferences_When_SameNameAppearsMultipleTimes()
        {
            string path1 = Path.Combine(_tempDir, "dir1", "MyAssembly.dll");
            string path2 = Path.Combine(_tempDir, "dir2", "MyAssembly.dll");
            CreateDummyFile(path1);
            CreateDummyFile(path2);

            string[] baseRefs = { path1, path2 };
            List<string> additionalRefs = new();

            string[] result = AssemblyBuilderCompiler.MergeReferencesByAssemblyName(baseRefs, additionalRefs);

            Assert.That(result, Has.Length.EqualTo(1));
            Assert.That(result[0], Is.EqualTo(path1));
        }

        [Test]
        public void Should_BeCaseInsensitive_When_ComparingAssemblyNames()
        {
            string lowerPath = Path.Combine(_tempDir, "system.threading.dll");
            string upperPath = Path.Combine(_tempDir, "alt", "System.Threading.dll");
            CreateDummyFile(lowerPath);
            CreateDummyFile(upperPath);

            string[] baseRefs = { lowerPath };
            List<string> additionalRefs = new() { upperPath };

            string[] result = AssemblyBuilderCompiler.MergeReferencesByAssemblyName(baseRefs, additionalRefs);

            Assert.That(result, Has.Length.EqualTo(1));
            Assert.That(result[0], Is.EqualTo(lowerPath));
        }

        [Test]
        public void Should_SkipAdditionalRef_When_FileDoesNotExist()
        {
            string basePath = Path.Combine(_tempDir, "Existing.dll");
            CreateDummyFile(basePath);
            string nonExistentPath = Path.Combine(_tempDir, "NonExistent.dll");

            string[] baseRefs = { basePath };
            List<string> additionalRefs = new() { nonExistentPath };

            string[] result = AssemblyBuilderCompiler.MergeReferencesByAssemblyName(baseRefs, additionalRefs);

            Assert.That(result, Has.Length.EqualTo(1));
            Assert.That(result[0], Is.EqualTo(basePath));
        }

        [Test]
        public void Should_SkipAdditionalRef_When_PathIsNullOrEmpty()
        {
            string basePath = Path.Combine(_tempDir, "Valid.dll");
            CreateDummyFile(basePath);

            string[] baseRefs = { basePath };
            List<string> additionalRefs = new() { null, "", "  " };

            string[] result = AssemblyBuilderCompiler.MergeReferencesByAssemblyName(baseRefs, additionalRefs);

            Assert.That(result, Has.Length.EqualTo(1));
            Assert.That(result[0], Is.EqualTo(basePath));
        }

        private void CreateDummyFile(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, new byte[] { 0 });
        }
    }
}
