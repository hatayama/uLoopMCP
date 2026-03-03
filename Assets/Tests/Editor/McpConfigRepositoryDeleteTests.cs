using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Newtonsoft.Json;

namespace io.github.hatayama.uLoopMCP
{
    [TestFixture]
    public class McpConfigRepositoryDeleteTests
    {
        private McpConfigRepository _repository;
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _repository = new McpConfigRepository();
            _tempDir = Path.Combine(Path.GetTempPath(), "McpConfigDeleteTests_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Test]
        public void Should_DeleteOnlyULoopMCPEntries_And_PreserveOtherServers()
        {
            string configPath = Path.Combine(_tempDir, "mcp.json");
            Dictionary<string, object> config = new()
            {
                ["mcpServers"] = new Dictionary<string, object>
                {
                    ["uLoopMCP"] = new { command = "node", args = new[] { "server.js" }, env = new Dictionary<string, string> { ["UNITY_TCP_PORT"] = "8800" } },
                    ["other-server"] = new { command = "python", args = new[] { "app.py" }, env = new Dictionary<string, string> { ["API_KEY"] = "abc" } }
                }
            };
            File.WriteAllText(configPath, JsonConvert.SerializeObject(config, Formatting.Indented));

            _repository.DeleteULoopMCPEntries(configPath);

            string resultJson = File.ReadAllText(configPath);
            Dictionary<string, object> result = JsonConvert.DeserializeObject<Dictionary<string, object>>(resultJson);
            string serversJson = JsonConvert.SerializeObject(result["mcpServers"]);
            Dictionary<string, object> servers = JsonConvert.DeserializeObject<Dictionary<string, object>>(serversJson);

            Assert.IsFalse(servers.ContainsKey("uLoopMCP"), "uLoopMCP entry should be deleted");
            Assert.IsTrue(servers.ContainsKey("other-server"), "other-server should be preserved");
        }

        [Test]
        public void Should_DeleteWindsurfPortSuffixedEntries()
        {
            string configPath = Path.Combine(_tempDir, "mcp_config.json");
            Dictionary<string, object> config = new()
            {
                ["mcpServers"] = new Dictionary<string, object>
                {
                    ["uLoopMCP-8801"] = new { command = "node", args = new[] { "server.js" }, env = new Dictionary<string, string> { ["UNITY_TCP_PORT"] = "8801" } },
                    ["other-server"] = new { command = "python", args = new[] { "app.py" }, env = new Dictionary<string, string> { ["API_KEY"] = "abc" } }
                }
            };
            File.WriteAllText(configPath, JsonConvert.SerializeObject(config, Formatting.Indented));

            _repository.DeleteULoopMCPEntries(configPath);

            string resultJson = File.ReadAllText(configPath);
            Dictionary<string, object> result = JsonConvert.DeserializeObject<Dictionary<string, object>>(resultJson);
            string serversJson = JsonConvert.SerializeObject(result["mcpServers"]);
            Dictionary<string, object> servers = JsonConvert.DeserializeObject<Dictionary<string, object>>(serversJson);

            Assert.IsFalse(servers.ContainsKey("uLoopMCP-8801"), "uLoopMCP-8801 entry should be deleted");
            Assert.IsTrue(servers.ContainsKey("other-server"), "other-server should be preserved");
        }

        [Test]
        public void Should_NotThrow_WhenFileDoesNotExist()
        {
            string configPath = Path.Combine(_tempDir, "nonexistent.json");

            Assert.DoesNotThrow(() => _repository.DeleteULoopMCPEntries(configPath));
        }

        [Test]
        public void Should_NotModifyFile_WhenNoULoopMCPEntries()
        {
            string configPath = Path.Combine(_tempDir, "mcp.json");
            Dictionary<string, object> config = new()
            {
                ["mcpServers"] = new Dictionary<string, object>
                {
                    ["other-server"] = new { command = "python", args = new[] { "app.py" }, env = new Dictionary<string, string> { ["API_KEY"] = "abc" } }
                }
            };
            string originalJson = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(configPath, originalJson);

            _repository.DeleteULoopMCPEntries(configPath);

            string resultJson = File.ReadAllText(configPath);
            Assert.AreEqual(originalJson, resultJson, "File content should not be modified");
        }

        [Test]
        public void Should_NotModifyFile_WhenNoMcpServersKey()
        {
            string configPath = Path.Combine(_tempDir, "mcp.json");
            Dictionary<string, object> config = new()
            {
                ["someOtherKey"] = "value"
            };
            string originalJson = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(configPath, originalJson);

            _repository.DeleteULoopMCPEntries(configPath);

            string resultJson = File.ReadAllText(configPath);
            Assert.AreEqual(originalJson, resultJson, "File content should not be modified");
        }
    }
}
