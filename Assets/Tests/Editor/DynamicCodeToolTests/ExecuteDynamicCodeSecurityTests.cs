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
            // Restrictedモードでコンパイラを作成（セキュリティ検証を有効化）
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