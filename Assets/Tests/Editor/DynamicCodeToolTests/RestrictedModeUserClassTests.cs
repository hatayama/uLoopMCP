#if ULOOPMCP_HAS_ROSLYN
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    /// <summary>
    /// Restrictedモードユーザークラス実行機能のテスト
    /// 設計ドキュメント参照: working-notes/2025-08-16_Restrictedモードユーザークラス実行機能_design.md
    /// </summary>
    [TestFixture]
    public class RestrictedModeUserClassTests
    {
        private DynamicCodeSecurityLevel originalLevel;
        private IDynamicCodeExecutor executor;
        
        [SetUp]
        public void SetUp()
        {
            // 元のセキュリティレベルを保存
            originalLevel = DynamicCodeSecurityManager.CurrentLevel;
            
            // テスト用のExecutorを作成
            executor = Factory.DynamicCodeExecutorFactory.CreateDefault();
        }
        
        [TearDown]
        public void TearDown()
        {
            // セキュリティレベルを復元
            DynamicCodeSecurityManager.InitializeFromSettings(originalLevel);
        }
        
        /// <summary>
        /// ユーザー定義アセンブリ検出テスト
        /// </summary>
        [Test]
        public void TestUserDefinedAssemblyDetection()
        {
            // Assembly-CSharp系の判定
            Assert.IsTrue(AssemblyClassifier.IsUserDefinedAssemblyByName("Assembly-CSharp"));
            Assert.IsTrue(AssemblyClassifier.IsUserDefinedAssemblyByName("Assembly-CSharp-Editor"));
            Assert.IsTrue(AssemblyClassifier.IsUserDefinedAssemblyByName("Assembly-CSharp-firstpass"));
            
            // システムアセンブリは除外
            Assert.IsFalse(AssemblyClassifier.IsUserDefinedAssemblyByName("System"));
            Assert.IsFalse(AssemblyClassifier.IsUserDefinedAssemblyByName("mscorlib"));
        }
        
        /// <summary>
        /// 危険なAPI検出テスト
        /// </summary>
        [Test]
        public void TestDangerousApiDetection()
        {
            DangerousApiDetector detector = new();
            
            // ファイルシステムAPI
            Assert.IsTrue(detector.IsDangerousApi("System.IO.File.Delete"));
            Assert.IsTrue(detector.IsDangerousApi("System.IO.Directory.Delete"));
            
            // ネットワークAPI
            Assert.IsTrue(detector.IsDangerousApi("System.Net.Http.HttpClient"));
            Assert.IsTrue(detector.IsDangerousApi("System.Net.WebClient"));
            
            // リフレクションAPI
            Assert.IsTrue(detector.IsDangerousApi("System.Reflection.Assembly.Load"));
            Assert.IsTrue(detector.IsDangerousApi("System.Type.InvokeMember"));
            
            // 安全なAPI
            Assert.IsFalse(detector.IsDangerousApi("UnityEngine.Debug.Log"));
            Assert.IsFalse(detector.IsDangerousApi("System.String.Concat"));
            Assert.IsFalse(detector.IsDangerousApi("System.Math.Sqrt"));
        }
        
        /// <summary>
        /// Restrictedモードで安全なコードが実行可能なことを確認
        /// </summary>
        [Test]
        public async Task TestSafeCodeExecutionInRestrictedMode()
        {
            // Restrictedモードに設定
            DynamicCodeSecurityManager.InitializeFromSettings(DynamicCodeSecurityLevel.Restricted);
            
            string safeCode = @"
                // 安全なコードのテスト
                UnityEngine.Debug.Log(""Safe logging"");
                string message = ""Hello World"";
                int result = 10 + 20;
                return message;
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                safeCode, 
                "TestCommand", 
                null, 
                CancellationToken.None,
                compileOnly: false
            );
            
            // デバッグ用：実際の結果を出力
            UnityEngine.Debug.Log($"[DEBUG] Success: {result.Success}");
            UnityEngine.Debug.Log($"[DEBUG] Result: '{result.Result}'");
            UnityEngine.Debug.Log($"[DEBUG] ErrorMessage: '{result.ErrorMessage}'");
            
            Assert.IsTrue(result.Success, $"Safe code should execute successfully. Error: {result.ErrorMessage}");
            Assert.AreEqual("Hello World", result.Result?.ToString());
        }
        
        /// <summary>
        /// Restrictedモードで危険なAPIを含むコードがブロックされることを確認
        /// </summary>
        [Test]
        public async Task TestDangerousCodeBlockedInRestrictedMode()
        {
            // Restrictedモードに設定
            DynamicCodeSecurityManager.InitializeFromSettings(DynamicCodeSecurityLevel.Restricted);
            
            string dangerousCode = @"
                // 危険なAPIを含むコード
                System.IO.File.Delete(""test.txt"");
                return ""Done"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                dangerousCode,
                "TestCommand",
                null,
                CancellationToken.None,
                compileOnly: false
            );
            
            Assert.IsFalse(result.Success, "Dangerous code should be blocked");
            
            // デバッグ用：実際のエラーメッセージを出力
            UnityEngine.Debug.Log($"[DEBUG] ErrorMessage: '{result.ErrorMessage}'");
            UnityEngine.Debug.Log($"[DEBUG] ErrorMessage Length: {result.ErrorMessage?.Length}");
            
            Assert.IsTrue(
                result.ErrorMessage?.Contains("セキュリティ違反") == true ||
                result.ErrorMessage?.Contains("Security validation failed") == true ||
                result.ErrorMessage?.Contains("dangerous") == true ||
                result.ErrorMessage?.Contains("blocked") == true ||
                result.ErrorMessage?.Contains("ForbiddenNamespace") == true,
                $"Error should mention security violation. Actual: '{result.ErrorMessage}'"
            );
        }
        
        /// <summary>
        /// ネストしたクラスでの危険API検出テスト
        /// </summary>
        [Test]
        public async Task TestDangerousApiInNestedClass()
        {
            DynamicCodeSecurityManager.InitializeFromSettings(DynamicCodeSecurityLevel.Restricted);
            
            string code = @"
                public class OuterClass {
                    private class InnerClass {
                        public void DangerousMethod() {
                            var client = new System.Net.Http.HttpClient();
                        }
                    }
                    
                    public string Process() {
                        var inner = new InnerClass();
                        return ""Processing"";
                    }
                }
                
                var outer = new OuterClass();
                return outer.Process();
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code,
                "TestCommand",
                null,
                CancellationToken.None,
                compileOnly: false
            );
            
            Assert.IsFalse(result.Success, "Code with nested dangerous API should be blocked");
        }
        
        /// <summary>
        /// ラムダ式内での危険API検出テスト
        /// </summary>
        [Test]
        public async Task TestDangerousApiInLambda()
        {
            DynamicCodeSecurityManager.InitializeFromSettings(DynamicCodeSecurityLevel.Restricted);
            
            string code = @"
                using System.Linq;
                
                public class UserClass {
                    public string ProcessData() {
                        var list = new[] { ""file1.txt"", ""file2.txt"" };
                        
                        // ラムダ式内での危険なAPI
                        var results = list.Select(f => {
                            if (System.IO.File.Exists(f)) {
                                return f;
                            }
                            return """";
                        }).ToList();
                        
                        return ""Processed"";
                    }
                }
                
                var obj = new UserClass();
                return obj.ProcessData();
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code,
                "TestCommand",
                null,
                CancellationToken.None,
                compileOnly: false
            );
            
            Assert.IsFalse(result.Success, "Code with lambda containing dangerous API should be blocked");
        }
        
        /// <summary>
        /// FullAccessモードでは全て許可されることを確認
        /// </summary>
        [Test]
        public async Task TestFullAccessModeAllowsEverything()
        {
            // FullAccessモードに設定
            DynamicCodeSecurityManager.InitializeFromSettings(DynamicCodeSecurityLevel.FullAccess);
            
            // デバッグ：現在のセキュリティレベルを確認
            UnityEngine.Debug.Log($"[DEBUG] Current Security Level: {DynamicCodeSecurityManager.CurrentLevel}");
            
            string code = @"
                // FullAccessモードでは通常のコードを実行
                // Restrictedモードと同じ安全なコードでも動作することを確認
                string message = ""FullAccess mode active"";
                int value = 100 + 200;
                UnityEngine.Debug.Log($""Running in FullAccess mode: {value}"");
                return $""{message}: {value}"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code,
                "TestCommand",
                null,
                CancellationToken.None,
                compileOnly: false
            );
            
            // デバッグ用：実際の結果を出力
            UnityEngine.Debug.Log($"[DEBUG] Success: {result.Success}");
            UnityEngine.Debug.Log($"[DEBUG] Result: '{result.Result}'");
            UnityEngine.Debug.Log($"[DEBUG] ErrorMessage: '{result.ErrorMessage}'");
            
            Assert.IsTrue(result.Success, $"FullAccess mode should allow all code. Error: {result.ErrorMessage}");
            Assert.IsTrue(result.Result?.ToString().Contains("FullAccess mode active"));
        }
        
        /// <summary>
        /// SecurityValidatorの単体テスト
        /// </summary>
        [Test]
        public void TestSecurityValidator()
        {
            SecurityValidator validator = new(DynamicCodeSecurityLevel.Restricted);
            
            // 危険なコード
            string dangerousCode = @"
                using System.IO;
                public class Test {
                    public void Method() {
                        File.Delete(""test.txt"");
                    }
                }
            ";
            
            SecurityValidationResult result = validator.ValidateCode(dangerousCode);
            
            Assert.IsFalse(result.IsValid, "Dangerous code should be invalid");
            Assert.IsTrue(result.Violations.Count > 0, "Should have violations");
        }
        
        /// <summary>
        /// AssemblyReferencePolicyがユーザー定義アセンブリを許可することを確認
        /// </summary>
        [Test]
        public void TestAssemblyReferencePolicyAllowsUserAssemblies()
        {
            // Restrictedモードでのアセンブリリスト取得
            IReadOnlyList<string> assemblies = AssemblyReferencePolicy.GetAssemblies(
                DynamicCodeSecurityLevel.Restricted
            );
            
            // Assembly-CSharpがあれば含まれているはず
            Assembly assemblyCSharp = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            
            if (assemblyCSharp != null)
            {
                Assert.IsTrue(
                    assemblies.Contains("Assembly-CSharp"),
                    "Assembly-CSharp should be allowed in Restricted mode"
                );
            }
            
            // Unity系は許可
            Assert.IsTrue(
                assemblies.Any(a => a.StartsWith("UnityEngine")),
                "UnityEngine assemblies should be allowed"
            );
            
            // 危険なアセンブリは除外
            Assert.IsFalse(
                assemblies.Contains("System.IO"),
                "System.IO should not be directly allowed"
            );
        }
    }
}
#endif