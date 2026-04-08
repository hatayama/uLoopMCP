using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor.Compilation;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    [TestFixture]
    public class CompiledAssemblyBuilderTests
    {
        [Test]
        public void CreateUniqueCompilationName_WhenClassNameContainsPathCharacters_ShouldSanitizeFileName()
        {
            string compilationName = CompiledAssemblyBuilder.CreateUniqueCompilationName(
                "Bad/Name:With\\Separators",
                42);

            Assert.That(compilationName, Does.EndWith("_42"));
            Assert.That(compilationName, Does.Not.Contain("/"));
            Assert.That(compilationName, Does.Not.Contain("\\"));
            Assert.That(compilationName, Does.Not.Contain(":"));
        }

        [Test]
        public void AwaitBuildCompletionAsync_WhenCancellationIsRequested_ShouldCancelPromptly()
        {
            TaskCompletionSource<CompilerMessage[]> buildTaskCompletionSource =
                new TaskCompletionSource<CompilerMessage[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            Task<CompilerMessage[]> waitTask = AssemblyBuilderFallbackCompilerBackend.AwaitBuildCompletionAsync(
                buildTaskCompletionSource.Task,
                cancellationTokenSource.Token);

            cancellationTokenSource.Cancel();

            Assert.That(async () => await waitTask, Throws.InstanceOf<TaskCanceledException>());
        }
    }
}
