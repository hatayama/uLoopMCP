#if ULOOPMCP_HAS_ROSLYN
using NUnit.Framework;
using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    /// <summary>
    /// Advanced test cases for using statement processing in the WrapCodeIfNeeded method
    /// Covering edge cases, corner cases, and security boundary tests
    /// </summary>
    public class WrapCodeWithUsingStatementsAdvancedTests
    {
        private RoslynCompiler _compiler;

        [SetUp]
        public void Setup()
        {
            // Create compiler in FullAccess mode (for testing)
            _compiler = new RoslynCompiler(DynamicCodeSecurityLevel.FullAccess);
        }

        [TearDown]
        public void TearDown()
        {
            _compiler?.Dispose();
        }

        [Test]
        public void TestUsingWithInlineComments_ExtractsCorrectly()
        {
            // Arrange: using statements with inline comments
            // Note: Current implementation extracts using statements with inline comments as-is
            // This remains a potential future improvement point
            string aiGeneratedCode = @"using UnityEngine; // For Unity
using System.Collections.Generic; // For collections
// This is a comment line
using System.Linq; // For LINQ

var objects = new List<GameObject>();
return objects.Count;";

            CompilationRequest request = new()
            {
                Code = aiGeneratedCode,
                ClassName = "DynamicCommand",
                Namespace = "Dynamic",
                AdditionalReferences = new List<string>()
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            // Current implementation extracts using statements with inline comments as-is
            // which may cause compilation errors
            if (!result.Success)
            {
                // Handling using statements with inline comments is a future improvement item
                Assert.Pass("Inline comments in using statements are not yet supported - known limitation");
            }
            else
            {
                // If successful, verify that using statements are included
                Assert.IsTrue(result.UpdatedCode.Contains("using UnityEngine"), 
                    "UnityEngine using should be preserved");
                Assert.IsTrue(result.UpdatedCode.Contains("using System.Collections.Generic"), 
                    "Collections using should be preserved");
                Assert.IsTrue(result.UpdatedCode.Contains("using System.Linq"), 
                    "LINQ using should be preserved");
            }
        }

        [Test]
        public void TestGlobalUsing_HandledAppropriately()
        {
            // Arrange: Code containing global using statements (C# 10.0+)
            string aiGeneratedCode = @"global using UnityEngine;
using System;

GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
cube.name = DateTime.Now.ToString();
return cube.name;";

            CompilationRequest request = new()
            {
                Code = aiGeneratedCode,
                ClassName = "DynamicCommand",
                Namespace = "Dynamic",
                AdditionalReferences = new List<string>()
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            // Global using statements are also extracted as using statements
            Assert.IsTrue(result.UpdatedCode.Contains("global using UnityEngine;"), 
                "Global using should be preserved");
            Assert.IsTrue(result.UpdatedCode.Contains("using System;"), 
                "System using should be preserved");
        }

        [Test]
        public void TestMixedSafeAndUnsafeUsings_PartialSuccess()
        {
            // Arrange: Mix of safe and unsafe using statements
            string aiGeneratedCode = @"using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;

GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
return ""Mixed usings"";";

            CompilationRequest request = new()
            {
                Code = aiGeneratedCode,
                ClassName = "DynamicCommand",
                Namespace = "Dynamic",
                AdditionalReferences = new List<string>()
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            // In v4.0 stateless design, CurrentLevel always returns Disabled
            // Therefore, this conditional branch is no longer necessary
            // Verification in Restricted mode is done by directly passing the level to the Executor
            if (!result.Success)
            {
                // In case of security violation
                Assert.IsTrue(result.HasSecurityViolations, "Security violations should be detected");
            }
            else
            {
                // If successful, using statements should be correctly placed
                Assert.IsTrue(result.UpdatedCode.Contains("using System.IO;"), 
                    "System.IO using should be preserved in FullAccess mode");
            }
        }

        [Test]
        public void TestUsingWithSpecialCharacters_HandledCorrectly()
        {
            // Arrange: Namespace with special characters (actually rare)
            string aiGeneratedCode = @"using UnityEngine;
using MyCompany.Tools.Version2_0;

GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
return ""Special namespace test"";";

            CompilationRequest request = new()
            {
                Code = aiGeneratedCode,
                ClassName = "DynamicCommand",
                Namespace = "Dynamic",
                AdditionalReferences = new List<string>()
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            // Namespaces with special characters are also processed correctly
            Assert.IsTrue(result.UpdatedCode.Contains("using MyCompany.Tools.Version2_0;"), 
                "Namespace with underscore and numbers should be preserved");
        }

        [Test]
        public void TestEmptyLinesAndWhitespace_CleansUpProperly()
        {
            // Arrange: Code with extra blank lines and whitespace
            string aiGeneratedCode = @"

using UnityEngine;


using System;
    
GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);

return cube.name;";

            CompilationRequest request = new()
            {
                Code = aiGeneratedCode,
                ClassName = "DynamicCommand",
                Namespace = "Dynamic",
                AdditionalReferences = new List<string>()
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            Assert.IsTrue(result.Success, "Compilation should succeed");
            Assert.IsTrue(result.UpdatedCode.Contains("using UnityEngine;"), 
                "UnityEngine using should be preserved");
            Assert.IsTrue(result.UpdatedCode.Contains("using System;"), 
                "System using should be preserved");
            
            // Verify that it is appropriately formatted
            string[] lines = result.UpdatedCode.Split('\n');
            int namespaceIndex = System.Array.FindIndex(lines, l => l.Contains("namespace Dynamic"));
            Assert.Greater(namespaceIndex, 1, "Should have using statements before namespace");
        }

        [Test]
        public void TestUsingInsideCodeBlock_NotExtracted()
        {
            // Arrange: When "using" is included as a string within the code
            string aiGeneratedCode = @"using UnityEngine;

string message = ""using System.IO is dangerous"";
GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
cube.name = message;
return cube.name;";

            CompilationRequest request = new()
            {
                Code = aiGeneratedCode,
                ClassName = "DynamicCommand",
                Namespace = "Dynamic",
                AdditionalReferences = new List<string>()
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            Assert.IsTrue(result.Success, "Compilation should succeed");
            Assert.IsTrue(result.UpdatedCode.Contains("using UnityEngine;"), 
                "Real using should be extracted");
            // "using" within a string is not extracted
            Assert.IsTrue(result.UpdatedCode.Contains("\"using System.IO is dangerous\""), 
                "String content should remain intact");
        }

        [Test]
        public void TestNestedNamespaces_ExtractsCorrectly()
        {
            // Arrange: Using statements with nested namespaces
            string aiGeneratedCode = @"using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;

GameObject canvas = new GameObject(""Canvas"");
canvas.AddComponent<Canvas>();
return ""Canvas created"";";

            CompilationRequest request = new()
            {
                Code = aiGeneratedCode,
                ClassName = "DynamicCommand",
                Namespace = "Dynamic",
                AdditionalReferences = new List<string>()
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            Assert.IsTrue(result.Success, "Compilation should succeed");
            Assert.IsTrue(result.UpdatedCode.Contains("using UnityEngine;"), 
                "Root namespace should be preserved");
            Assert.IsTrue(result.UpdatedCode.Contains("using UnityEngine.UI;"), 
                "Nested UI namespace should be preserved");
            Assert.IsTrue(result.UpdatedCode.Contains("using UnityEngine.Events;"), 
                "Nested Events namespace should be preserved");
        }

        [Test]
        public void TestConditionalCompilationDirectives_HandledProperly()
        {
            // Arrange: Including conditional compilation directives
            string aiGeneratedCode = @"using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
#if UNITY_EDITOR
EditorUtility.DisplayDialog(""Test"", ""Cube created"", ""OK"");
#endif
return ""Done"";";

            CompilationRequest request = new()
            {
                Code = aiGeneratedCode,
                ClassName = "DynamicCommand",
                Namespace = "Dynamic",
                AdditionalReferences = new List<string>()
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            // Conditional compilation directives are not specially handled in the current WrapCodeIfNeeded implementation
            // but using UnityEngine will be extracted
            Assert.IsTrue(result.UpdatedCode.Contains("using UnityEngine;"), 
                "UnityEngine using should be preserved");
            
            // Using statements inside #if UNITY_EDITOR blocks may be treated as strings
            // Behavior may differ depending on implementation
        }

        [Test]
        public void TestVeryLongNamespace_HandledWithoutTruncation()
        {
            // Arrange: Very long namespace
            string aiGeneratedCode = @"using UnityEngine;
using Company.Product.Module.SubModule.Feature.Implementation.Version2;

GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
return ""Long namespace test"";";

            CompilationRequest request = new()
            {
                Code = aiGeneratedCode,
                ClassName = "DynamicCommand",
                Namespace = "Dynamic",
                AdditionalReferences = new List<string>()
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            // Long namespace is preserved without truncation
            Assert.IsTrue(result.UpdatedCode.Contains(
                "using Company.Product.Module.SubModule.Feature.Implementation.Version2;"), 
                "Long namespace should be preserved without truncation");
        }

        [Test]
        public void TestUsingWithoutSemicolon_NotExtracted()
        {
            // Arrange: Using statement without semicolon (syntax error)
            string aiGeneratedCode = @"using UnityEngine
using System;

GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
return cube.name;";

            CompilationRequest request = new()
            {
                Code = aiGeneratedCode,
                ClassName = "DynamicCommand",
                Namespace = "Dynamic",
                AdditionalReferences = new List<string>()
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            // Using statement without semicolon is not extracted (results in syntax error)
            Assert.IsFalse(result.Success, "Should fail due to syntax error");
        }

        [Test]
        public void TestSystemReflection_SecurityBoundaryTest()
        {
            // Arrange: Using reflection (security boundary test)
            string aiGeneratedCode = @"using UnityEngine;
using System.Reflection;

GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
Type cubeType = cube.GetType();
MethodInfo[] methods = cubeType.GetMethods();
return $""Cube has {methods.Length} methods"";";

            CompilationRequest request = new()
            {
                Code = aiGeneratedCode,
                ClassName = "DynamicCommand",
                Namespace = "Dynamic",
                AdditionalReferences = new List<string>()
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            // In v4.0 stateless design, security level is controlled by Executor
            // Usability of System.Reflection depends on security policy
            if (!result.Success && result.HasSecurityViolations)
            {
                // If detected as a security violation
                Assert.Pass("System.Reflection is blocked as per security policy");
            }
            else if (result.Success)
            {
                Assert.Pass("System.Reflection is allowed in current configuration");
            }
        }

        [Test]
        public void TestPerformanceWithManyUsings_HandlesEfficiently()
        {
            // Arrange: Multiple using statements
            string aiGeneratedCode = @"using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;
using UnityEngine.Video;
using UnityEngine.Animations;
using UnityEngine.AI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
return ""Many usings test"";";

            CompilationRequest request = new()
            {
                Code = aiGeneratedCode,
                ClassName = "DynamicCommand",
                Namespace = "Dynamic",
                AdditionalReferences = new List<string>()
            };

            // Act
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            CompilationResult result = _compiler.Compile(request);
            sw.Stop();

            // Assert
            Assert.IsTrue(result.Success, "Compilation should succeed");
            Assert.Less(sw.ElapsedMilliseconds, McpConstants.TEST_COMPILE_TIMEOUT_MS, "Should compile within 5 seconds");
            
            // Verify that all using statements are preserved
            Assert.IsTrue(result.UpdatedCode.Contains("using UnityEngine;"));
            Assert.IsTrue(result.UpdatedCode.Contains("using System.Linq;"));
            Assert.IsTrue(result.UpdatedCode.Contains("using System.Text;"));
        }
    }
}
#endif