#if ULOOPMCP_HAS_ROSLYN
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    public class ExecuteDynamicCodeNetworkAwaitTests
    {

        [Test]
        public async Task Await_HttpClient_FullAccess_Succeeds()
        {
            IDynamicCodeExecutor executor = Factory.DynamicCodeExecutorFactory.Create(
                DynamicCodeSecurityLevel.FullAccess
            );

            using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            string code = @"using System; using System.Net.Http; using System.Threading.Tasks;
using (var client = new HttpClient())
{
    string html = await client.GetStringAsync(""https://example.com"");
    return html.Contains(""Example Domain"") ? ""OK"" : html;
}";

            ExecutionResult result = await executor.ExecuteCodeAsync(
                code,
                "DynamicCommand",
                null,
                cts.Token,
                false
            );

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual("OK", result.Result);
        }

        [Test]
        public async Task Await_HttpClient_Restricted_Blocked()
        {
            IDynamicCodeExecutor executor = Factory.DynamicCodeExecutorFactory.Create(
                DynamicCodeSecurityLevel.Restricted
            );

            using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            string code = @"using System; using System.Net.Http; using System.Threading.Tasks;
using (var client = new HttpClient())
{
    string html = await client.GetStringAsync(""https://example.com"");
    return html;
}";

            ExecutionResult result = await executor.ExecuteCodeAsync(
                code,
                "DynamicCommand",
                null,
                cts.Token,
                false
            );

            Assert.IsFalse(result.Success, "Network access should be blocked in Restricted level");
        }
    }
}
#endif


