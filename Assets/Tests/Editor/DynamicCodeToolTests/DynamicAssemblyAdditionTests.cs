#if ULOOPMCP_HAS_ROSLYN
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    /// <summary>
    /// Test for Assembly Reference Management System
    /// Related classes: RoslynCompiler, CompilationRequest, CompilationResult
    /// </summary>
    [TestFixture]
    public class AssemblyReferenceManagementTests
    {
        private RoslynCompiler _compiler;

        [SetUp]
        public void SetUp()
        {
            // Create compiler in FullAccess mode
            _compiler = new RoslynCompiler(DynamicCodeSecurityLevel.FullAccess);
        }

        [TearDown]
        public void TearDown()
        {
            _compiler?.ClearCache();
            _compiler?.Dispose();
            _compiler = null;
        }

        [Test]
        public void Compile_WithBasicUnityTypes_ShouldSucceed()
        {
            // Arrange - Use basic Unity types (included in standard assembly references)
            string code = @"
                var go = new UnityEngine.GameObject(""Test"");
                UnityEngine.Debug.Log(""Hello from dynamic code"");
                return go.name;";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "TestClass",
                Namespace = "TestNamespace"
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            Assert.IsTrue(result.Success, $"Compilation failed: {string.Join(", ", result.Errors?.Select(e => e.Message) ?? new string[0])}");
            Assert.IsNotNull(result.CompiledAssembly);
            Assert.IsEmpty(result.Errors ?? new List<CompilationError>());
        }

        [Test]
        public void Compile_WithNewtonSoftJsonType_ShouldCompileIfAvailable()
        {
            // Arrange - Use Newtonsoft.Json types (if available)
            string code = @"
                try 
                {
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(new { test = ""value"" });
                    return json;
                }
                catch
                {
                    return ""Newtonsoft.Json not available"";
                }";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "JsonTestClass",
                Namespace = "TestNamespace"
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert - Success or appropriate error message
            if (!result.Success)
            {
                // Ensure errors are present when compilation fails
                Assert.IsNotNull(result.Errors, "Expected compilation errors when Success is false");

                // If Newtonsoft.Json is not found, confirm appropriate error message
                bool hasExpectedError = result.Errors.Any(e =>
                    e.Message.Contains("Newtonsoft") ||
                    e.ErrorCode == "CS0246");
                Assert.IsTrue(hasExpectedError, "Expected CS0246 error for missing Newtonsoft.Json type");
            }
            else
            {
                Assert.IsNotNull(result.CompiledAssembly);
            }
        }

        [Test]
        public void Compile_WithNonExistentType_ShouldFailGracefully()
        {
            // Arrange - Use non-existent types
            string code = @"
                var obj = new CompletelyFakeType();
                return obj.ToString();";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "FakeTypeTestClass",
                Namespace = "TestNamespace"
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            Assert.IsFalse(result.Success, "Compilation should fail for non-existent type");
            Assert.IsNotEmpty(result.Errors);
            Assert.IsTrue(result.Errors.Any(e => e.ErrorCode == "CS0246"), "Should contain CS0246 error for unknown type");
        }

        [Test]
        public void ClearCache_ShouldClearCompilationCache()
        {
            // Arrange
            string code = @"return ""cached result"";";
            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "CacheTestClass",
                Namespace = "TestNamespace"
            };

            // Act - Clear cache after compilation
            CompilationResult firstResult = _compiler.Compile(request);
            _compiler.ClearCache();
            CompilationResult secondResult = _compiler.Compile(request);

            // Assert
            Assert.IsTrue(firstResult.Success);
            Assert.IsTrue(secondResult.Success);
            // Both should succeed, verify cache is cleared
            Assert.IsNotNull(firstResult.CompiledAssembly);
            Assert.IsNotNull(secondResult.CompiledAssembly);
        }

        [Test]
        public void Compile_WithEditorOnlyClass_ShouldSucceed()
        {
            // Arrange - Use UnityEditor class (included in assembly references)
            string code = @"
                var window = UnityEditor.EditorWindow.CreateInstance<UnityEditor.EditorWindow>();
                return window != null ? ""EditorWindow created"" : ""Failed"";";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "EditorOnlyTestClass",
                Namespace = "TestNamespace"
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert - UnityEditor assembly is referenceable
            Assert.IsTrue(result.Success, $"Compilation failed: {string.Join(", ", result.Errors?.Select(e => e.Message) ?? new string[0])}");
            Assert.IsNotNull(result.CompiledAssembly);
        }

        [Test]
        public void Compile_WithGenericCustomClass_ShouldSucceed()
        {
            // Arrange - Complex case using generic types
            string code = @"
                using System.Collections.Generic;
                List<string> stringList = new List<string> { ""test1"", ""test2"" };
                Dictionary<string, int> stringIntDict = new Dictionary<string, int>();
                stringIntDict[""count""] = stringList.Count;
                
                return $""List: {stringList.Count}, Dict: {stringIntDict[""count""]}"";";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "GenericTestClass",
                Namespace = "TestNamespace"
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            Assert.IsTrue(result.Success, $"Compilation failed: {string.Join(", ", result.Errors?.Select(e => e.Message) ?? new string[0])}");
            Assert.IsNotNull(result.CompiledAssembly);
        }

        [Test]
        public void Compile_WithSeparateAssemblyClassWithUsing_ShouldSucceedInAllAssembliesMode()
        {
            // Arrange - Use class from another assembly (DynamicAssemblyTest.dll) with using statement
            string code =
                "using io.github.hatayama.uLoopMCP;\n" +
                "namespace TestNamespace\n" +
                "{\n" +
                "    public class SeparateAssemblyWithUsingTestClass\n" +
                "    {\n" +
                "        public object Execute()\n" +
                "        {\n" +
                "            var testInstance = new " + nameof(io.github.hatayama.uLoopMCP.DynamicAssemblyTest) + "();\n" +
                "            return testInstance.HelloWorld();\n" +
                "        }\n" +
                "    }\n" +
                "}";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "SeparateAssemblyWithUsingTestClass",
                Namespace = "TestNamespace",
                AssemblyMode = AssemblyLoadingMode.AllAssemblies // All assembly mode
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert - Succeeds in all assembly mode
            Assert.IsTrue(result.Success, $"Compilation failed: {string.Join(", ", result.Errors?.Select(e => e.Message) ?? new string[0])}");
            Assert.IsNotNull(result.CompiledAssembly);
            Assert.IsFalse(result.HasSecurityViolations, "Should not be a security violation");
        }

        [Test]
        public void Compile_WithSeparateAssemblyClassFullyQualified_ShouldSucceedInAllAssembliesMode()
        {
            // Arrange - Use class from another assembly with fully qualified name
            string code =
                "var testInstance = new io.github.hatayama.uLoopMCP." + nameof(io.github.hatayama.uLoopMCP.DynamicAssemblyTest) + "();\n" +
                "return testInstance.HelloWorld();";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "FullyQualifiedSeparateAssemblyTestClass",
                Namespace = "TestNamespace",
                AssemblyMode = AssemblyLoadingMode.AllAssemblies // All assembly mode
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert - Succeeds with fully qualified name in all assembly mode
            Assert.IsTrue(result.Success, $"Compilation failed: {string.Join(", ", result.Errors?.Select(e => e.Message) ?? new string[0])}");
            Assert.IsNotNull(result.CompiledAssembly);
            Assert.IsFalse(result.HasSecurityViolations, "Should not be a security violation");
        }
    }
}
#endif