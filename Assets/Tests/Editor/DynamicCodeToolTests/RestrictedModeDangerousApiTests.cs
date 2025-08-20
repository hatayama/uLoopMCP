#if ULOOPMCP_HAS_ROSLYN
using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    /// <summary>
    /// Restrictedモードで危険APIがブロックされ、
    /// 安全APIが許可されることを包括的にテストする
    /// DangerousApiDetectorで定義された全APIの動作確認
    /// </summary>
    [TestFixture]
    public class RestrictedModeDangerousApiTests
    {
        private IDynamicCodeExecutor executor;
        
        // テスト用の一時ディレクトリとアセットパス
        private const string TEST_TEMP_DIR = "TestTemp_RestrictedMode";
        private const string TEST_ASSET_PATH = "Assets/Tests/Editor/DynamicCodeToolTests/Temp/TestTemp_RestrictedMode_temp_asset.asset";
        
        [SetUp]
        public void SetUp()
        {
            // v4.0ステートレス設計 - Executorに直接レベル指定
            // テスト用にエディタ設定をRestrictedに
            McpEditorSettings.SetDynamicCodeSecurityLevel(DynamicCodeSecurityLevel.Restricted);
            
            // 全テストでRestrictedモードを使用
            executor = Factory.DynamicCodeExecutorFactory.Create(
                DynamicCodeSecurityLevel.Restricted
            );
            
            // テスト用ディレクトリを作成
            if (!System.IO.Directory.Exists(TEST_TEMP_DIR))
            {
                System.IO.Directory.CreateDirectory(TEST_TEMP_DIR);
                UnityEngine.Debug.Log($"[RestrictedModeDangerousApiTests] Created test directory: {TEST_TEMP_DIR}");
            }
        }
        
        [TearDown]
        public void TearDown()
        {
            // テスト後のクリーンアップ（ディレクトリごと削除）
            CleanupTestDirectory();
            
            executor = null;
        }
        
        private void CleanupTestDirectory()
        {
            // テスト用ディレクトリを丸ごと削除
            try
            {
                if (System.IO.Directory.Exists(TEST_TEMP_DIR))
                {
                    System.IO.Directory.Delete(TEST_TEMP_DIR, true);
                    UnityEngine.Debug.Log($"[RestrictedModeDangerousApiTests] Cleaned up test directory: {TEST_TEMP_DIR}");
                }
                
                // テスト用アセットファイルをAssetDatabase経由で削除（metaファイルも自動削除される）
                string assetPath = TEST_ASSET_PATH;
                if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
                {
                    UnityEditor.AssetDatabase.DeleteAsset(assetPath);
                    UnityEngine.Debug.Log($"[RestrictedModeDangerousApiTests] Deleted asset: {assetPath}");
                }
                
                // Tempフォルダも削除（中身が空の場合）
                string tempFolder = "Assets/Tests/Editor/DynamicCodeToolTests/Temp";
                if (UnityEditor.AssetDatabase.IsValidFolder(tempFolder))
                {
                    // フォルダが空の場合のみ削除
                    string[] assets = UnityEditor.AssetDatabase.FindAssets("", new[] { tempFolder });
                    if (assets.Length == 0)
                    {
                        UnityEditor.AssetDatabase.DeleteAsset(tempFolder);
                        UnityEngine.Debug.Log($"[RestrictedModeDangerousApiTests] Deleted temp folder: {tempFolder}");
                    }
                }
            }
            catch (Exception ex)
            {
                // クリーンアップエラーは警告として記録
                UnityEngine.Debug.LogWarning($"[RestrictedModeDangerousApiTests] Failed to delete test directory: {ex.Message}");
            }
        }
        
        // ================================================================================
        // System.IO.File テスト
        // ================================================================================
        
        #region System.IO.File - 危険API（ブロックされるべき）
        
        [Test]
        public async Task TestRestrictedMode_FileDelete_Blocked()
        {
            string code = @"
                System.IO.File.Delete(""test.txt"");
                return ""File deleted"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            Assert.IsFalse(result.Success, "File.Delete should be blocked in Restricted mode");
            StringAssert.Contains("Dangerous", result.ErrorMessage, "Should report security violation");
        }
        
        [Test]
        public async Task TestRestrictedMode_FileWriteAllText_Blocked()
        {
            string code = @"
                System.IO.File.WriteAllText(""test.txt"", ""malicious content"");
                return ""File written"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            Assert.IsFalse(result.Success, "File.WriteAllText should be blocked");
            StringAssert.Contains("Dangerous", result.ErrorMessage);
        }
        
        [Test]
        public async Task TestRestrictedMode_FileWriteAllBytes_Blocked()
        {
            string code = @"
                System.IO.File.WriteAllBytes(""test.bin"", new byte[] { 0x00, 0x01 });
                return ""Bytes written"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            Assert.IsFalse(result.Success, "File.WriteAllBytes should be blocked");
            StringAssert.Contains("Dangerous", result.ErrorMessage);
        }
        
        [Test]
        public async Task TestRestrictedMode_FileReplace_Blocked()
        {
            string code = @"
                System.IO.File.Replace(""source.txt"", ""dest.txt"", ""backup.txt"");
                return ""File replaced"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            Assert.IsFalse(result.Success, "File.Replace should be blocked");
            StringAssert.Contains("Dangerous", result.ErrorMessage);
        }
        
        #endregion
        
        #region System.IO.File - 安全API（許可されるべき）
        
        [Test]
        public async Task TestRestrictedMode_FileCreate_Allowed()
        {
            string code = $@"
                using (var stream = System.IO.File.Create(""{TEST_TEMP_DIR}/temp_test.txt""))
                {{
                    byte[] data = System.Text.Encoding.UTF8.GetBytes(""test"");
                    stream.Write(data, 0, data.Length);
                }}
                return ""File created successfully"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            Assert.IsTrue(result.Success, $"File.Create should be allowed. Error: {result.ErrorMessage}");
            StringAssert.Contains("successfully", result.Result?.ToString() ?? "");
        }
        
        [Test]
        public async Task TestRestrictedMode_FileCopy_Allowed()
        {
            string code = @"
                // Note: Copy requires source file to exist
                if (System.IO.File.Exists(""source.txt""))
                {
                    System.IO.File.Copy(""source.txt"", ""dest.txt"");
                    return ""File copied"";
                }
                return ""Source file not found (expected in test)"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            Assert.IsTrue(result.Success, $"File.Copy should be allowed. Error: {result.ErrorMessage}");
        }
        
        [Test]
        public async Task TestRestrictedMode_FileReadAllText_Allowed()
        {
            string code = $@"
                // Create a file first
                using (var stream = System.IO.File.Create(""{TEST_TEMP_DIR}/read_test.txt""))
                {{
                    byte[] data = System.Text.Encoding.UTF8.GetBytes(""test content"");
                    stream.Write(data, 0, data.Length);
                }}
                
                // Now read it
                string content = System.IO.File.ReadAllText(""{TEST_TEMP_DIR}/read_test.txt"");
                return $""Read content: {{content}}"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            Assert.IsTrue(result.Success, $"File.ReadAllText should be allowed. Error: {result.ErrorMessage}");
            StringAssert.Contains("test content", result.Result?.ToString() ?? "");
        }
        
        [Test]
        public async Task TestRestrictedMode_FileExists_Allowed()
        {
            string code = @"
                bool exists = System.IO.File.Exists(""any_file.txt"");
                return $""File.Exists executed: {exists}"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            Assert.IsTrue(result.Success, $"File.Exists should be allowed. Error: {result.ErrorMessage}");
            StringAssert.Contains("File.Exists executed", result.Result?.ToString() ?? "");
        }
        
        #endregion
        
        // ================================================================================
        // System.IO.Directory テスト
        // ================================================================================
        
        #region System.IO.Directory - 危険API
        
        [Test]
        public async Task TestRestrictedMode_DirectoryDelete_Blocked()
        {
            string code = @"
                System.IO.Directory.Delete(""test_dir"");
                return ""Directory deleted"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            Assert.IsFalse(result.Success, "Directory.Delete should be blocked");
            StringAssert.Contains("Dangerous", result.ErrorMessage);
        }
        
        #endregion
        
        #region System.IO.Directory - 安全API
        
        [Test]
        public async Task TestRestrictedMode_DirectoryCreate_Allowed()
        {
            string code = $@"
                System.IO.Directory.CreateDirectory(""{TEST_TEMP_DIR}/test_directory"");
                return ""Directory created"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            Assert.IsTrue(result.Success, $"Directory.CreateDirectory should be allowed. Error: {result.ErrorMessage}");
        }
        
        [Test]
        public async Task TestRestrictedMode_DirectoryGetFiles_Allowed()
        {
            string code = @"
                string[] files = System.IO.Directory.GetFiles(""."");
                return $""Found {files.Length} files"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            Assert.IsTrue(result.Success, $"Directory.GetFiles should be allowed. Error: {result.ErrorMessage}");
            StringAssert.Contains("files", result.Result?.ToString() ?? "");
        }
        
        [Test]
        public async Task TestRestrictedMode_DirectoryExists_Allowed()
        {
            string code = @"
                bool exists = System.IO.Directory.Exists(""."");
                return $""Directory.Exists executed: {exists}"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            Assert.IsTrue(result.Success, $"Directory.Exists should be allowed. Error: {result.ErrorMessage}");
        }
        
        #endregion
        
        // ================================================================================
        // System.Diagnostics.Process テスト
        // ================================================================================
        
        #region System.Diagnostics.Process - 危険API
        
        [Test]
        public async Task TestRestrictedMode_ProcessStart_Blocked()
        {
            string code = @"
                System.Diagnostics.Process.Start(""notepad.exe"");
                return ""Process started"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            Assert.IsFalse(result.Success, "Process.Start should be blocked");
            StringAssert.Contains("Dangerous", result.ErrorMessage);
        }
        
        [Test]
        public async Task TestRestrictedMode_ProcessKill_Blocked()
        {
            string code = @"
                var process = System.Diagnostics.Process.GetCurrentProcess();
                process.Kill();
                return ""Process killed"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            Assert.IsFalse(result.Success, "Process.Kill should be blocked");
            StringAssert.Contains("Dangerous", result.ErrorMessage);
        }
        
        #endregion
        
        #region System.Diagnostics.Process - 安全API
        
        [Test]
        public async Task TestRestrictedMode_ProcessGetCurrentProcess_Allowed()
        {
            string code = @"
                var process = System.Diagnostics.Process.GetCurrentProcess();
                return $""Current process ID: {process.Id}"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            Assert.IsTrue(result.Success, $"Process.GetCurrentProcess should be allowed. Error: {result.ErrorMessage}");
            StringAssert.Contains("process ID", result.Result?.ToString() ?? "");
        }
        
        #endregion
        
        // ================================================================================
        // System.Reflection.Assembly テスト
        // ================================================================================
        
        #region System.Reflection.Assembly - 危険API
        
        [Test]
        public async Task TestRestrictedMode_AssemblyLoad_Blocked()
        {
            string code = @"
                System.Reflection.Assembly.Load(""System"");
                return ""Assembly loaded"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            Assert.IsFalse(result.Success, "Assembly.Load should be blocked");
            StringAssert.Contains("Dangerous", result.ErrorMessage);
        }
        
        [Test]
        public async Task TestRestrictedMode_AssemblyLoadFrom_Blocked()
        {
            string code = @"
                System.Reflection.Assembly.LoadFrom(""test.dll"");
                return ""Assembly loaded from file"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            Assert.IsFalse(result.Success, "Assembly.LoadFrom should be blocked");
            StringAssert.Contains("Dangerous", result.ErrorMessage);
        }
        
        #endregion
        
        #region System.Reflection.Assembly - 安全API
        
        [Test]
        public async Task TestRestrictedMode_AssemblyGetExecutingAssembly_Allowed()
        {
            string code = @"
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                return $""Executing assembly: {assembly.GetName().Name}"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            Assert.IsTrue(result.Success, $"Assembly.GetExecutingAssembly should be allowed. Error: {result.ErrorMessage}");
            StringAssert.Contains("assembly", result.Result?.ToString() ?? "");
        }
        
        #endregion
        
        // ================================================================================
        // System.Environment テスト
        // ================================================================================
        
        #region System.Environment - 危険API
        
        // [Test] - v5.1: Environment.Exitは実行時に実際にプロセス終了するため削除
        // public async Task TestRestrictedMode_EnvironmentExit_Blocked()
        
        // [Test] - v5.1: Environment.FailFastは実行時に実際にプロセス終了するため削除
        // public async Task TestRestrictedMode_EnvironmentFailFast_Blocked()
        
        #endregion
        
        #region System.Environment - 安全API
        
        [Test]
        public async Task TestRestrictedMode_EnvironmentGetEnvironmentVariable_Allowed()
        {
            string code = @"
                string value = System.Environment.GetEnvironmentVariable(""PATH"") ?? ""not set"";
                return $""Environment variable retrieved: {value.Length} chars"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            Assert.IsTrue(result.Success, $"Environment.GetEnvironmentVariable should be allowed. Error: {result.ErrorMessage}");
            StringAssert.Contains("Environment variable", result.Result?.ToString() ?? "");
        }
        
        #endregion
        
        // ================================================================================
        // System.Threading.Thread テスト
        // ================================================================================
        
        #region System.Threading.Thread - 危険API
        
        [Test]
        public async Task TestRestrictedMode_ThreadAbort_Blocked()
        {
            string code = @"
                var thread = System.Threading.Thread.CurrentThread;
                thread.Abort();
                return ""Thread aborted"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            Assert.IsFalse(result.Success, "Thread.Abort should be blocked");
            StringAssert.Contains("Dangerous", result.ErrorMessage);
        }
        
        #endregion
        
        #region System.Threading.Thread - 安全API
        
        [Test]
        public async Task TestRestrictedMode_ThreadCurrentThread_Allowed()
        {
            string code = @"
                var thread = System.Threading.Thread.CurrentThread;
                return $""Current thread ID: {thread.ManagedThreadId}"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            Assert.IsTrue(result.Success, $"Thread.CurrentThread should be allowed. Error: {result.ErrorMessage}");
            StringAssert.Contains("thread ID", result.Result?.ToString() ?? "");
        }
        
        #endregion
        
        // ================================================================================
        // System.Type テスト
        // ================================================================================
        
        #region System.Type - 危険API
        
        [Test]
        public async Task TestRestrictedMode_TypeInvokeMember_Blocked()
        {
            string code = @"
                var type = typeof(string);
                type.InvokeMember(""Concat"", 
                    System.Reflection.BindingFlags.InvokeMethod, 
                    null, null, new object[] { ""a"", ""b"" });
                return ""Method invoked"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            Assert.IsFalse(result.Success, "Type.InvokeMember should be blocked");
            StringAssert.Contains("Dangerous", result.ErrorMessage);
        }
        
        #endregion
        
        #region System.Type - 安全API
        
        [Test]
        public async Task TestRestrictedMode_TypeGetType_Allowed()
        {
            string code = @"
                var type = System.Type.GetType(""System.String"");
                return $""Type retrieved: {type?.Name ?? ""null""}"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            Assert.IsTrue(result.Success, $"Type.GetType should be allowed. Error: {result.ErrorMessage}");
            StringAssert.Contains("Type retrieved", result.Result?.ToString() ?? "");
        }
        
        #endregion
        
        // ================================================================================
        // System.Activator テスト
        // ================================================================================
        
        #region System.Activator - 危険API
        
        [Test]
        public async Task TestRestrictedMode_ActivatorCreateComInstanceFrom_Blocked()
        {
            string code = @"
                System.Activator.CreateComInstanceFrom(""test.dll"", ""TestClass"");
                return ""COM instance created"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            Assert.IsFalse(result.Success, "Activator.CreateComInstanceFrom should be blocked");
            StringAssert.Contains("Dangerous", result.ErrorMessage);
        }
        
        #endregion
        
        #region System.Activator - 安全API
        
        [Test]
        public async Task TestRestrictedMode_ActivatorCreateInstance_Allowed()
        {
            string code = @"
                var obj = System.Activator.CreateInstance(typeof(System.Collections.Generic.List<string>));
                return $""Instance created: {obj.GetType().Name}"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            Assert.IsTrue(result.Success, $"Activator.CreateInstance should be allowed. Error: {result.ErrorMessage}");
            StringAssert.Contains("Instance created", result.Result?.ToString() ?? "");
        }
        
        #endregion
        
        // ================================================================================
        // UnityEditor.AssetDatabase テスト
        // ================================================================================
        
        #region UnityEditor.AssetDatabase - 危険API
        
        [Test]
        public async Task TestRestrictedMode_AssetDatabaseDeleteAsset_Blocked()
        {
            string code = @"
                UnityEditor.AssetDatabase.DeleteAsset(""Assets/test.txt"");
                return ""Asset deleted"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            Assert.IsFalse(result.Success, "AssetDatabase.DeleteAsset should be blocked");
            StringAssert.Contains("Dangerous", result.ErrorMessage);
        }
        
        #endregion
        
        #region UnityEditor.AssetDatabase - 安全API
        
        [Test]
        public async Task TestRestrictedMode_AssetDatabaseCreateAsset_Allowed()
        {
            string code = @"
                // Note: CreateAsset requires a UnityEngine.Object, so we use a ScriptableObject
                var obj = UnityEngine.ScriptableObject.CreateInstance<UnityEngine.ScriptableObject>();
                // Tempフォルダが存在しない場合は作成
                string tempDir = @""Assets/Tests/Editor/DynamicCodeToolTests/Temp"";
                if (!UnityEditor.AssetDatabase.IsValidFolder(tempDir))
                {
                    string parent = @""Assets/Tests/Editor/DynamicCodeToolTests"";
                    UnityEditor.AssetDatabase.CreateFolder(parent, ""Temp"");
                }
                UnityEditor.AssetDatabase.CreateAsset(obj, @""Assets/Tests/Editor/DynamicCodeToolTests/Temp/TestTemp_RestrictedMode_temp_asset.asset"");
                return ""Asset created"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            // CreateAssetは実際にはエディタ機能なので、実行時エラーになる可能性があるが
            // セキュリティ違反ではないはず
            if (!result.Success)
            {
                StringAssert.DoesNotContain("Dangerous", result.ErrorMessage ?? "", 
                    "AssetDatabase.CreateAsset should not be blocked for security reasons");
            }
        }
        
        #endregion
        
        // ================================================================================
        // UnityEditor.FileUtil テスト
        // ================================================================================
        
        #region UnityEditor.FileUtil - 危険API
        
        [Test]
        public async Task TestRestrictedMode_FileUtilDeleteFileOrDirectory_Blocked()
        {
            string code = @"
                UnityEditor.FileUtil.DeleteFileOrDirectory(""test"");
                return ""File or directory deleted"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            Assert.IsFalse(result.Success, "FileUtil.DeleteFileOrDirectory should be blocked");
            StringAssert.Contains("Dangerous", result.ErrorMessage);
        }
        
        #endregion
        
        #region UnityEditor.FileUtil - 安全API
        
        [Test]
        public async Task TestRestrictedMode_FileUtilCopyFileOrDirectory_Allowed()
        {
            string code = $@"
                // まず確実に存在するファイルを作成
                string sourceFile = ""{TEST_TEMP_DIR}/source_file.txt"";
                string destFile = ""{TEST_TEMP_DIR}/dest_file.txt"";
                
                // ディレクトリが存在することを確認
                if (!System.IO.Directory.Exists(""{TEST_TEMP_DIR}""))
                {{
                    System.IO.Directory.CreateDirectory(""{TEST_TEMP_DIR}"");
                }}
                
                // ソースファイルを作成
                using (var stream = System.IO.File.Create(sourceFile))
                {{
                    byte[] data = System.Text.Encoding.UTF8.GetBytes(""test content for copy"");
                    stream.Write(data, 0, data.Length);
                }}
                
                // ファイルが確実に存在することを確認
                if (!System.IO.File.Exists(sourceFile))
                {{
                    return ""Failed to create source file"";
                }}
                
                // FileUtil.CopyFileOrDirectory を実行（これはセキュリティ違反ではないはず）
                UnityEditor.FileUtil.CopyFileOrDirectory(sourceFile, destFile);
                
                // コピーが成功したことを確認
                if (System.IO.File.Exists(destFile))
                {{
                    return ""FileUtil.CopyFileOrDirectory executed successfully"";
                }}
                
                return ""FileUtil.CopyFileOrDirectory API call allowed (copy may have failed for other reasons)"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false
            );
            
            // API呼び出し自体はセキュリティ違反ではないはず
            Assert.IsTrue(
                result.Success || !result.ErrorMessage?.Contains("Dangerous") == true,
                $"FileUtil.CopyFileOrDirectory should not be blocked for security reasons. Error: {result.ErrorMessage}"
            );
        }
        
        #endregion
    }
}
#endif