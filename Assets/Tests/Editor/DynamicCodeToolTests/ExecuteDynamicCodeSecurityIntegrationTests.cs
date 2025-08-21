#if ULOOPMCP_HAS_ROSLYN
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    /// <summary>
    /// ExecuteDynamicCodeのセキュリティ機能統合テスト
    /// v4.0ステートレス設計: ExecuteDynamicCodeToolを使用せず、DynamicCodeExecutorを直接使用
    /// 設計ドキュメント: /working-notes/2025-08-21_v4.0ステートレス設計移行/2025-08-21_v4.0ステートレス設計移行_design.md
    /// </summary>
    [TestFixture]
    public class ExecuteDynamicCodeSecurityIntegrationTests
    {
        // ExecuteDynamicCodeToolは使用しない（v4.0ステートレス設計）
        // 各テストメソッドで独立したExecutorを作成

        [Test]
        public async Task Level0_コード実行が拒否されるか確認()
        {
            // Executorを直接作成（グローバル状態非依存）
            IDynamicCodeExecutor executor = Factory.DynamicCodeExecutorFactory.Create(
                DynamicCodeSecurityLevel.Disabled
            );
            
            // 直接実行
            ExecutionResult result = await executor.ExecuteCodeAsync(
                "return \"Hello World\";",
                "DynamicCommand",
                null,
                CancellationToken.None,
                false
            );
            
            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.ErrorMessage);
            Assert.IsTrue(result.ErrorMessage.Contains("disabled") || 
                         result.ErrorMessage.Contains("Disabled"));
        }

        [Test]
        public async Task Level1_SystemIO使用コードが拒否されるか確認()
        {
            // Restrictedモードで直接Executor作成
            IDynamicCodeExecutor executor = Factory.DynamicCodeExecutorFactory.Create(
                DynamicCodeSecurityLevel.Restricted
            );
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                "System.IO.File.Delete(\"test.txt\"); return \"Done\";",
                "DynamicCommand",
                null,
                CancellationToken.None,
                false
            );
            
            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.ErrorMessage.Contains("セキュリティ違反") || 
                         result.ErrorMessage.Contains("dangerous") || 
                         result.ErrorMessage.Contains("blocked"));
        }

        [Test]
        public async Task Level1_File使用コードが拒否されるか確認()
        {
            // Restrictedモードで直接Executor作成
            IDynamicCodeExecutor executor = Factory.DynamicCodeExecutorFactory.Create(
                DynamicCodeSecurityLevel.Restricted
            );
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                "System.IO.File.WriteAllText(\"test.txt\", \"content\"); return \"Done\";",
                "DynamicCommand",
                null,
                CancellationToken.None,
                false
            );
            
            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.ErrorMessage.Contains("セキュリティ違反") || 
                         result.ErrorMessage.Contains("dangerous") || 
                         result.ErrorMessage.Contains("blocked"));
        }

        [Test]
        public async Task Level1_HttpClient使用コードが拒否されるか確認()
        {
            // Restrictedモードで直接Executor作成
            IDynamicCodeExecutor executor = Factory.DynamicCodeExecutorFactory.Create(
                DynamicCodeSecurityLevel.Restricted
            );
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                "var client = new System.Net.Http.HttpClient(); return \"Done\";",
                "DynamicCommand",
                null,
                CancellationToken.None,
                false
            );
            
            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.ErrorMessage.Contains("セキュリティ違反") || 
                         result.ErrorMessage.Contains("dangerous") || 
                         result.ErrorMessage.Contains("blocked"));
        }

        [Test]
        public async Task Level1_GameObject作成コードが実行できるか確認()
        {
            // Restrictedモードで直接Executor作成
            IDynamicCodeExecutor executor = Factory.DynamicCodeExecutorFactory.Create(
                DynamicCodeSecurityLevel.Restricted
            );
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                "UnityEngine.GameObject obj = new UnityEngine.GameObject(\"TestObject\"); UnityEngine.Object.DestroyImmediate(obj); return \"GameObject created and destroyed\";",
                "DynamicCommand",
                null,
                CancellationToken.None,
                false
            );
            
            // Assert
            Assert.IsTrue(result.Success, $"Unexpected error: {result.ErrorMessage}");
            Assert.IsTrue(string.IsNullOrEmpty(result.ErrorMessage), $"Error should be null or empty but was: '{result.ErrorMessage}'");
            Assert.AreEqual("GameObject created and destroyed", result.Result);
        }

        [Test]
        public async Task Level1_UnityEngineDebugLogが実行できるか確認()
        {
            // Restrictedモードで直接Executor作成
            IDynamicCodeExecutor executor = Factory.DynamicCodeExecutorFactory.Create(
                DynamicCodeSecurityLevel.Restricted
            );
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                "UnityEngine.Debug.Log(\"Test message\"); return \"Log executed\";",
                "DynamicCommand",
                null,
                CancellationToken.None,
                false
            );
            
            // Assert
            Assert.IsTrue(result.Success, $"Unexpected error: {result.ErrorMessage}");
            Assert.AreEqual("Log executed", result.Result);
        }

        [Test]
        public async Task Level2_全機能有効でコード実行が成功するか確認()
        {
            // FullAccessモードで直接Executor作成
            IDynamicCodeExecutor executor = Factory.DynamicCodeExecutorFactory.Create(
                DynamicCodeSecurityLevel.FullAccess
            );
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                "return 1 + 2;",
                "DynamicCommand",
                null,
                CancellationToken.None,
                false
            );
            
            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(3, System.Convert.ToInt32(result.Result));
        }

        [Test]
        public async Task Level2_File使用コードも実行できるか確認()
        {
            // FullAccessモードで直接Executor作成
            IDynamicCodeExecutor executor = Factory.DynamicCodeExecutorFactory.Create(
                DynamicCodeSecurityLevel.FullAccess
            );
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                "bool exists = System.IO.File.Exists(\"dummy.txt\"); return exists;",
                "DynamicCommand",
                null,
                CancellationToken.None,
                false
            );
            
            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(false, System.Convert.ToBoolean(result.Result)); // ファイルは存在しないはず
        }

        [Test]
        public async Task CompileOnlyフラグが機能するか確認()
        {
            // Restrictedモードで直接Executor作成
            IDynamicCodeExecutor executor = Factory.DynamicCodeExecutorFactory.Create(
                DynamicCodeSecurityLevel.Restricted
            );
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                "return \"Test\";",
                "DynamicCommand",
                null,
                CancellationToken.None,
                true  // CompileOnly = true
            );
            
            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsNull(result.Result);  // CompileOnlyの場合、実行されないのでnull
        }

        [Test]
        public async Task パラメータ渡しが機能するか確認()
        {
            // Restrictedモードで直接Executor作成
            IDynamicCodeExecutor executor = Factory.DynamicCodeExecutorFactory.Create(
                DynamicCodeSecurityLevel.Restricted
            );
            
            // パラメータ配列を作成
            object[] parameters = new object[] { 10, 20 };
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                "int v1 = (int)parameters[\"param0\"]; int v2 = (int)parameters[\"param1\"]; return v1 + v2;",
                "DynamicCommand",
                parameters,
                CancellationToken.None,
                false
            );
            
            // Assert
            Assert.IsTrue(result.Success, $"Execution failed: {result.ErrorMessage}");
            Assert.AreEqual(30, System.Convert.ToInt32(result.Result));
        }

        [Test]
        public async Task コンパイルエラーが適切に返されるか確認()
        {
            // Restrictedモードで直接Executor作成
            IDynamicCodeExecutor executor = Factory.DynamicCodeExecutorFactory.Create(
                DynamicCodeSecurityLevel.Restricted
            );
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                "this is invalid C# code",
                "DynamicCommand",
                null,
                CancellationToken.None,
                false
            );
            
            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.ErrorMessage);
            // "this is invalid C# code"は複数のコンパイルエラーを引き起こす
            // 例: "Invalid token", "Identifier expected", ";が必要" など
            Assert.IsTrue(result.ErrorMessage.Contains("Invalid") || 
                         result.ErrorMessage.Contains("expected") || 
                         result.ErrorMessage.Contains("エラー") ||
                         result.ErrorMessage.Contains("コンパイル"));
        }

        [Test]
        public async Task 実行時エラーが適切に返されるか確認()
        {
            // Restrictedモードで直接Executor作成
            IDynamicCodeExecutor executor = Factory.DynamicCodeExecutorFactory.Create(
                DynamicCodeSecurityLevel.Restricted
            );
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                "int x = 10; int y = 0; return x / y;",  // ゼロ除算
                "DynamicCommand",
                null,
                CancellationToken.None,
                false
            );
            
            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.ErrorMessage);
            Assert.IsTrue(result.ErrorMessage.Contains("DivideByZero") || result.ErrorMessage.Contains("zero"));
        }
    }
}
#endif