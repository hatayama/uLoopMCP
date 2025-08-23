#if ULOOPMCP_HAS_ROSLYN
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    /// <summary>
    /// Security Feature Integration Test for ExecuteDynamicCode
    /// Directly use DynamicCodeExecutor without using ExecuteDynamicCodeTool
    /// (To maintain test independence)
    /// </summary>
    [TestFixture]
    public class ExecuteDynamicCodeSecurityIntegrationTests
    {
        // Do not use ExecuteDynamicCodeTool (for test independence)
        // Create independent Executor for each test method

        [Test]
        public async Task Level0_VerifyCodeExecutionIsBlocked()
        {
            // Create Executor directly (not dependent on global state)
            IDynamicCodeExecutor executor = Factory.DynamicCodeExecutorFactory.Create(
                DynamicCodeSecurityLevel.Disabled
            );
            
            // Direct execution
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
        public async Task Level1_VerifySystemIOCodeIsBlocked()
        {
            // Create Executor directly in Restricted mode
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
            Assert.IsTrue(result.ErrorMessage.Contains("Security Violation") || 
                         result.ErrorMessage.Contains("dangerous") || 
                         result.ErrorMessage.Contains("blocked"));
        }

        [Test]
        public async Task Level1_VerifyFileUsageCodeIsBlocked()
        {
            // Create Executor directly in Restricted mode
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
            Assert.IsTrue(result.ErrorMessage.Contains("Security Violation") || 
                         result.ErrorMessage.Contains("dangerous") || 
                         result.ErrorMessage.Contains("blocked"));
        }

        [Test]
        public async Task Level1_VerifyHttpClientUsageCodeIsBlocked()
        {
            // Create Executor directly in Restricted mode
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
            Assert.IsTrue(result.ErrorMessage.Contains("Security Violation") || 
                         result.ErrorMessage.Contains("dangerous") || 
                         result.ErrorMessage.Contains("blocked"));
        }

        [Test]
        public async Task Level1_VerifyGameObjectCreationCodeCanExecute()
        {
            // Create Executor directly in Restricted mode
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
        public async Task Level1_VerifyUnityEngineDebugLogCanExecute()
        {
            // Create Executor directly in Restricted mode
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
        public async Task Level2_VerifyCodeExecutionSucceedsWithAllFeaturesEnabled()
        {
            // Create Executor directly in FullAccess mode
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
        public async Task Level2_VerifyFileUsageCodeCanExecute()
        {
            // Create Executor directly in FullAccess mode
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
            Assert.AreEqual(false, System.Convert.ToBoolean(result.Result)); // File should not exist
        }

        [Test]
        public async Task VerifyCompileOnlyFlagWorks()
        {
            // Create Executor directly in Restricted mode
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
            Assert.IsNull(result.Result);  // null since not executed in CompileOnly mode
        }

        [Test]
        public async Task VerifyParameterPassingWorks()
        {
            // Create Executor directly in Restricted mode
            IDynamicCodeExecutor executor = Factory.DynamicCodeExecutorFactory.Create(
                DynamicCodeSecurityLevel.Restricted
            );
            
            // Create parameter array
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
        public async Task VerifyCompilationErrorIsReturnedAppropriately()
        {
            // Create Executor directly in Restricted mode
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
            // "this is invalid C# code" causes multiple compilation errors
            // Examples: "Invalid token", "Identifier expected", "Semicolon required"
            Assert.IsTrue(result.ErrorMessage.Contains("Invalid") || 
                         result.ErrorMessage.Contains("expected") || 
                         result.ErrorMessage.Contains("Error") ||
                         result.ErrorMessage.Contains("Compile"));
        }

        [Test]
        public async Task VerifyRuntimeErrorIsReturnedAppropriately()
        {
            // Create Executor directly in Restricted mode
            IDynamicCodeExecutor executor = Factory.DynamicCodeExecutorFactory.Create(
                DynamicCodeSecurityLevel.Restricted
            );
            
            ExecutionResult result = await executor.ExecuteCodeAsync(
                "int x = 10; int y = 0; return x / y;",  // Division by zero
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