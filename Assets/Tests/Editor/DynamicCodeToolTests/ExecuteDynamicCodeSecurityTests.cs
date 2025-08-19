#if ULOOPMCP_HAS_ROSLYN
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    /// <summary>
    /// ExecuteDynamicCodeTool関連のRestrictedモードテスト
    /// セキュリティ違反を検出するテストを集約
    /// </summary>
    [TestFixture]
    public class ExecuteDynamicCodeSecurityTests
    {
        private RoslynCompiler _compiler;

        [SetUp]
        public void SetUp()
        {
            // Restrictedモードに設定（セキュリティ検証を有効化）
            DynamicCodeSecurityManager.InitializeFromSettings(DynamicCodeSecurityLevel.Restricted);
            _compiler = new RoslynCompiler();
        }

        [TearDown]
        public void TearDown()
        {
            _compiler?.ClearCache();
            _compiler = null;
            // デフォルトのRestrictedモードに戻す
            DynamicCodeSecurityManager.InitializeFromSettings(DynamicCodeSecurityLevel.Restricted);
        }

        [Test]
        public void Compile_WithSystemIOType_ShouldFailWithSecurityViolation()
        {
            // Arrange - System.IOの危険なAPIを使用（セキュリティポリシーで禁止）
            string code = @"
                using System.IO;
                string tempFile = ""test.txt"";
                File.Delete(tempFile);  // Deleteは危険API
                File.WriteAllText(tempFile, ""test"");  // WriteAllTextも危険API
                return ""done"";";

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
        public void Compile_WithSystemNetHttpType_ShouldFailWithSecurityViolation()
        {
            // Arrange - System.Net.Httpの型を使用（セキュリティポリシーで禁止）
            string code = @"
                using System.Net.Http;
                HttpClient client = new();
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
        public void Compile_WithDangerousFileDelete_ShouldFailWithSecurityViolation()
        {
            // Arrange - 危険なファイル削除操作
            string code = @"
                string path = ""/temp/test.txt"";
                System.IO.File.Delete(path);
                return ""deleted"";";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "FileDeleteTestClass",
                Namespace = "TestNamespace"
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            Assert.IsFalse(result.Success, "Should fail due to dangerous API");
            Assert.IsTrue(result.HasSecurityViolations, "Should have security violations");
        }

        [Test]
        public void Compile_WithProcessStart_ShouldFailWithSecurityViolation()
        {
            // Arrange - プロセス起動（危険な操作）
            string code = @"
                System.Diagnostics.Process.Start(""notepad.exe"");
                return ""started"";";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "ProcessTestClass",
                Namespace = "TestNamespace"
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            Assert.IsFalse(result.Success, "Should fail due to dangerous API");
            Assert.IsTrue(result.HasSecurityViolations || result.Errors?.Any() == true, 
                "Should have security violations or compilation errors");
        }

        [Test]
        public void Compile_WithReflectionAssemblyLoad_ShouldFailWithSecurityViolation()
        {
            // Arrange - Assembly.Load（危険な操作）
            string code = @"
                System.Reflection.Assembly.Load(""System.IO"");
                return ""loaded"";";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "ReflectionTestClass",
                Namespace = "TestNamespace"
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            Assert.IsFalse(result.Success, "Should fail due to dangerous API");
            Assert.IsTrue(result.HasSecurityViolations || result.Errors?.Any() == true, 
                "Should have security violations or compilation errors");
        }

        [Test]
        public void Compile_WithDynamicAssemblyTest_SafeMethods_ShouldSucceed()
        {
            // Arrange - DynamicAssemblyTestの安全なメソッド使用
            string code = @"
                var test = new io.github.hatayama.uLoopMCP.DynamicAssemblyTest();
                string hello = test.HelloWorld();
                int sum = test.Add(5, 3);
                string assembly = test.GetAssemblyName();
                return $""{hello}, Sum: {sum}, Assembly: {assembly}"";";

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
            // ExecuteAnoterInstanceMethodは別アセンブリのメソッドなので、
            // コンパイル時には内容を検査できず、成功する
            string code = @"
                var test = new io.github.hatayama.uLoopMCP.DynamicAssemblyTest();
                test.ExecuteAnoterInstanceMethod();
                return ""Compiled"";";

            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = "DynamicAssemblyIndirectTestClass",
                Namespace = "TestNamespace"
            };

            // Act
            CompilationResult result = _compiler.Compile(request);

            // Assert
            // 別アセンブリのメソッド内容は検査できないため、コンパイルは成功する
            Assert.IsTrue(result.Success, "Should compile successfully as method content is not inspected");
            Assert.IsFalse(result.HasSecurityViolations, "Should not detect violations in external assembly methods");
        }

        [Test]
        public void Compile_WithForDynamicAssemblyTest_DirectDangerousCall_ShouldSucceed()
        {
            // Arrange  
            // ForDynamicAssemblyTestのメソッド呼び出し自体は検出できない
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
            // 別アセンブリのメソッド呼び出しは、その内容まで検査できない
            Assert.IsTrue(result.Success, "External assembly method calls compile successfully");
            Assert.IsFalse(result.HasSecurityViolations, "Cannot detect violations inside external methods");
        }
    }
}
#endif