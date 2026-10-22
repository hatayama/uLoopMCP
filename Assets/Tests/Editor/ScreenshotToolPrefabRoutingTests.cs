using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Verifies the screenshot tool sends requests with a prefab path to the prefab capture.
    /// </summary>
    public sealed class ScreenshotToolPrefabRoutingTests
    {
        /// <summary>
        /// Verifies a request with PrefabPath reaches the prefab capture, which rejects a missing asset before rendering.
        /// </summary>
        [Test]
        public void ExecuteAsync_WithAPrefabPath_RoutesToThePrefabCapture()
        {
            ScreenshotTool tool = new();

            Task<UnityCliLoopToolResponse> task = tool.ExecuteAsync(
                JObject.Parse("{\"PrefabPath\":\"Assets/Nope.prefab\"}"),
                CancellationToken.None);

            Assert.That(task.IsFaulted, Is.True);
            Assert.That(task.Exception.InnerException, Is.TypeOf<UnityCliLoopToolParameterValidationException>());
            Assert.That(task.Exception.InnerException.Message, Does.Contain("PrefabPath does not point to a prefab or model asset"));
        }
    }
}
