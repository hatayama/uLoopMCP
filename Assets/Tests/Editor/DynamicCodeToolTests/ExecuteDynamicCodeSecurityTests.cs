#if ULOOPMCP_HAS_ROSLYN
using NUnit.Framework;
using System.Linq;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    /// <summary>
    /// Dummy type defined in the test assembly (*.Tests.dll).
    /// Used to verify that, in Restricted mode, dynamic compilation can
    /// reference types from test assemblies (ScriptAssemblies) as intended.
    /// </summary>
    public class EditorTestHelperType
    {
        public static string Ping()
        {
            return "pong";
        }
    }

    /// <summary>
    /// Tests related to ExecuteDynamicCodeTool in Restricted mode
    /// Aggregates tests for detecting security violations
    /// </summary>
    [TestFixture]
    public class ExecuteDynamicCodeSecurityTests
    {
        private RoslynCompiler _compiler;

        // Ensure compile-time reference to the test-assembly type used by dynamic code
        private static readonly string ForceReferenceToEditorTestHelperType =
            nameof(EditorTestHelperType) + "." + nameof(EditorTestHelperType.Ping);

        static ExecuteDynamicCodeSecurityTests()
        {
            var _ = ForceReferenceToEditorTestHelperType;
        }

        private void AssertSuccess(CompilationResult result)
        {
            if (!result.Success)
            {
                string details = string.Join("\n", (result.Errors ?? new System.Collections.Generic.List<CompilationError>())
                    .Select(e => $"[{e.ErrorCode}] L{e.Line},{e.Column} {e.Message}"));
                Assert.Fail($"Compilation failed. Errors:\n{details}");
            }
        }

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
            AssertSuccess(result);
            Assert.IsFalse(result.HasSecurityViolations, "Should not have security violations");
        }

        [Test]
        public void Compile_Restricted_CanReference_TestAssemblyType_ShouldSucceed()
        {
            // Arrange - reference type defined in test assembly (*.Tests.dll)
            string code =
                "string ping = io.github.hatayama.uLoopMCP.DynamicCodeToolTests.EditorTestHelperType.Ping();\n" +
                "return ping;";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "RefTestsAsmClass",
                Namespace = "TestNamespace"
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            AssertSuccess(result);
            Assert.IsFalse(result.HasSecurityViolations, "No security violations expected");
        }

        [Test]
        public void Compile_Restricted_CanReference_UnityPackageType_ShouldSucceed_IfPresent()
        {
            // Arrange - try referencing a commonly available package type
            // Fallback to UnityEngine.Vector3 if package type isn't available
            string code =
                "#if UNITY_MATHEMATICS\n" +
                "var v = new Unity.Mathematics.float3(1f, 2f, 3f);\n" +
                "float sum = v.x + v.y + v.z;\n" +
                "return sum;\n" +
                "#else\n" +
                "var v = new UnityEngine.Vector3(1f, 2f, 3f);\n" +
                "float sum = v.x + v.y + v.z;\n" +
                "return sum;\n" +
                "#endif\n";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "RefPkgClass",
                Namespace = "TestNamespace"
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            AssertSuccess(result);
            Assert.IsFalse(result.HasSecurityViolations, "No security violations expected");
        }

        [Test]
        public void Compile_Restricted_CanReference_Editor_And_Engine_Types_ShouldSucceed()
        {
            // Arrange - mix UnityEditor and UnityEngine APIs
            string code =
                "double t = UnityEditor.EditorApplication.timeSinceStartup;\n" +
                "var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();\n" +
                "return scene.name;";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "RefEditorEngineClass",
                Namespace = "TestNamespace"
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            AssertSuccess(result);
            Assert.IsFalse(result.HasSecurityViolations, "No security violations expected");
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
            AssertSuccess(result);
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
            AssertSuccess(result);
            Assert.IsFalse(result.HasSecurityViolations, "Cannot detect violations inside external methods");
        }
    }
}
#endif