using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEditor.Compilation;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    [TestFixture]
    public class SharedRoslynCompilerWorkerHostTests
    {
        private string _tempDirectoryPath;

        [SetUp]
        public void SetUp()
        {
            _tempDirectoryPath = Path.Combine(Path.GetTempPath(), $"SharedRoslynCompilerWorkerHostTests_{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDirectoryPath);
        }

        [TearDown]
        public void TearDown()
        {
            SharedRoslynCompilerWorkerHost.ShutdownForTests();

            if (Directory.Exists(_tempDirectoryPath))
            {
                Directory.Delete(_tempDirectoryPath, true);
            }
        }

        [Test]
        public void TryCompile_WhenExternalCompilerIsAvailable_ShouldBuildAssembly()
        {
            ExternalCompilerPaths externalCompilerPaths = ExternalCompilerPathResolver.Resolve();
            Assert.That(externalCompilerPaths, Is.Not.Null, "Unity external compiler layout should be available for this test");

            DynamicReferenceSetBuilderService referenceSetBuilder = new DynamicReferenceSetBuilderService();
            List<string> references = referenceSetBuilder.BuildReferenceSet(
                new List<string>(),
                null,
                externalCompilerPaths);

            string sourcePath = Path.Combine(_tempDirectoryPath, "WorkerSmokeTest.cs");
            string dllPath = Path.Combine(_tempDirectoryPath, "WorkerSmokeTest.dll");
            string requestFilePath = Path.Combine(_tempDirectoryPath, "WorkerSmokeTest.worker");

            File.WriteAllText(
                sourcePath,
                "public static class WorkerSmokeTest { public static int Execute() { return 3; } }");
            File.WriteAllLines(
                requestFilePath,
                new[] { Path.GetFullPath(sourcePath), Path.GetFullPath(dllPath) }
                    .Concat(references.Select(Path.GetFullPath)));

            int buildCount = 0;
            bool buildStarted = false;
            bool buildFinished = false;

            CompilerMessage[] messages = SharedRoslynCompilerWorkerHost.TryCompile(
                requestFilePath,
                externalCompilerPaths,
                CancellationToken.None,
                () => buildStarted = true,
                () => buildFinished = true,
                () => buildCount++);

            Assert.That(messages, Is.Not.Null, "Worker path should return compiler output instead of falling back");
            Assert.That(
                messages.Any(message => message.type == CompilerMessageType.Error),
                Is.False,
                string.Join("\n", messages.Select(message => message.message)));
            Assert.That(File.Exists(dllPath), Is.True, "Worker compilation should emit the target assembly");
            Assert.That(buildCount, Is.EqualTo(1));
            Assert.That(buildStarted, Is.True);
            Assert.That(buildFinished, Is.True);
        }
    }
}
