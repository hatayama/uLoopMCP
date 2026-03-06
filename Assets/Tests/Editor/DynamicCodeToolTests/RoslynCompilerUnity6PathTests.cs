#if ULOOPMCP_HAS_ROSLYN
using System.Linq;
using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    /// <summary>
    /// Tests to verify RoslynCompiler works correctly with Unity 6's new folder structure.
    /// Unity 6 moved reference assemblies from Contents/{subpath} to Contents/Resources/Scripting/{subpath}.
    /// See: https://github.com/hatayama/uLoopMCP/issues/370
    ///
    /// IMPORTANT: These tests use Restricted mode because FullAccess mode falls back to
    /// AppDomain.GetAssemblies() which masks the path resolution issue.
    /// </summary>
    [TestFixture]
    public class RoslynCompilerUnity6PathTests
    {
        private RoslynCompiler _compiler;

        [SetUp]
        public void SetUp()
        {
            // Use Restricted mode to test Unity folder path resolution
            // FullAccess mode uses AppDomain.GetAssemblies() as fallback, which masks path issues
            _compiler = new RoslynCompiler(DynamicCodeSecurityLevel.Restricted);
        }

        [TearDown]
        public void TearDown()
        {
            _compiler?.ClearCache();
            _compiler?.Dispose();
            _compiler = null;
        }

        [Test]
        public void Compile_WithSystemObject_ShouldSucceed()
        {
            // Arrange
            // System.Object is the most fundamental type in .NET
            // If Unity 6 path resolution fails, this compilation will fail with "System.Object is not defined"
            string code = @"
                System.Object obj = new System.Object();
                return obj.GetType().Name;";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "SystemObjectTestClass",
                Namespace = "TestNamespace"
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            Assert.IsTrue(result.Success,
                $"Compilation failed (System.Object not found indicates Unity path issue): " +
                $"{string.Join(", ", result.Errors?.Select(e => e.Message) ?? new string[0])}");
            Assert.IsNotNull(result.CompiledAssembly);
        }

        [Test]
        public void Compile_WithBasicTypes_ShouldSucceed()
        {
            // Arrange
            // Verify that basic types (string, int, bool) can be resolved
            string code = @"
                string text = ""Hello"";
                int number = 42;
                bool flag = true;
                return $""{text} {number} {flag}"";";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "BasicTypesTestClass",
                Namespace = "TestNamespace"
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            Assert.IsTrue(result.Success,
                $"Compilation failed: {string.Join(", ", result.Errors?.Select(e => e.Message) ?? new string[0])}");
            Assert.IsNotNull(result.CompiledAssembly);
        }

        [Test]
        public void Compile_WithCollectionTypes_ShouldSucceed()
        {
            // Arrange
            // Verify that System.Collections.Generic types can be resolved
            string code = @"
                var list = new System.Collections.Generic.List<string> { ""a"", ""b"", ""c"" };
                var dict = new System.Collections.Generic.Dictionary<string, int>();
                dict[""count""] = list.Count;
                return dict[""count""];";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "CollectionTypesTestClass",
                Namespace = "TestNamespace"
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            Assert.IsTrue(result.Success,
                $"Compilation failed: {string.Join(", ", result.Errors?.Select(e => e.Message) ?? new string[0])}");
            Assert.IsNotNull(result.CompiledAssembly);
        }

        [Test]
        public void Compile_WithLinq_ShouldSucceed()
        {
            // Arrange
            // Verify that System.Linq can be resolved (requires reference assemblies)
            string code = @"
                using System.Linq;
                var numbers = new[] { 1, 2, 3, 4, 5 };
                return numbers.Where(n => n > 2).Sum();";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "LinqTestClass",
                Namespace = "TestNamespace"
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            Assert.IsTrue(result.Success,
                $"Compilation failed: {string.Join(", ", result.Errors?.Select(e => e.Message) ?? new string[0])}");
            Assert.IsNotNull(result.CompiledAssembly);
        }
    }
}
#endif
