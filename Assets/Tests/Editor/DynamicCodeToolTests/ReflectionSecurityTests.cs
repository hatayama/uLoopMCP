using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    /// <summary>
    /// Reflection is allowed in Restricted mode, but access to dangerous types is blocked.
    /// typeof(DangerousType) is caught by IL TypeRef inspection (ldtoken).
    /// Type.GetType("string") is caught by source-level pattern scan.
    /// </summary>
    [TestFixture]
    public class ReflectionSecurityTests
    {
        private IDynamicCodeExecutor _executor;

        [SetUp]
        public void SetUp()
        {
            _executor = Factory.DynamicCodeExecutorFactory.Create(DynamicCodeSecurityLevel.Restricted);
        }

        #region Reflection allowed (safe types)

        [Test]
        public async Task Restricted_MethodInfoInvoke_SafeType_Allowed()
        {
            string code = @"
                var method = typeof(UnityEngine.Mathf).GetMethod(""Max"",
                    new System.Type[] { typeof(float), typeof(float) });
                object result = method.Invoke(null, new object[] { 3f, 7f });
                return result;
            ";

            ExecutionResult result = await _executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false);

            Assert.IsTrue(result.Success, $"Safe reflection should succeed. Error: {result.ErrorMessage}");
        }

        [Test]
        public async Task Restricted_TypeGetProperty_SafeType_Allowed()
        {
            string code = @"
                var type = typeof(UnityEngine.Application);
                var prop = type.GetProperty(""unityVersion"");
                return prop.GetValue(null);
            ";

            ExecutionResult result = await _executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false);

            Assert.IsTrue(result.Success, $"Safe type property access via reflection should succeed. Error: {result.ErrorMessage}");
        }

        #endregion

        #region typeof(DangerousType) blocked by IL TypeRef

        [Test]
        public async Task Restricted_TypeofFileInfo_Blocked()
        {
            string code = @"
                var type = typeof(System.IO.FileInfo);
                return type.Name;
            ";

            ExecutionResult result = await _executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false);

            Assert.IsFalse(result.Success, "typeof(System.IO.FileInfo) should be blocked");
        }

        [Test]
        public async Task Restricted_TypeofHttpClient_Blocked()
        {
            string code = @"
                var type = typeof(System.Net.Http.HttpClient);
                return type.Name;
            ";

            ExecutionResult result = await _executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false);

            Assert.IsFalse(result.Success, "typeof(System.Net.Http.HttpClient) should be blocked");
        }

        [Test]
        public async Task Restricted_TypeofSocket_Blocked()
        {
            string code = @"
                var method = typeof(System.Net.Sockets.Socket).GetMethod(""Connect"",
                    new System.Type[] { typeof(string), typeof(int) });
                return ""should not reach here"";
            ";

            ExecutionResult result = await _executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false);

            Assert.IsFalse(result.Success, "typeof(Socket) reflection should be blocked");
        }

        #endregion

        #region Type.GetType("string") blocked by source pattern

        [Test]
        public async Task Restricted_TypeGetType_StringBased_Blocked()
        {
            string code = @"
                var type = Type.GetType(""System.IO.File"");
                return type?.Name ?? ""null"";
            ";

            ExecutionResult result = await _executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false);

            Assert.IsFalse(result.Success, "Type.GetType() should be blocked in Restricted mode");
            StringAssert.Contains("Dangerous", result.ErrorMessage);
        }

        #endregion

        #region Direct dangerous API still blocked

        [Test]
        public async Task Restricted_FileDelete_Direct_StillBlocked()
        {
            string code = @"
                System.IO.File.Delete(""/tmp/test"");
                return ""deleted"";
            ";

            ExecutionResult result = await _executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false);

            Assert.IsFalse(result.Success, "Direct File.Delete should still be blocked");
            StringAssert.Contains("Dangerous", result.ErrorMessage);
        }

        [Test]
        public async Task Restricted_ProcessStart_Direct_StillBlocked()
        {
            string code = @"
                System.Diagnostics.Process.Start(""ls"");
                return ""started"";
            ";

            ExecutionResult result = await _executor.ExecuteCodeAsync(
                code, "TestCommand", null, CancellationToken.None, compileOnly: false);

            Assert.IsFalse(result.Success, "Direct Process.Start should still be blocked");
            StringAssert.Contains("Dangerous", result.ErrorMessage);
        }

        #endregion
    }
}
