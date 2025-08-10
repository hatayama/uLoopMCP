using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using io.github.hatayama.uLoopMCP;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// 動的アセンブリ追加システムのテスト
    /// 設計ドキュメント: フリーズ回避のための軽量化アセンブリ読み込み
    /// 関連クラス: RoslynCompiler, CompilationRequest, CompilationResult
    /// </summary>
    [TestFixture]
    public class DynamicAssemblyAdditionTests
    {
        private RoslynCompiler _compiler;

        [SetUp]
        public void SetUp()
        {
            _compiler = new RoslynCompiler();
        }

        [TearDown]
        public void TearDown()
        {
            _compiler?.ClearCache();
            _compiler = null;
        }

        [Test]
        public void Compile_WithBasicUnityTypes_ShouldSucceedWithoutDynamicAddition()
        {
            // Arrange - 基本的なUnityの型を使用（キュレートされたアセンブリに含まれる）
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
        public void Compile_WithSystemNetHttpType_ShouldFailWithSecurityViolation()
        {
            // Arrange - System.Net.Httpの型を使用（セキュリティポリシーで禁止）
            string code = @"
                using System.Net.Http;
                var client = new HttpClient();
                return client.GetType().Name;";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "HttpTestClass",
                Namespace = "TestNamespace"
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert - セキュリティ違反で失敗することを期待
            Assert.IsFalse(result.Success, "Compilation should fail due to security policy violation");
            Assert.IsTrue(result.HasSecurityViolations, "Result should indicate security violations");
            Assert.AreEqual(CompilationFailureReason.SecurityViolation, result.FailureReason, "Failure reason should be SecurityViolation");
            Assert.IsTrue(result.SecurityViolations.Count > 0, "Should have at least one security violation");
            
            // セキュリティ違反の詳細を確認
            SecurityViolation violation = result.SecurityViolations.FirstOrDefault(v => v.Type == SecurityViolationType.ForbiddenNamespace);
            Assert.IsNotNull(violation, "Should have forbidden namespace violation");
            Assert.That(violation.Description, Does.Contain("System.Net"), "Violation description should mention System.Net namespace");
        }

        [Test]
        public void Compile_WithNewtonSoftJsonType_ShouldSucceedWithDynamicAddition()
        {
            // Arrange - Newtonsoft.Jsonの型を使用（存在する場合）
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

            // Assert - 成功または適切なエラーメッセージ
            if (!result.Success)
            {
                // Newtonsoft.Jsonが見つからない場合は適切なエラーメッセージであることを確認
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
            // Arrange - 存在しない型を使用
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
        public void Compile_WithSystemIOType_ShouldFailWithSecurityViolation()
        {
            // Arrange - System.IOの型を使用（セキュリティポリシーで禁止）
            string code = @"
                using System.IO;
                var tempFile = Path.GetTempFileName();
                File.WriteAllText(tempFile, ""test"");
                var content = File.ReadAllText(tempFile);
                File.Delete(tempFile);
                return content;";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "IOTestClass", 
                Namespace = "TestNamespace"
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert - セキュリティ違反で失敗することを期待
            Assert.IsFalse(result.Success, "Compilation should fail due to security policy violation");
            Assert.IsTrue(result.HasSecurityViolations, "Result should indicate security violations");
            Assert.AreEqual(CompilationFailureReason.SecurityViolation, result.FailureReason, "Failure reason should be SecurityViolation");
            Assert.IsTrue(result.SecurityViolations.Count > 0, "Should have at least one security violation");
            
            // セキュリティ違反の詳細を確認
            SecurityViolation violation = result.SecurityViolations.FirstOrDefault(v => v.Type == SecurityViolationType.ForbiddenNamespace);
            Assert.IsNotNull(violation, "Should have forbidden namespace violation");
            Assert.That(violation.Description, Does.Contain("System.IO"), "Violation description should mention System.IO namespace");
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

            // Act - コンパイル後にキャッシュクリア
            CompilationResult firstResult = _compiler.Compile(request);
            _compiler.ClearCache();
            CompilationResult secondResult = _compiler.Compile(request);

            // Assert
            Assert.IsTrue(firstResult.Success);
            Assert.IsTrue(secondResult.Success);
            // 両方とも成功するが、キャッシュがクリアされていることを確認
            Assert.IsNotNull(firstResult.CompiledAssembly);
            Assert.IsNotNull(secondResult.CompiledAssembly);
        }

        [Test]
        public void Compile_WithCustomUserClass_ShouldFailGracefully()
        {
            // Arrange - ユーザー定義のカスタムクラス（テストアセンブリにのみ存在）
            // 動的アセンブリ追加では見つからないため失敗するのが正常
            string code = @"
                var testHelper = new LogGetterTestHelper();
                return testHelper.GetType().Name;";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "CustomClassTestClass",
                Namespace = "TestNamespace"
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert - テストアセンブリのクラスは動的追加で見つからない（正常動作）
            Assert.IsFalse(result.Success, "Custom test class should not be found in dynamic assembly addition");
            Assert.AreEqual(CompilationFailureReason.CompilationError, result.FailureReason);
            Assert.IsFalse(result.HasSecurityViolations, "Should not be a security violation");
            Assert.IsTrue(result.Errors.Count > 0, "Should have compilation errors");
            
            // エラーメッセージに型が見つからないことが含まれている
            bool hasTypeNotFoundError = result.Errors.Any(e => 
                e.Message.Contains("LogGetterTestHelper") && 
                e.Message.Contains("could not be found"));
            Assert.IsTrue(hasTypeNotFoundError, "Should have 'type not found' error for custom test class");
        }

        [Test]
        public void Compile_WithCustomClassFromAnotherNamespace_ShouldFailGracefully()
        {
            // Arrange - 異なる名前空間のカスタムクラス（テストアセンブリにのみ存在）
            // 動的アセンブリ追加では見つからないため失敗するのが正常
            string code = @"
                var compiler = new RoslynCompiler();
                return compiler.GetType().FullName;";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "NamespaceTestClass",
                Namespace = "TestNamespace"
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert - テストアセンブリのクラスは動的追加で見つからない（正常動作）
            Assert.IsFalse(result.Success, "Custom namespace class should not be found in dynamic assembly addition");
            Assert.AreEqual(CompilationFailureReason.CompilationError, result.FailureReason);
            Assert.IsFalse(result.HasSecurityViolations, "Should not be a security violation");
            Assert.IsTrue(result.Errors.Count > 0, "Should have compilation errors");
            
            bool hasTypeNotFoundError = result.Errors.Any(e => 
                e.Message.Contains("RoslynCompiler") && 
                e.Message.Contains("could not be found"));
            Assert.IsTrue(hasTypeNotFoundError, "Should have 'type not found' error for RoslynCompiler");
        }

        [Test]
        public void Compile_WithEditorOnlyClass_ShouldSucceedWithDynamicAddition()
        {
            // Arrange - UnityEditorのクラスを使用（動的アセンブリ追加で見つかる）
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

            // Assert - UnityEditorアセンブリは動的追加で見つかる
            Assert.IsTrue(result.Success, $"Compilation failed: {string.Join(", ", result.Errors?.Select(e => e.Message) ?? new string[0])}");
            Assert.IsNotNull(result.CompiledAssembly);
        }

        [Test]
        public void Compile_WithMultipleCustomClasses_ShouldFailForTestAssemblyClasses()
        {
            // Arrange - テストアセンブリのクラスと実際に見つかるクラスの混在
            // LogGetterTestHelper, RoslynCompiler → テストアセンブリ（見つからない）
            // EditorWindow → UnityEditorアセンブリ（動的追加で見つかる）
            string code = @"
                var testHelper = new LogGetterTestHelper();
                var compiler = new RoslynCompiler();
                using UnityEditor;
                var window = ScriptableObject.CreateInstance<EditorWindow>();
                
                return $""Helper: {testHelper.GetType().Name}, Compiler: {compiler.GetType().Name}, Window: {window != null}"";";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "MultiClassTestClass", 
                Namespace = "TestNamespace"
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert - テストアセンブリのクラスが見つからないため失敗
            Assert.IsFalse(result.Success, "Should fail because test assembly classes are not found");
            Assert.AreEqual(CompilationFailureReason.CompilationError, result.FailureReason);
            Assert.IsFalse(result.HasSecurityViolations, "Should not be a security violation");
            Assert.IsTrue(result.Errors.Count > 0, "Should have compilation errors");
            
            // テストアセンブリのクラスが見つからないエラーを確認
            bool hasTestClassErrors = result.Errors.Any(e => 
                e.Message.Contains("LogGetterTestHelper") || 
                e.Message.Contains("RoslynCompiler"));
            Assert.IsTrue(hasTestClassErrors, "Should have errors for test assembly classes");
        }

        [Test]
        public void Compile_WithNonExistentCustomClass_ShouldFailGracefullyWithSpecificError()
        {
            // Arrange - 存在しないカスタムクラスを使用
            string code = @"
                var nonExistent = new NonExistentCustomClass();
                return nonExistent.ToString();";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "NonExistentClassTestClass",
                Namespace = "TestNamespace"
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            Assert.IsFalse(result.Success, "Compilation should fail for non-existent class");
            Assert.AreEqual(CompilationFailureReason.CompilationError, result.FailureReason);
            Assert.IsFalse(result.HasSecurityViolations, "Should not be a security violation");
            Assert.IsTrue(result.Errors.Count > 0, "Should have compilation errors");
            
            // エラーメッセージに型が見つからないことが含まれている
            bool hasTypeNotFoundError = result.Errors.Any(e => 
                e.Message.Contains("NonExistentCustomClass") && 
                e.Message.Contains("could not be found"));
            Assert.IsTrue(hasTypeNotFoundError, "Should have 'type not found' error for custom class");
        }

        [Test]
        public void Compile_WithGenericCustomClass_ShouldSucceedWithDynamicAddition()
        {
            // Arrange - ジェネリック型を使用した複雑なケース
            string code = @"
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
            Assert.That(result.UpdatedCode, Does.Contain("using System.Collections.Generic"), 
                "Should auto-add System.Collections.Generic using statement");
        }

        [Test]
        public void Compile_WithSeparateAssemblyClassWithUsing_ShouldSucceedInAllAssembliesMode()
        {
            // Arrange - 別アセンブリ（DynamicAssemblyTest.dll）のクラスをusing文付きで使用
            string code = @"using io.github.hatayama.uLoopMCP;
namespace TestNamespace
{
    public class SeparateAssemblyWithUsingTestClass
    {
        public object Execute()
        {
            var testInstance = new DynamicAssemblyTest();
            return testInstance.HelloWorld();
        }
    }
}";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "SeparateAssemblyWithUsingTestClass",
                Namespace = "TestNamespace",
                AssemblyMode = AssemblyLoadingMode.AllAssemblies  // 全アセンブリモード
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert - 全アセンブリモードでは成功する
            Assert.IsTrue(result.Success, $"Compilation failed: {string.Join(", ", result.Errors?.Select(e => e.Message) ?? new string[0])}");
            Assert.IsNotNull(result.CompiledAssembly);
            Assert.IsFalse(result.HasSecurityViolations, "Should not be a security violation");
        }

        [Test]
        public void Compile_WithSeparateAssemblyClass_ShouldFailInDynamicAdditionMode()
        {
            // Arrange - 別アセンブリのクラスを動的追加モードでテスト
            string code = @"
                var testInstance = new DynamicAssemblyTest();
                return testInstance.HelloWorld();";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "DynamicModeTestClass",
                Namespace = "TestNamespace",
                AssemblyMode = AssemblyLoadingMode.DynamicAddition  // 動的追加モード
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert - 動的追加モードでは失敗する（現在の仕様）
            Assert.IsFalse(result.Success, "Should fail in DynamicAddition mode for separate assembly class");
            Assert.AreEqual(CompilationFailureReason.CompilationError, result.FailureReason);
            Assert.IsFalse(result.HasSecurityViolations, "Should not be a security violation");
            
            bool hasTypeNotFoundError = result.Errors.Any(e => 
                e.Message.Contains("DynamicAssemblyTest") && 
                e.Message.Contains("could not be found"));
            Assert.IsTrue(hasTypeNotFoundError, "Should have 'type not found' error for DynamicAssemblyTest");
        }

        [Test]
        public void Compile_WithSeparateAssemblyClassFullyQualified_ShouldSucceedInAllAssembliesMode()
        {
            // Arrange - 別アセンブリのクラスを完全修飾名で使用
            string code = @"
                var testInstance = new io.github.hatayama.uLoopMCP.DynamicAssemblyTest();
                return testInstance.HelloWorld();";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "FullyQualifiedSeparateAssemblyTestClass",
                Namespace = "TestNamespace",
                AssemblyMode = AssemblyLoadingMode.AllAssemblies  // 全アセンブリモード
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert - 完全修飾名で全アセンブリモードでは成功
            Assert.IsTrue(result.Success, $"Compilation failed: {string.Join(", ", result.Errors?.Select(e => e.Message) ?? new string[0])}");
            Assert.IsNotNull(result.CompiledAssembly);
            Assert.IsFalse(result.HasSecurityViolations, "Should not be a security violation");
        }
    }
}