#if ULOOPMCP_HAS_ROSLYN
using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    /// <summary>
    /// Tests related to ExecuteDynamicCodeTool in Restricted mode
    /// Aggregates tests for detecting security violations
    /// </summary>
    [TestFixture]
    public class ExecuteDynamicCodeSecurityTests
    {
        private RoslynCompiler _compiler;

        [SetUp]
        public void SetUp()
        {
            // Create compiler in Restricted mode (enable security verification)
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
        public void Compile_WithDynamicAssemblyTest_SafeMethods_ShouldSucceed()
        {
            // Arrange - Use safe method of DynamicAssemblyTest
            string code =
                "var test = new io.github.hatayama.uLoopMCP." + nameof(io.github.hatayama.uLoopMCP.DynamicAssemblyTest) + "();\n" +
                "string hello = test.HelloWorld();\n" +
                "int sum = test.Add(5, 3);\n" +
                "string assembly = test.GetAssemblyName();\n" +
                "return $\"{hello}, Sum: {sum}, Assembly: {assembly}\";";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "DynamicAssemblyTestClass",
                Namespace = "TestNamespace"
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            Assert.IsTrue(result.Success, "Safe methods should compile successfully");
            Assert.IsFalse(result.HasSecurityViolations, "Should not have security violations");
        }

        [Test]
        public void Compile_WithDynamicAssemblyTest_ExecuteAnoterInstanceMethod_ShouldSucceed()
        {
            // Arrange
            // ExecuteAnotherInstanceMethod is a method from another assembly,
            // so it cannot be inspected during compilation and will succeed
            string code =
                "var test = new io.github.hatayama.uLoopMCP." + nameof(io.github.hatayama.uLoopMCP.DynamicAssemblyTest) + "();\n" +
                "test.ExecuteAnoterInstanceMethod();\n" +
                "return \"Compiled\";";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "DynamicAssemblyIndirectTestClass",
                Namespace = "TestNamespace"
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            // Cannot inspect method contents from another assembly, so compilation succeeds
            Assert.IsTrue(result.Success, "Should compile successfully as method content is not inspected");
            Assert.IsFalse(result.HasSecurityViolations, "Should not detect violations in external assembly methods");
        }

        [Test]
        public void Compile_WithForDynamicAssemblyTest_DirectDangerousCall_ShouldSucceed()
        {
            // Arrange  
            // Method call for DynamicAssemblyTest itself cannot be detected
            string code = @"
                var forTest = new io.github.hatayama.uLoopMCP.ForDynamicAssemblyTest();
                return forTest.TestForbiddenOperationsInAnotherDLL();";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "ForDynamicAssemblyTestClass",
                Namespace = "TestNamespace"
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            // Method calls from another assembly cannot be inspected to their full content
            Assert.IsTrue(result.Success, "External assembly method calls compile successfully");
            Assert.IsFalse(result.HasSecurityViolations, "Cannot detect violations inside external methods");
        }
    }
}
#endif