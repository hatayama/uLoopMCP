using NUnit.Framework;

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
    }
}
