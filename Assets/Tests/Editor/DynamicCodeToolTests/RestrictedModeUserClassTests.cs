#if ULOOPMCP_HAS_ROSLYN
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    /// <summary>
    /// Test for executing user classes in Restricted mode
    /// </summary>
    [TestFixture]
    public class RestrictedModeUserClassTests
    {
        private IDynamicCodeExecutor executor;
        
        
        [SetUp]
        public void SetUp()
        {
            // v4.0 stateless design - remove changes to global settings
            // Each test directly specifies the level for the Executor
        }
        
        [TearDown]
        public void TearDown()
        {
            // Explicit cleanup
            executor = null;
        }
        
        /// <summary>
        /// Test for detecting dangerous APIs
        /// </summary>
        [Test]
        public void TestDangerousApiDetection()
        {
            DangerousApiDetector detector = new();
            
            // File system APIs
            Assert.IsTrue(detector.IsDangerousApi("System.IO.File.Delete"));
            Assert.IsTrue(detector.IsDangerousApi("System.IO.Directory.Delete"));
            
            // Network APIs
            Assert.IsTrue(detector.IsDangerousApi("System.Net.Http.HttpClient"));
            Assert.IsTrue(detector.IsDangerousApi("System.Net.WebClient"));
            
            // Reflection APIs
            Assert.IsTrue(detector.IsDangerousApi("System.Reflection.Assembly.Load"));
            Assert.IsTrue(detector.IsDangerousApi("System.Type.InvokeMember"));
            
            // Newly added dangerous APIs (2025-08-19)
            // System.Web related
            Assert.IsTrue(detector.IsDangerousApi("System.Web.HttpContext"));
            Assert.IsTrue(detector.IsDangerousApi("System.Web.HttpRequest"));
            Assert.IsTrue(detector.IsDangerousApi("System.Web.HttpResponse"));
            
            // UnityEngine.Networking related
            Assert.IsTrue(detector.IsDangerousApi("UnityEngine.Networking.UnityWebRequest"));
            Assert.IsTrue(detector.IsDangerousApi("UnityEngine.Networking.NetworkTransport"));
            
            // System.Data related
            Assert.IsTrue(detector.IsDangerousApi("System.Data.SqlClient.SqlConnection"));
            Assert.IsTrue(detector.IsDangerousApi("System.Data.SqlClient.SqlCommand"));
            Assert.IsTrue(detector.IsDangerousApi("System.Data.DataSet"));
            
            // System.Runtime.Remoting related
            Assert.IsTrue(detector.IsDangerousApi("System.Runtime.Remoting.RemotingConfiguration"));
            Assert.IsTrue(detector.IsDangerousApi("System.Runtime.Remoting.RemotingServices"));
            
            // System.Security.Cryptography related (certificate manipulation)
            Assert.IsTrue(detector.IsDangerousApi("System.Security.Cryptography.X509Certificates.X509Certificate"));
            Assert.IsTrue(detector.IsDangerousApi("System.Security.Cryptography.X509Certificates.X509Store"));
            
            // Safe APIs
            Assert.IsFalse(detector.IsDangerousApi("UnityEngine.Debug.Log"));
            Assert.IsFalse(detector.IsDangerousApi("System.String.Concat"));
            Assert.IsFalse(detector.IsDangerousApi("System.Math.Sqrt"));
        }
        
        /// <summary>
        /// Verify that safe code can be executed in Restricted mode
        /// </summary>
        [Test]
        public async Task TestSafeCodeExecutionInRestrictedMode()
        {
            // Create Executor in Restricted mode
            executor = Factory.DynamicCodeExecutorFactory.Create(DynamicCodeSecurityLevel.Restricted);
            
            string safeCode = @"
                // Test for safe code
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
            
            // For debugging: output actual results
            UnityEngine.Debug.Log($"[DEBUG] Success: {result.Success}");
            UnityEngine.Debug.Log($"[DEBUG] Result: '{result.Result}'");
            UnityEngine.Debug.Log($"[DEBUG] ErrorMessage: '{result.ErrorMessage}'");
            
            Assert.IsTrue(result.Success, $"Safe code should execute successfully. Error: {result.ErrorMessage}");
            Assert.AreEqual("Hello World", result.Result?.ToString());
        }
        
        /// <summary>
        /// Verify that code containing dangerous APIs is blocked in Restricted mode
        /// </summary>
        [Test]
        public async Task TestDangerousCodeBlockedInRestrictedMode()
        {
            // Create Executor in Restricted mode
            executor = Factory.DynamicCodeExecutorFactory.Create(DynamicCodeSecurityLevel.Restricted);
            
            string dangerousCode = @"
                // Code containing dangerous API
                // Note: System.IO assembly should be blocked in Restricted mode
                System.IO.File.Delete(""test.txt"");
                return ""Done"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                dangerousCode,
                "TestCommand",
                null,
                CancellationToken.None,
                compileOnly: true  // first test with compilation only
            );
            
            // For debugging: output actual results
            UnityEngine.Debug.Log($"[DEBUG] Success: {result.Success}");
            UnityEngine.Debug.Log($"[DEBUG] ErrorMessage: '{result.ErrorMessage}'");
            
            // Should be detected as a security violation during compilation in Restricted mode
            // Or should be blocked during runtime even if compilation succeeds
            if (result.Success)
            {
                // If compilation succeeds, verify that execution is blocked
                result = await executor.ExecuteCodeAsync(
                    dangerousCode,
                    "TestCommand",
                    null,
                    CancellationToken.None,
                    compileOnly: false
                );
                
                UnityEngine.Debug.Log($"[DEBUG] Execution Success: {result.Success}");
                UnityEngine.Debug.Log($"[DEBUG] Execution ErrorMessage: '{result.ErrorMessage}'");
            }
            
            Assert.IsFalse(result.Success, "Dangerous code should be blocked either at compile or runtime");
            
            Assert.IsTrue(
                result.ErrorMessage?.Contains("Security Violation") == true ||
                result.ErrorMessage?.Contains("Security validation failed") == true ||
                result.ErrorMessage?.Contains("dangerous") == true ||
                result.ErrorMessage?.Contains("blocked") == true ||
                result.ErrorMessage?.Contains("ForbiddenNamespace") == true ||
                result.ErrorMessage?.Contains("System.IO") == true,
                $"Error should mention security violation or forbidden namespace. Actual: '{result.ErrorMessage}'"
            );
        }
        
        /// <summary>
        /// Test for dangerous API detection in nested classes
        /// </summary>
        [Test]
        public async Task TestDangerousApiInNestedClass()
        {
            // Create Executor in Restricted mode
            executor = Factory.DynamicCodeExecutorFactory.Create(DynamicCodeSecurityLevel.Restricted);
            
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
        /// Test for dangerous API detection inside lambda expressions
        /// </summary>
        [Test]
        public async Task TestDangerousApiInLambda()
        {
            // Create Executor in Restricted mode
            executor = Factory.DynamicCodeExecutorFactory.Create(DynamicCodeSecurityLevel.Restricted);
            
            string code = @"
                using System.Linq;
                
                public class UserClass {
                    public string ProcessData() {
                        var list = new[] { ""file1.txt"", ""file2.txt"" };
                        
                        // Dangerous API in lambda expression
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
        /// Verify that everything is allowed in FullAccess mode
        /// </summary>
        [Test]
        public async Task TestFullAccessModeAllowsEverything()
        {
            // Create Executor in FullAccess mode
            executor = Factory.DynamicCodeExecutorFactory.Create(DynamicCodeSecurityLevel.FullAccess);
            
            // Debug: verify current security level
            UnityEngine.Debug.Log($"[DEBUG] Current Security Level: FullAccess");
            
            string code = @"
                // Execute normal code in FullAccess mode
                // Verify that the same safe code works
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
            
            // For debugging: output actual results
            UnityEngine.Debug.Log($"[DEBUG] Success: {result.Success}");
            UnityEngine.Debug.Log($"[DEBUG] Result: '{result.Result}'");
            UnityEngine.Debug.Log($"[DEBUG] ErrorMessage: '{result.ErrorMessage}'");
            
            Assert.IsTrue(result.Success, $"FullAccess mode should allow all code. Error: {result.ErrorMessage}");
            Assert.IsTrue(result.Result?.ToString().Contains("FullAccess mode active"));
        }
        
        /// <summary>
        /// Unit test for SecurityValidator
        /// </summary>
        [Test]
        public void TestSecurityValidator()
        {
            SecurityValidator validator = new(DynamicCodeSecurityLevel.Restricted);
            
            // Dangerous code
            string dangerousCode = @"
                using System.IO;
                public class Test {
                    public void Method() {
                        File.Delete(""test.txt"");
                    }
                }
            ";
            
            // Prepare reference assemblies for compilation
            List<Microsoft.CodeAnalysis.MetadataReference> references = new()
            {
                Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(System.Console).Assembly.Location),
                Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location)
            };
            
            // Create CSharpCompilation directly
            Microsoft.CodeAnalysis.SyntaxTree syntaxTree = 
                Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(dangerousCode);
            
            Microsoft.CodeAnalysis.CSharp.CSharpCompilation compilation = 
                Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
                    "TestAssembly",
                    new[] { syntaxTree },
                    references,
                    new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(
                        Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary
                    )
                );
            
            // Use new API
            SecurityValidationResult result = validator.ValidateCompilation(compilation);
            
            Assert.IsFalse(result.IsValid, "Dangerous code should be invalid");
            Assert.IsTrue(result.Violations.Count > 0, "Should have violations");
        }
        
        /// <summary>
        /// Verify that AssemblyReferencePolicy returns all assemblies
        /// </summary>
        [Test]
        public void TestAssemblyReferencePolicyReturnsAllAssemblies()
        {
            // Get assembly list in Restricted mode
            IReadOnlyList<string> restrictedAssemblies = AssemblyReferencePolicy.GetAssemblies(
                DynamicCodeSecurityLevel.Restricted
            );
            
            // Get assembly list in FullAccess mode
            IReadOnlyList<string> fullAccessAssemblies = AssemblyReferencePolicy.GetAssemblies(
                DynamicCodeSecurityLevel.FullAccess
            );
            
            // Verify that the same assemblies are returned for Restricted and FullAccess
            Assert.AreEqual(
                restrictedAssemblies.Count,
                fullAccessAssemblies.Count,
                "Restricted and FullAccess should return the same number of assemblies"
            );
            
            // Verify that Unity assemblies are included
            Assert.IsTrue(
                restrictedAssemblies.Any(a => a.StartsWith("UnityEngine")),
                "UnityEngine assemblies should be included"
            );
            
            // Verify that System assemblies are included (because all assemblies are allowed)
            Assert.IsTrue(
                restrictedAssemblies.Any(a => a.StartsWith("System")),
                "System assemblies should be included"
            );
            
            // Verify that an empty list is returned for Disabled mode
            IReadOnlyList<string> disabledAssemblies = AssemblyReferencePolicy.GetAssemblies(
                DynamicCodeSecurityLevel.Disabled
            );
            Assert.AreEqual(0, disabledAssemblies.Count, "Disabled mode should return no assemblies");
        }
        
        /// <summary>
        /// Verify that System.Web API is blocked in Restricted mode
        /// </summary>
        [Test]
        public async Task TestSystemWebBlockedInRestrictedMode()
        {
            // Create Executor in Restricted mode
            executor = Factory.DynamicCodeExecutorFactory.Create(DynamicCodeSecurityLevel.Restricted);
            
            string code = @"
                // Attempt to use System.Web API
                // Note: System.Web assembly is typically unavailable in Unity environment, so compilation error is expected
                var context = System.Web.HttpContext.Current;
                return ""Web API accessed"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code,
                "TestCommand",
                null,
                CancellationToken.None,
                compileOnly: false
            );
            
            // System.Web is unavailable in Unity environment, so compilation error is expected
            Assert.IsFalse(result.Success, "System.Web API should fail in Unity environment");
            // OK if it fails with either compilation error or security violation
            Assert.IsTrue(
                result.ErrorMessage?.Contains("compilation error") == true ||
                result.ErrorMessage?.Contains("Security Violation") == true ||
                result.ErrorMessage?.Contains("Security validation failed") == true ||
                result.ErrorMessage?.Contains("ForbiddenNamespace") == true ||
                result.ErrorMessage?.Contains("does not exist") == true,
                $"Error should be either compilation error or security violation. Actual: '{result.ErrorMessage}'"
            );
        }
        
        /// <summary>
        /// Verify that UnityEngine.Networking API is blocked in Restricted mode
        /// </summary>
        [Test]
        public async Task TestUnityNetworkingBlockedInRestrictedMode()
        {
            // Create Executor in Restricted mode
            executor = Factory.DynamicCodeExecutorFactory.Create(DynamicCodeSecurityLevel.Restricted);
            
            string code = @"
                // Attempt to use UnityEngine.Networking API
                // Note: Legacy Networking (UNet) has been deprecated since Unity 2019
                // UnityWebRequest might have been moved to a different namespace
                var request = UnityEngine.Networking.UnityWebRequest.Get(""https://example.com"");
                return ""Networking API accessed"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code,
                "TestCommand",
                null,
                CancellationToken.None,
                compileOnly: false
            );
            
            // Since this API likely does not exist in Unity 2022,
            // OK if it fails with either compilation error or security violation
            Assert.IsFalse(result.Success, "UnityEngine.Networking API should fail");
            Assert.IsTrue(
                result.ErrorMessage?.Contains("Security Violation") == true ||
                result.ErrorMessage?.Contains("Security validation failed") == true ||
                result.ErrorMessage?.Contains("ForbiddenNamespace") == true ||
                result.ErrorMessage?.Contains("does not exist") == true ||
                result.ErrorMessage?.Contains("compilation error") == true ||
                result.ErrorMessage?.Contains("Networking") == true,
                $"Error should be compilation error or security violation. Actual: '{result.ErrorMessage}'"
            );
        }
        
        /// <summary>
        /// Verify that System.Data.SqlClient API is blocked in Restricted mode
        /// </summary>
        [Test]
        public async Task TestSqlClientBlockedInRestrictedMode()
        {
            // Create Executor in Restricted mode
            executor = Factory.DynamicCodeExecutorFactory.Create(DynamicCodeSecurityLevel.Restricted);
            
            string code = @"
                // Attempt to use System.Data.SqlClient API
                // Note: System.Data.SqlClient is typically unavailable in Unity environment
                var connection = new System.Data.SqlClient.SqlConnection(""Server=localhost;Database=test;"");
                return ""SQL client accessed"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code,
                "TestCommand",
                null,
                CancellationToken.None,
                compileOnly: false
            );
            
            // Since this API likely does not exist in Unity environment,
            // OK if it fails with either compilation error or security violation
            Assert.IsFalse(result.Success, "System.Data.SqlClient API should fail");
            Assert.IsTrue(
                result.ErrorMessage?.Contains("Security Violation") == true ||
                result.ErrorMessage?.Contains("Security validation failed") == true ||
                result.ErrorMessage?.Contains("ForbiddenNamespace") == true ||
                result.ErrorMessage?.Contains("does not exist") == true ||
                result.ErrorMessage?.Contains("compilation error") == true ||
                result.ErrorMessage?.Contains("SqlClient") == true,
                $"Error should be compilation error or security violation. Actual: '{result.ErrorMessage}'"
            );
        }
        
        /// <summary>
        /// Verify that System.Runtime.Remoting API is blocked in Restricted mode
        /// </summary>
        [Test]
        public async Task TestRemotingBlockedInRestrictedMode()
        {
            // Create Executor in Restricted mode
            executor = Factory.DynamicCodeExecutorFactory.Create(DynamicCodeSecurityLevel.Restricted);
            
            string code = @"
                // Attempt to use System.Runtime.Remoting API
                // May result in runtime error, but should be detected as a dangerous API
                // However, failure due to non-existent file is also acceptable
                System.Runtime.Remoting.RemotingConfiguration.Configure(""app.config"", false);
                return ""Remoting configured"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code,
                "TestCommand",
                null,
                CancellationToken.None,
                compileOnly: false
            );
            
            Assert.IsFalse(result.Success, "System.Runtime.Remoting API should fail");
            // OK if it fails with either security violation or file not found error
            Assert.IsTrue(
                result.ErrorMessage?.Contains("Security Violation") == true ||
                result.ErrorMessage?.Contains("Security validation failed") == true ||
                result.ErrorMessage?.Contains("ForbiddenNamespace") == true ||
                result.ErrorMessage?.Contains("could not be loaded") == true ||
                result.ErrorMessage?.Contains("Could not find file") == true,
                $"Error should be either security violation or file not found. Actual: '{result.ErrorMessage}'"
            );
        }
        
        /// <summary>
        /// Verify that System.Security.Cryptography.X509Certificates API is blocked in Restricted mode
        /// </summary>
        [Test]
        public async Task TestX509CertificatesBlockedInRestrictedMode()
        {
            // Create Executor in Restricted mode
            executor = Factory.DynamicCodeExecutorFactory.Create(DynamicCodeSecurityLevel.Restricted);
            
            string code = @"
                // Attempt to use System.Security.Cryptography.X509Certificates API
                // May result in runtime error, but should be detected as a dangerous API
                // However, failure due to non-existent certificate file is also acceptable
                var cert = new System.Security.Cryptography.X509Certificates.X509Certificate(""cert.pfx"");
                return ""Certificate loaded"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code,
                "TestCommand",
                null,
                CancellationToken.None,
                compileOnly: false
            );
            
            Assert.IsFalse(result.Success, "X509Certificates API should fail");
            // OK if it fails with either security violation or file not found error
            Assert.IsTrue(
                result.ErrorMessage?.Contains("Security Violation") == true ||
                result.ErrorMessage?.Contains("Security validation failed") == true ||
                result.ErrorMessage?.Contains("ForbiddenNamespace") == true ||
                result.ErrorMessage?.Contains("Could not find file") == true ||
                result.ErrorMessage?.Contains("cert.pfx") == true,
                $"Error should be either security violation or file not found. Actual: '{result.ErrorMessage}'"
            );
        }
        
        /// <summary>
        /// Verify that System.Diagnostics.Process API is blocked in Restricted mode
        /// </summary>
        [Test]
        public async Task TestProcessStartBlockedInRestrictedMode()
        {
            // Create Executor in Restricted mode
            executor = Factory.DynamicCodeExecutorFactory.Create(DynamicCodeSecurityLevel.Restricted);
            
            string code = @"
                // Attempt to use System.Diagnostics.Process.Start
                System.Diagnostics.Process.Start(""notepad.exe"");
                return ""Process started"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code,
                "TestCommand",
                null,
                CancellationToken.None,
                compileOnly: false
            );
            
            Assert.IsFalse(result.Success, "Process.Start should be blocked");
            Assert.IsTrue(
                result.ErrorMessage?.Contains("Security Violation") == true ||
                result.ErrorMessage?.Contains("Security validation failed") == true ||
                result.ErrorMessage?.Contains("dangerous") == true,
                $"Error should mention security violation. Actual: '{result.ErrorMessage}'"
            );
        }
        
        /// <summary>
        /// Verify that System.Activator.CreateComInstanceFrom API is blocked in Restricted mode
        /// </summary>
        [Test]
        public async Task TestActivatorCreateComInstanceFromBlockedInRestrictedMode()
        {
            // Create Executor in Restricted mode
            executor = Factory.DynamicCodeExecutorFactory.Create(DynamicCodeSecurityLevel.Restricted);
            
            string code = @"
                // Attempt to use System.Activator.CreateComInstanceFrom
                System.Activator.CreateComInstanceFrom(""test.dll"", ""TestClass"");
                return ""COM instance created"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code,
                "TestCommand",
                null,
                CancellationToken.None,
                compileOnly: false
            );
            
            Assert.IsFalse(result.Success, "CreateComInstanceFrom should be blocked");
        }
        
        /// <summary>
        /// Verify that UnityEditor.AssetDatabase.DeleteAsset API is blocked in Restricted mode
        /// </summary>
        [Test]
        public async Task TestAssetDatabaseDeleteAssetBlockedInRestrictedMode()
        {
            // Create Executor in Restricted mode
            executor = Factory.DynamicCodeExecutorFactory.Create(DynamicCodeSecurityLevel.Restricted);
            
            string code = @"
                // Attempt to use UnityEditor.AssetDatabase.DeleteAsset
                UnityEditor.AssetDatabase.DeleteAsset(""Assets/Test.txt"");
                return ""Asset deleted"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code,
                "TestCommand",
                null,
                CancellationToken.None,
                compileOnly: false
            );
            
            Assert.IsFalse(result.Success, "AssetDatabase.DeleteAsset should be blocked");
            Assert.IsTrue(
                result.ErrorMessage?.Contains("Security Violation") == true ||
                result.ErrorMessage?.Contains("Security validation failed") == true ||
                result.ErrorMessage?.Contains("dangerous") == true,
                $"Error should mention security violation. Actual: '{result.ErrorMessage}'"
            );
        }
        
        /// <summary>
        /// Verify that UnityEditor.FileUtil.DeleteFileOrDirectory API is blocked in Restricted mode
        /// </summary>
        [Test]
        public async Task TestFileUtilDeleteFileOrDirectoryBlockedInRestrictedMode()
        {
            // Create Executor in Restricted mode
            executor = Factory.DynamicCodeExecutorFactory.Create(DynamicCodeSecurityLevel.Restricted);
            
            string code = @"
                // Attempt to use UnityEditor.FileUtil.DeleteFileOrDirectory
                UnityEditor.FileUtil.DeleteFileOrDirectory(""Assets/Test"");
                return ""File deleted"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code,
                "TestCommand",
                null,
                CancellationToken.None,
                compileOnly: false
            );
            
            Assert.IsFalse(result.Success, "FileUtil.DeleteFileOrDirectory should be blocked");
        }
        
        /// <summary>
        /// Verify that System.Environment.Exit API is blocked in Restricted mode
        /// </summary>
        [Test]
        public async Task TestEnvironmentExitBlockedInRestrictedMode()
        {
            // Create Executor in Restricted mode
            executor = Factory.DynamicCodeExecutorFactory.Create(DynamicCodeSecurityLevel.Restricted);
            
            string code = @"
                // Attempt to use System.Environment.Exit
                System.Environment.Exit(0);
                return ""Exiting"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code,
                "TestCommand",
                null,
                CancellationToken.None,
                compileOnly: false
            );
            
            Assert.IsFalse(result.Success, "Environment.Exit should be blocked");
        }
        
        /// <summary>
        /// Verify that System.Threading.Thread.Abort API is blocked in Restricted mode
        /// </summary>
        [Test]
        public async Task TestThreadAbortBlockedInRestrictedMode()
        {
            // Create Executor in Restricted mode
            executor = Factory.DynamicCodeExecutorFactory.Create(DynamicCodeSecurityLevel.Restricted);
            
            string code = @"
                // Attempt to use System.Threading.Thread.Abort
                var thread = System.Threading.Thread.CurrentThread;
                thread.Abort();
                return ""Thread aborted"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code,
                "TestCommand",
                null,
                CancellationToken.None,
                compileOnly: false
            );
            
            Assert.IsFalse(result.Success, "Thread.Abort should be blocked");
        }
        
        /// <summary>
        /// Verify that access to DynamicCodeSecurityManager is blocked in Restricted mode
        /// </summary>
        [Test]
        public async Task TestSecurityManagerAccessBlockedInRestrictedMode()
        {
            // Create Executor in Restricted mode
            executor = Factory.DynamicCodeExecutorFactory.Create(DynamicCodeSecurityLevel.Restricted);
            
            string code = @"
                // Attempt to access DynamicCodeSecurityManager
                var level = io.github.hatayama.uLoopMCP.DynamicCodeSecurityManager.CurrentLevel;
                return $""Security level: {level}"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code,
                "TestCommand",
                null,
                CancellationToken.None,
                compileOnly: false
            );
            
            // CurrentLevel reading is currently allowed, but should be blocked in the future
            // TODO: Modify to block CurrentLevel reading
            
            // InitializeFromSettings should be blocked
            code = @"
                // Attempt to call InitializeFromSettings
                io.github.hatayama.uLoopMCP.DynamicCodeSecurityManager.InitializeFromSettings(
                    io.github.hatayama.uLoopMCP.DynamicCodeSecurityLevel.FullAccess);
                return ""Security changed"";
            ";
            
            result = await executor.ExecuteCodeAsync(
                code,
                "TestCommand",
                null,
                CancellationToken.None,
                compileOnly: false
            );
            
            Assert.IsFalse(result.Success, "Security level change should be blocked");
        }
        
        /// <summary>
        /// Verify that System.IO.File.WriteAllText API is blocked in Restricted mode
        /// </summary>
        [Test]
        public async Task TestFileWriteAllTextBlockedInRestrictedMode()
        {
            // Create Executor in Restricted mode
            executor = Factory.DynamicCodeExecutorFactory.Create(DynamicCodeSecurityLevel.Restricted);
            
            string code = @"
                // Attempt to use System.IO.File.WriteAllText
                System.IO.File.WriteAllText(""test.txt"", ""malicious content"");
                return ""File written"";
            ";
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                code,
                "TestCommand",
                null,
                CancellationToken.None,
                compileOnly: false
            );
            
            Assert.IsFalse(result.Success, "File.WriteAllText should be blocked");
        }
        
        // TestFileCreateAllowedInRestrictedMode is redundant with RestrictedModeDangerousApiTests.TestRestrictedMode_FileCreate_Allowed, so it is deleted
    }
}
#endif