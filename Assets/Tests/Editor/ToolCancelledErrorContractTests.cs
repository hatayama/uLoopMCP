using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

using io.github.hatayama.UnityCliLoop.Infrastructure;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Freezes the tool_cancelled JSON-RPC error.data frame that the Go CLI decodes into a typed struct.
    /// </summary>
    public sealed class ToolCancelledErrorContractTests
    {
        private const string SharedContractPath = "tests/contracts/tool_cancelled_error_contract.json";

        /// <summary>
        /// Verifies ToolCancelledErrorData emits exactly the error.data fields and values the shared fixture freezes.
        /// </summary>
        [Test]
        public void ToolCancelledErrorData_WhenSerialized_MatchesSharedErrorData()
        {
            JObject expected = ReadSharedErrorData();
            ToolCancelledErrorData errorData = new ToolCancelledErrorData(
                runningToolName: "run-tests",
                message: "'run-tests' was cancelled by uloop cancel.");

            string json = JsonConvert.SerializeObject(
                errorData,
                Formatting.None,
                UnityCliLoopJsonResponseSerializerSettings.Settings);
            JObject actual = JObject.Parse(json);

            Assert.That(JToken.DeepEquals(actual, expected), Is.True, $"Expected {expected} but got {actual}");
        }

        private static JObject ReadSharedErrorData()
        {
            string projectRoot = UnityCliLoopPathResolver.GetProjectRoot();
            string json = File.ReadAllText(Path.Combine(projectRoot, SharedContractPath));
            JObject contract = JObject.Parse(json);
            return (JObject)contract["errorData"]!;
        }
    }
}
