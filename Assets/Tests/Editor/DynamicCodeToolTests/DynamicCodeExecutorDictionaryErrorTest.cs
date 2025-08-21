#if ULOOPMCP_HAS_ROSLYN
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;
using io.github.hatayama.uLoopMCP;
using io.github.hatayama.uLoopMCP.Factory;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    /// <summary>
    /// DynamicCodeExecutorのDictionaryエラーを再現するテスト
    /// TDDアプローチ: まず失敗するテストを書いて、エラーを再現する
    /// </summary>
    [TestFixture]
    public class DynamicCodeExecutorDictionaryErrorTest
    {
        private IDynamicCodeExecutor _executor;

        [SetUp]
        public void SetUp()
        {
            // v4.0ステートレス設計 - グローバル設定への変更を削除
            // RestrictedモードでExecutorを作成
            _executor = DynamicCodeExecutorFactory.Create(DynamicCodeSecurityLevel.Restricted);
        }

        [Test]
        [Description("最もシンプルなコードでもDictionaryエラーが発生することを確認")]
        public async Task SimpleReturnStatement_ShouldNotThrowDictionaryError()
        {
            // Arrange
            string simpleCode = "return \"Hello World\";";
            
            // Act
            ExecutionResult result = await _executor.ExecuteCodeAsync(
                simpleCode,
                "DynamicCommand",
                null,
                CancellationToken.None,
                false
            );
            
            // Assert - エラーが発生しないことを期待
            Assert.IsTrue(result.Success, 
                $"Simple return statement should succeed. Error: {result.ErrorMessage ?? "No error"}, Logs: {string.Join(", ", result.Logs ?? new System.Collections.Generic.List<string>())}");
            Assert.AreEqual("Hello World", result.Result);
        }

        [Test]
        [Description("変数宣言を含むコードでもDictionaryエラーが発生しないことを確認")]
        public async Task VariableDeclaration_ShouldNotThrowDictionaryError()
        {
            // Arrange
            string codeWithVariable = @"
                int x = 5;
                int y = 10;
                return x + y;
            ";
            
            // Act
            ExecutionResult result = await _executor.ExecuteCodeAsync(
                codeWithVariable,
                "DynamicCommand",
                null,
                CancellationToken.None,
                false
            );
            
            // Assert
            Assert.IsTrue(result.Success,
                $"Variable declaration should succeed. Error: {result.ErrorMessage ?? "No error"}, Logs: {string.Join(", ", result.Logs ?? new System.Collections.Generic.List<string>())}");
            // 注意: ExecuteDynamicCodeResponse.Resultはstring型なので、結果も文字列として比較
            Assert.AreEqual("15", result.Result.ToString());
        }



        [Test]
        [Description("コンパイルエラーの内容を詳細に確認")]
        public async Task AnalyzeCompilationError_GetDetailedErrorInfo()
        {
            // Arrange
            string testCode = "return 42;";
            
            // Act
            ExecutionResult result = await _executor.ExecuteCodeAsync(
                testCode,
                "DynamicCommand",
                null,
                CancellationToken.None,
                true // CompileOnlyモードでテスト
            );
            
            // Assert - エラー内容を詳細に出力
            if (!result.Success)
            {
                TestContext.WriteLine("=== Error Message ===");
                TestContext.WriteLine($"Error: {result.ErrorMessage ?? "No error message"}");
                
                TestContext.WriteLine("\n=== Logs ===");
                foreach (string log in result.Logs ?? new System.Collections.Generic.List<string>())
                {
                    TestContext.WriteLine($"Log: {log}");
                }
                
                // Dictionaryエラーが含まれているか確認
                bool hasDictionaryError = false;
                if (result.ErrorMessage != null && result.ErrorMessage.Contains("Dictionary"))
                {
                    hasDictionaryError = true;
                }
                foreach (string log in result.Logs ?? new System.Collections.Generic.List<string>())
                {
                    if (log.Contains("Dictionary"))
                    {
                        hasDictionaryError = true;
                        break;
                    }
                }
                
                Assert.IsFalse(hasDictionaryError, 
                    $"Code should not produce Dictionary-related errors. Error: {result.ErrorMessage}, Logs: {string.Join(", ", result.Logs)}");
            }
            else
            {
                Assert.Pass("Compilation succeeded as expected");
            }
        }
    }
}
#endif
