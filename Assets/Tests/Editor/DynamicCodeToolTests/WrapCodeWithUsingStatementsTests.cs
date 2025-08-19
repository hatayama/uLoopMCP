#if ULOOPMCP_HAS_ROSLYN
using NUnit.Framework;
using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    /// <summary>
    /// WrapCodeIfNeededメソッドのusing文処理テスト
    /// AI生成コードのusing文を適切に抽出・配置する機能のテスト
    /// </summary>
    public class WrapCodeWithUsingStatementsTests
    {
        private RoslynCompiler _compiler;

        [SetUp]
        public void Setup()
        {
            _compiler = new RoslynCompiler();
        }

        [TearDown]
        public void TearDown()
        {
            _compiler?.Dispose();
        }

        [Test]
        public void TestSimpleCodeWithUsing_ExtractsAndRelocatesUsing()
        {
            // Arrange: AIが書いたコード（using文付き）
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
            
            // ラップされたコードにusing UnityEngine;が含まれることを確認
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
            // Arrange: 複数のusing文を含むコード
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
            
            // 全てのusing文が保持されていることを確認
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
            // Arrange: using文なしのコード（完全修飾名使用）
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
        public void TestRestrictedMode_DetectsDangerousUsing()
        {
            // このテストはRestrictedモードでの動作を確認
            // 注: CurrentLevelは読み取り専用のため、現在のセキュリティレベルに依存
            DynamicCodeSecurityLevel originalLevel = DynamicCodeSecurityManager.CurrentLevel;
            
            if (originalLevel == DynamicCodeSecurityLevel.Restricted)
            {
                // Arrange: 危険なusing文を含むコード
                string aiGeneratedCode = @"using System.IO;
File.WriteAllText(""test.txt"", ""dangerous"");
return ""File written"";";

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
                Assert.IsFalse(result.Success, "Compilation should fail due to security violation");
                Assert.IsTrue(result.HasSecurityViolations, "Security violations should be detected");
                Assert.AreEqual(CompilationFailureReason.SecurityViolation, result.FailureReason);
            }
            else
            {
                // Restrictedモードでない場合はテストをスキップ
                Assert.Ignore($"Test requires Restricted mode, but current level is {originalLevel}");
            }
        }

        [Test]
        public void TestMixedContent_CorrectlySeparatesUsingFromCode()
        {
            // Arrange: using文とコメント、空行が混在
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
            
            // using文が適切に抽出されていることを確認
            Assert.IsTrue(result.UpdatedCode.Contains("using UnityEngine;"), 
                "UnityEngine using should be preserved");
            Assert.IsTrue(result.UpdatedCode.Contains("using System;"), 
                "System using should be preserved");
            
            // コメントは除外されていることを確認（using文以外として扱われる）
            string[] lines = result.UpdatedCode.Split('\n');
            int namespaceIndex = System.Array.FindIndex(lines, l => l.Contains("namespace Dynamic"));
            Assert.Greater(namespaceIndex, 0, "Namespace should come after using statements");
        }

        [Test]
        public void TestUsingStaticAndAlias_HandledCorrectly()
        {
            // Arrange: using static と using alias を含むコード
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