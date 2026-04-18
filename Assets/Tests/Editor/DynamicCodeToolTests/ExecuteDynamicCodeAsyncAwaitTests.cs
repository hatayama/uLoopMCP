#if ULOOPMCP_HAS_ROSLYN
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    [TestFixture]
    public class ExecuteDynamicCodeAsyncAwaitTests
    {
        [Test]
        public async Task ExecuteAsync_WhenSnippetAwaitsTimerDelay_ShouldReturnAwaitedResult()
        {
            DynamicCodeSecurityLevel previousSecurityLevel = ULoopSettings.GetDynamicCodeSecurityLevel();
            ULoopSettings.SetDynamicCodeSecurityLevel(DynamicCodeSecurityLevel.Restricted);
            ExecuteDynamicCodeTool tool = new ExecuteDynamicCodeTool();
            JObject paramsToken = new JObject
            {
                ["Code"] = @"await TimerDelay.Wait(10, ct);
return ""async-ok"";"
            };

            BaseToolResponse baseResponse = null;
            try
            {
                baseResponse = await tool.ExecuteAsync(paramsToken);
            }
            finally
            {
                ULoopSettings.SetDynamicCodeSecurityLevel(previousSecurityLevel);
            }

            ExecuteDynamicCodeResponse response = baseResponse as ExecuteDynamicCodeResponse;

            Assert.IsNotNull(response, "Response should be ExecuteDynamicCodeResponse");
            Assert.That(response.Success, Is.True, response.ErrorMessage);
            Assert.That(response.Result, Is.EqualTo("async-ok"));
        }

        [Test]
        public async Task ExecuteAsync_WhenSnippetAwaitsValueTask_ShouldReturnAwaitedResult()
        {
            DynamicCodeSecurityLevel previousSecurityLevel = ULoopSettings.GetDynamicCodeSecurityLevel();
            ULoopSettings.SetDynamicCodeSecurityLevel(DynamicCodeSecurityLevel.Restricted);
            ExecuteDynamicCodeTool tool = new ExecuteDynamicCodeTool();
            JObject paramsToken = new JObject
            {
                ["Code"] = @"ValueTask<int> pending = new ValueTask<int>(Task.FromResult(42));
await TimerDelay.Wait(10, ct);
int result = await pending;
return result;"
            };

            BaseToolResponse baseResponse = null;
            try
            {
                baseResponse = await tool.ExecuteAsync(paramsToken);
            }
            finally
            {
                ULoopSettings.SetDynamicCodeSecurityLevel(previousSecurityLevel);
            }

            ExecuteDynamicCodeResponse response = baseResponse as ExecuteDynamicCodeResponse;

            Assert.IsNotNull(response, "Response should be ExecuteDynamicCodeResponse");
            Assert.That(response.Success, Is.True, response.ErrorMessage);
            Assert.That(response.Result, Is.EqualTo("42"));
        }
    }
}
#endif
