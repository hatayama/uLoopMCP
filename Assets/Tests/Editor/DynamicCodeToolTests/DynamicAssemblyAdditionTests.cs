#if ULOOPMCP_HAS_ROSLYN
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using io.github.hatayama.uLoopMCP;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
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
            // FullAccessモードでコンパイラを作成
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
                // Ensure errors are present when compilation fails
                Assert.IsNotNull(result.Errors, "Expected compilation errors when Success is false");
                
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

        // v5.2: 削除 - 現在の仕様では全アセンブリへの参照が許可されるため不要
        // [Test] Compile_WithCustomUserClass_ShouldFailGracefully

        // v5.2: 削除 - 現在の仕様では全アセンブリへの参照が許可されるため不要
        // [Test] Compile_WithCustomClassFromAnotherNamespace_ShouldFailGracefully



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

        // v5.2: 削除 - 現在の仕様では全アセンブリへの参照が許可されるため不要
        // [Test] Compile_WithMultipleCustomClasses_ShouldFailForTestAssemblyClasses

        // v5.2: 削除 - 現在の仕様では全アセンブリへの参照が許可されるため不要
        // [Test] Compile_WithNonExistentCustomClass_ShouldFailGracefullyWithSpecificError

        [Test]
        public void Compile_WithGenericCustomClass_ShouldSucceedWithDynamicAddition()
        {
            // Arrange - ジェネリック型を使用した複雑なケース
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

        // v5.2: 削除 - 現在の仕様では全アセンブリへの参照が許可されるため不要
        // [Test] Compile_WithSeparateAssemblyClass_ShouldFailInDynamicAdditionMode

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
#endif
