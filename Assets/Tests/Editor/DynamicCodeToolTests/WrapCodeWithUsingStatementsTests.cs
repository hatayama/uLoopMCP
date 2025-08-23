#if ULOOPMCP_HAS_ROSLYN
using NUnit.Framework;
using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    /// <summary>
    /// Test for WrapCodeIfNeeded method's using statement processing
    /// Test the functionality of extracting and placing using statements for AI-generated code
    /// </summary>
    public class WrapCodeWithUsingStatementsTests
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
        public void TestSimpleCodeWithUsing_ExtractsAndRelocatesUsing()
        {
            // Arrange: AI-generated code (with using statement)
            string aiGeneratedCode = @"using UnityEngine;
GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
return ""Cube created"";";

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
            Assert.IsNotNull(result.CompiledAssembly, "Assembly should be created");
            
            // Verify that the wrapped code contains using UnityEngine;
            Assert.IsTrue(result.UpdatedCode.Contains("using UnityEngine;"), 
                "Using statement should be preserved");
            Assert.IsTrue(result.UpdatedCode.Contains("namespace Dynamic"), 
                "Namespace should be added");
            Assert.IsTrue(result.UpdatedCode.Contains("public class DynamicCommand"), 
                "Class wrapper should be added");
        }

        [Test]
        public void TestMultipleUsings_ExtractsAllUsings()
        {
            // Arrange: Code with multiple using statements
            string aiGeneratedCode = @"using UnityEngine;
using System.Collections.Generic;
using System.Linq;

var objects = new List<GameObject>();
objects.Add(GameObject.CreatePrimitive(PrimitiveType.Cube));
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
            Assert.IsTrue(result.Success, "Compilation should succeed");
            
            // Verify that all using statements are preserved
            Assert.IsTrue(result.UpdatedCode.Contains("using UnityEngine;"), 
                "UnityEngine using should be preserved");
            Assert.IsTrue(result.UpdatedCode.Contains("using System.Collections.Generic;"), 
                "System.Collections.Generic using should be preserved");
            Assert.IsTrue(result.UpdatedCode.Contains("using System.Linq;"), 
                "System.Linq using should be preserved");
        }

        [Test]
        public void TestCodeWithoutUsing_WorksAsExpected()
        {
            // Arrange: Code without using statement (using fully qualified names)
            string aiGeneratedCode = @"UnityEngine.GameObject cube = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
return ""Cube created"";";

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
            Assert.IsFalse(result.UpdatedCode.StartsWith("using "), 
                "No using statements should be added when not present in original code");
        }

        [Test]
        public void TestMixedContent_CorrectlySeparatesUsingFromCode()
        {
            // Arrange: Mixture of using statements, comments, and blank lines
            string aiGeneratedCode = @"using UnityEngine;
// This is a comment
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
            Assert.IsTrue(result.Success, "Compilation should succeed");
            
            // Verify that using statements are correctly extracted
            Assert.IsTrue(result.UpdatedCode.Contains("using UnityEngine;"), 
                "UnityEngine using should be preserved");
            Assert.IsTrue(result.UpdatedCode.Contains("using System;"), 
                "System using should be preserved");
            
            // Verify that comments are excluded (treated as non-using content)
            string[] lines = result.UpdatedCode.Split('\n');
            int namespaceIndex = System.Array.FindIndex(lines, l => l.Contains("namespace Dynamic"));
            Assert.Greater(namespaceIndex, 0, "Namespace should come after using statements");
        }

        [Test]
        public void TestUsingStaticAndAlias_HandledCorrectly()
        {
            // Arrange: Code with using static and using alias
            string aiGeneratedCode = @"using UnityEngine;
using static UnityEngine.Mathf;
using GO = UnityEngine.GameObject;

GO cube = GO.CreatePrimitive(PrimitiveType.Cube);
cube.transform.position = new Vector3(Sin(0), Cos(0), 0);
return ""Created with static using"";";

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
            Assert.IsTrue(result.UpdatedCode.Contains("using static UnityEngine.Mathf;"), 
                "Using static should be preserved");
            Assert.IsTrue(result.UpdatedCode.Contains("using GO = UnityEngine.GameObject;"), 
                "Using alias should be preserved");
        }
    }
}
#endif