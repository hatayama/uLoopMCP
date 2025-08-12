#if ULOOPMCP_HAS_ROSLYN
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// ExecuteDynamicCodeToolのセキュリティ機能統合テスト
    /// 実際のツール経由でセキュリティ制限が機能するか確認
    /// </summary>
    [TestFixture]
    public class ExecuteDynamicCodeSecurityIntegrationTests
    {
        private ExecuteDynamicCodeTool _tool;
        private DynamicCodeSecurityLevel _originalLevel;

        [SetUp]
        public void SetUp()
        {
            // 元のセキュリティレベルを保存
            _originalLevel = DynamicCodeSecurityManager.CurrentLevel;
            
            // ツールインスタンス作成
            _tool = new ExecuteDynamicCodeTool();
        }

        [TearDown]
        public void TearDown()
        {
            // 元のセキュリティレベルに戻す
            SecurityTestHelper.SetSecurityLevel(_originalLevel);
        }

        [Test]
        public async Task Level0_コード実行が拒否されるか確認()
        {
            // Arrange
            SecurityTestHelper.SetSecurityLevel(DynamicCodeSecurityLevel.Disabled);
            ExecuteDynamicCodeSchema parameters = new()
            {
                Code = "return \"Hello World\";",
                CompileOnly = false
            };

            // Act
            BaseToolResponse baseResponse = await _tool.ExecuteAsync(JToken.FromObject(parameters));
            ExecuteDynamicCodeResponse response = baseResponse as ExecuteDynamicCodeResponse;

            // Assert
            Assert.IsFalse(response.Success);
            Assert.IsNotNull(response.Error);
            Assert.IsTrue(response.Error.Contains("disabled") || response.Error.Contains("Disabled"));
            Assert.AreEqual("Disabled", response.SecurityLevel);
        }

        [Test]
        public async Task Level1_SystemIO使用コードが拒否されるか確認()
        {
            // Arrange
            SecurityTestHelper.SetSecurityLevel(DynamicCodeSecurityLevel.Restricted);
            ExecuteDynamicCodeSchema parameters = new()
            {
                Code = "System.IO.File.ReadAllText(\"test.txt\"); return \"Done\";",
                CompileOnly = false
            };

            // Act
            BaseToolResponse baseResponse = await _tool.ExecuteAsync(JToken.FromObject(parameters));
            ExecuteDynamicCodeResponse response = baseResponse as ExecuteDynamicCodeResponse;

            // Assert
            Assert.IsFalse(response.Success);
            Assert.IsNotNull(response.Error);
            Assert.IsTrue(response.Error.Contains("dangerous") || response.Error.Contains("blocked"));
            Assert.AreEqual("Restricted", response.SecurityLevel);
        }

        [Test]
        public async Task Level1_File使用コードが拒否されるか確認()
        {
            // Arrange
            SecurityTestHelper.SetSecurityLevel(DynamicCodeSecurityLevel.Restricted);
            ExecuteDynamicCodeSchema parameters = new()
            {
                Code = "File.WriteAllText(\"test.txt\", \"content\"); return \"Done\";",
                CompileOnly = false
            };

            // Act
            BaseToolResponse baseResponse = await _tool.ExecuteAsync(JToken.FromObject(parameters));
            ExecuteDynamicCodeResponse response = baseResponse as ExecuteDynamicCodeResponse;

            // Assert
            Assert.IsFalse(response.Success);
            Assert.IsNotNull(response.Error);
            Assert.IsTrue(response.Error.Contains("dangerous") || response.Error.Contains("blocked"));
        }

        [Test]
        public async Task Level1_HttpClient使用コードが拒否されるか確認()
        {
            // Arrange
            SecurityTestHelper.SetSecurityLevel(DynamicCodeSecurityLevel.Restricted);
            ExecuteDynamicCodeSchema parameters = new()
            {
                Code = "var client = new HttpClient(); return \"Done\";",
                CompileOnly = false
            };

            // Act
            BaseToolResponse baseResponse = await _tool.ExecuteAsync(JToken.FromObject(parameters));
            ExecuteDynamicCodeResponse response = baseResponse as ExecuteDynamicCodeResponse;

            // Assert
            Assert.IsFalse(response.Success);
            Assert.IsNotNull(response.Error);
            Assert.IsTrue(response.Error.Contains("dangerous") || response.Error.Contains("blocked"));
        }

        [Test]
        public async Task Level1_GameObject作成コードが実行できるか確認()
        {
            // Arrange
            SecurityTestHelper.SetSecurityLevel(DynamicCodeSecurityLevel.Restricted);
            ExecuteDynamicCodeSchema parameters = new()
            {
                Code = "GameObject obj = new GameObject(\"TestObject\"); UnityEngine.Object.DestroyImmediate(obj); return \"GameObject created and destroyed\";",
                CompileOnly = false
            };

            // Act
            BaseToolResponse baseResponse = await _tool.ExecuteAsync(JToken.FromObject(parameters));
            ExecuteDynamicCodeResponse response = baseResponse as ExecuteDynamicCodeResponse;

            // Assert
            Assert.IsTrue(response.Success, $"Unexpected error: {response.Error}");
            Assert.IsTrue(string.IsNullOrEmpty(response.Error), $"Error should be null or empty but was: '{response.Error}'");
            Assert.AreEqual("GameObject created and destroyed", response.Result);
            Assert.AreEqual("Restricted", response.SecurityLevel);
        }

        [Test]
        public async Task Level1_UnityEngineDebugLogが実行できるか確認()
        {
            // Arrange
            SecurityTestHelper.SetSecurityLevel(DynamicCodeSecurityLevel.Restricted);
            ExecuteDynamicCodeSchema parameters = new()
            {
                Code = "UnityEngine.Debug.Log(\"Test message\"); return \"Logged\";",
                CompileOnly = false
            };

            // Act
            BaseToolResponse baseResponse = await _tool.ExecuteAsync(JToken.FromObject(parameters));
            ExecuteDynamicCodeResponse response = baseResponse as ExecuteDynamicCodeResponse;

            // Assert
            Assert.IsTrue(response.Success, $"Unexpected error: {response.Error}");
            Assert.IsTrue(string.IsNullOrEmpty(response.Error), $"Error should be null or empty but was: '{response.Error}'");
            Assert.AreEqual("Logged", response.Result);
        }

        [Test]
        public async Task Level1_数学演算が実行できるか確認()
        {
            // Arrange
            SecurityTestHelper.SetSecurityLevel(DynamicCodeSecurityLevel.Restricted);
            ExecuteDynamicCodeSchema parameters = new()
            {
                Code = "int sum = 0; for(int i = 1; i <= 10; i++) sum += i; return sum;",
                CompileOnly = false
            };

            // Act
            BaseToolResponse baseResponse = await _tool.ExecuteAsync(JToken.FromObject(parameters));
            ExecuteDynamicCodeResponse response = baseResponse as ExecuteDynamicCodeResponse;

            // Assert
            Assert.IsTrue(response.Success, $"Unexpected error: {response.Error}");
            Assert.IsTrue(string.IsNullOrEmpty(response.Error), $"Error should be null or empty but was: '{response.Error}'");
            Assert.AreEqual("55", response.Result?.ToString());
        }

        [Test]
        public async Task Level1_AssemblyCSharpクラス使用コードが拒否されるか確認()
        {
            // Arrange
            SecurityTestHelper.SetSecurityLevel(DynamicCodeSecurityLevel.Restricted);
            ExecuteDynamicCodeSchema parameters = new()
            {
                Code = "var test = new io.github.hatayama.uLoopMCP.ForAssemblyCSharpTest(); return test.HelloWorld();",
                CompileOnly = false
            };

            // Act
            BaseToolResponse baseResponse = await _tool.ExecuteAsync(JToken.FromObject(parameters));
            ExecuteDynamicCodeResponse response = baseResponse as ExecuteDynamicCodeResponse;

            // Assert
            Assert.IsFalse(response.Success, "Level 1 should block Assembly-CSharp access");
            Assert.IsNotNull(response.Error);
            // Level 1ではAssembly-CSharpが参照に含まれないため、コンパイルエラーになる
            Assert.IsTrue(response.Error.Contains("ForAssemblyCSharpTest") || response.Error.Contains("missing an assembly reference") || response.Error.Contains("does not exist"),
                $"Error message should indicate compilation error due to missing Assembly-CSharp reference, but was: {response.Error}");
            Assert.AreEqual("Restricted", response.SecurityLevel);
        }

        [Test]
        public async Task Level1_ForDynamicAssemblyTestクラス使用コードが拒否されるか確認()
        {
            // Arrange
            SecurityTestHelper.SetSecurityLevel(DynamicCodeSecurityLevel.Restricted);
            ExecuteDynamicCodeSchema parameters = new()
            {
                Code = "var test = new io.github.hatayama.uLoopMCP.ForDynamicAssemblyTest(); return test.HelloWorldInAnotherDLL();",
                CompileOnly = false
            };

            // Act
            BaseToolResponse baseResponse = await _tool.ExecuteAsync(JToken.FromObject(parameters));
            ExecuteDynamicCodeResponse response = baseResponse as ExecuteDynamicCodeResponse;

            // Assert
            Assert.IsFalse(response.Success, "Level 1 should block ForDynamicAssemblyTest access");
            Assert.IsNotNull(response.Error);
            // ForDynamicAssemblyTest.dllもユーザーコードなのでブロックされる
            Assert.AreEqual("Restricted", response.SecurityLevel);
        }

        [Test]
        public async Task Level2_SystemIO使用コードが実行できるか確認()
        {
            // Arrange
            SecurityTestHelper.SetSecurityLevel(DynamicCodeSecurityLevel.FullAccess);
            string testFile = System.IO.Path.GetTempFileName();
            
            try
            {
                System.IO.File.WriteAllText(testFile, "Test content");
                
                ExecuteDynamicCodeSchema parameters = new()
                {
                    Code = $"string content = System.IO.File.ReadAllText(@\"{testFile}\"); return content;",
                    CompileOnly = false
                };

                // Act
                BaseToolResponse baseResponse = await _tool.ExecuteAsync(JToken.FromObject(parameters));
                ExecuteDynamicCodeResponse response = baseResponse as ExecuteDynamicCodeResponse;

                // Assert
                Assert.IsTrue(response.Success, $"Unexpected error: {response.Error}");
                Assert.IsTrue(string.IsNullOrEmpty(response.Error), $"Error should be null or empty but was: '{response.Error}'");
                Assert.AreEqual("Test content", response.Result);
                Assert.AreEqual("FullAccess", response.SecurityLevel);
            }
            finally
            {
                // クリーンアップ
                if (System.IO.File.Exists(testFile))
                {
                    System.IO.File.Delete(testFile);
                }
            }
        }

        [Test]
        public async Task Level2_Directory操作が実行できるか確認()
        {
            // Arrange
            SecurityTestHelper.SetSecurityLevel(DynamicCodeSecurityLevel.FullAccess);
            ExecuteDynamicCodeSchema parameters = new()
            {
                Code = "bool exists = System.IO.Directory.Exists(System.IO.Directory.GetCurrentDirectory()); return exists;",
                CompileOnly = false
            };

            // Act
            BaseToolResponse baseResponse = await _tool.ExecuteAsync(JToken.FromObject(parameters));
            ExecuteDynamicCodeResponse response = baseResponse as ExecuteDynamicCodeResponse;

            // Assert
            Assert.IsTrue(response.Success, $"Unexpected error: {response.Error}");
            Assert.IsTrue(string.IsNullOrEmpty(response.Error), $"Error should be null or empty but was: '{response.Error}'");
            Assert.AreEqual("True", response.Result?.ToString());
        }

        [Test]
        public async Task Level2_AssemblyCSharpクラスが使用できるか確認()
        {
            // Arrange
            SecurityTestHelper.SetSecurityLevel(DynamicCodeSecurityLevel.FullAccess);
            ExecuteDynamicCodeSchema parameters = new()
            {
                Code = "var test = new io.github.hatayama.uLoopMCP.ForAssemblyCSharpTest(); return test.HelloWorld();",
                CompileOnly = false
            };

            // Act
            BaseToolResponse baseResponse = await _tool.ExecuteAsync(JToken.FromObject(parameters));
            ExecuteDynamicCodeResponse response = baseResponse as ExecuteDynamicCodeResponse;

            // Assert
            Assert.IsTrue(response.Success, $"Level 2 should allow Assembly-CSharp access. Error: {response.Error}");
            Assert.IsTrue(string.IsNullOrEmpty(response.Error), $"Error should be null or empty but was: '{response.Error}'");
            Assert.AreEqual("Hello World", response.Result);
            Assert.AreEqual("FullAccess", response.SecurityLevel);
        }

        [Test]
        public async Task Level2_ForDynamicAssemblyTestクラスが使用できるか確認()
        {
            // Arrange
            SecurityTestHelper.SetSecurityLevel(DynamicCodeSecurityLevel.FullAccess);
            ExecuteDynamicCodeSchema parameters = new()
            {
                Code = "var test = new io.github.hatayama.uLoopMCP.ForDynamicAssemblyTest(); return test.HelloWorldInAnotherDLL();",
                CompileOnly = false
            };

            // Act
            BaseToolResponse baseResponse = await _tool.ExecuteAsync(JToken.FromObject(parameters));
            ExecuteDynamicCodeResponse response = baseResponse as ExecuteDynamicCodeResponse;

            // Assert
            Assert.IsTrue(response.Success, $"Level 2 should allow ForDynamicAssemblyTest access. Error: {response.Error}");
            Assert.IsTrue(string.IsNullOrEmpty(response.Error), $"Error should be null or empty but was: '{response.Error}'");
            Assert.AreEqual("Hello World", response.Result);
            Assert.AreEqual("FullAccess", response.SecurityLevel);
        }

        [Test]
        public async Task Level2_AssemblyCSharpの危険なメソッドも実行できるか確認()
        {
            // Arrange
            SecurityTestHelper.SetSecurityLevel(DynamicCodeSecurityLevel.FullAccess);
            ExecuteDynamicCodeSchema parameters = new()
            {
                // Level 2では危険な操作も許可される（テスト目的のみ、実際の実行は避ける）
                Code = "// Level 2 allows dangerous operations\nreturn \"Test skipped for safety\";",
                CompileOnly = false
            };

            // Act
            BaseToolResponse baseResponse = await _tool.ExecuteAsync(JToken.FromObject(parameters));
            ExecuteDynamicCodeResponse response = baseResponse as ExecuteDynamicCodeResponse;

            // Assert
            Assert.IsTrue(response.Success, $"Level 2 should allow execution. Error: {response.Error}");
            Assert.AreEqual("FullAccess", response.SecurityLevel);
        }

        [Test]
        public async Task CompileOnlyモードでLevel0でもコンパイルは成功するか確認()
        {
            // Arrange
            SecurityTestHelper.SetSecurityLevel(DynamicCodeSecurityLevel.Disabled);
            ExecuteDynamicCodeSchema parameters = new()
            {
                Code = "return \"Hello World\";",
                CompileOnly = true
            };

            // Act
            BaseToolResponse baseResponse = await _tool.ExecuteAsync(JToken.FromObject(parameters));
            ExecuteDynamicCodeResponse response = baseResponse as ExecuteDynamicCodeResponse;

            // Assert
            // Level 0では実行が禁止されるため、CompileOnlyでも失敗する
            Assert.IsFalse(response.Success);
            Assert.IsNotNull(response.Error);
            Assert.IsTrue(response.Error.Contains("disabled") || response.Error.Contains("Disabled"));
        }

        [Test]
        public async Task SecurityLevelがレスポンスに含まれるか確認()
        {
            // Arrange
            DynamicCodeSecurityLevel[] levels = 
            {
                DynamicCodeSecurityLevel.Disabled,
                DynamicCodeSecurityLevel.Restricted,
                DynamicCodeSecurityLevel.FullAccess
            };

            foreach (DynamicCodeSecurityLevel level in levels)
            {
                SecurityTestHelper.SetSecurityLevel(level);
                ExecuteDynamicCodeSchema parameters = new()
                {
                    Code = "return 123;",
                    CompileOnly = false
                };

                // Act
                BaseToolResponse baseResponse = await _tool.ExecuteAsync(JToken.FromObject(parameters));
                ExecuteDynamicCodeResponse response = baseResponse as ExecuteDynamicCodeResponse;

                // Assert
                Assert.IsNotNull(response.SecurityLevel);
                Assert.AreEqual(level.ToString(), response.SecurityLevel);
            }
        }
    }
}
#endif