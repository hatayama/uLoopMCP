using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace io.github.hatayama.UnityCliLoop
{
    [TestFixture]
    public sealed class UnityCliLoopToolRegistryTests
    {
        [Test]
        public void Constructor_WhenFirstPartyToolsUseToolAttribute_RegistersThem()
        {
            // Tests that bundled tools use the same attribute-based registry path as extension tools.
            UnityCliLoopToolRegistry registry = new UnityCliLoopToolRegistry();

            Assert.That(registry.IsToolRegistered("compile"), Is.True);
            Assert.That(registry.IsToolRegistered("get-logs"), Is.True);
            Assert.That(registry.IsToolRegistered("execute-dynamic-code"), Is.True);
        }

        [Test]
        public void IsThirdPartyTool_WhenToolComesFromApplicationAssembly_ReturnsFalse()
        {
            // Tests that the renamed application assembly is still classified as first-party.
            UnityCliLoopToolRegistry registry = new UnityCliLoopToolRegistry();

            Assert.That(registry.IsThirdPartyTool("get-logs"), Is.False);
        }

        [Test]
        public void Constructor_WhenFocusWindowIsNativeCliCommand_DoesNotRegisterItAsTool()
        {
            // Tests that focus-window stays a native CLI command instead of an extension-facing Unity tool.
            UnityCliLoopToolRegistry registry = new UnityCliLoopToolRegistry();

            Assert.That(registry.IsToolRegistered("focus-window"), Is.False);
        }

        [Test]
        public void Constructor_WhenGetVersionIsInternalBridgeCommand_DoesNotRegisterItAsTool()
        {
            // Tests that get-version is kept out of the extension-facing runtime registry.
            UnityCliLoopToolRegistry registry = new UnityCliLoopToolRegistry();

            Assert.That(registry.IsToolRegistered(McpConstants.COMMAND_NAME_GET_VERSION), Is.False);
        }

        [Test]
        public async Task ExecuteCommandAsync_WhenCommandIsGetVersion_ReturnsBridgeVersionPayload()
        {
            // Tests that get-version still works as a CLI-only bridge command after leaving the tool registry.
            UnityCliLoopToolResponse response = await UnityApiHandler.ExecuteCommandAsync(
                McpConstants.COMMAND_NAME_GET_VERSION,
                new JObject());

            GetVersionResponse getVersionResponse = response as GetVersionResponse;
            Assert.That(getVersionResponse, Is.Not.Null);
            Assert.That(getVersionResponse.UnityVersion, Is.Not.Empty);
            Assert.That(getVersionResponse.IsEditor, Is.True);
        }

        [Test]
        public void Constructor_WhenLegacyDevelopmentToolsAreRemoved_DoesNotRegisterThem()
        {
            // Tests that legacy MCP-era development tools are not exposed through the runtime registry.
            UnityCliLoopToolRegistry registry = new UnityCliLoopToolRegistry();

            Assert.That(registry.IsToolRegistered("ping"), Is.False);
            Assert.That(registry.IsToolRegistered("debug-sleep"), Is.False);
        }

        [Test]
        public void Constructor_WhenSampleToolUsesToolContractsAssembly_RegistersAsThirdParty()
        {
            // Tests that a sample extension tool uses the same registry path while remaining outside first-party assemblies.
            UnityCliLoopToolRegistry registry = new UnityCliLoopToolRegistry();

            Assert.That(registry.IsToolRegistered("hello-world"), Is.True);
            Assert.That(registry.IsThirdPartyTool("hello-world"), Is.True);
        }

        [Test]
        public void CustomCommandSamplesAsmdef_ReferencesOnlyToolContracts()
        {
            // Tests that third-party sample tools depend only on the public tool contract assembly.
            string asmdefPath = Path.Combine(
                UnityMcpPathResolver.GetProjectRoot(),
                "Assets",
                "Editor",
                "CustomCommandSamples",
                "UnityCLILoop.CustomCommandSamples.Editor.asmdef");
            JObject asmdef = JObject.Parse(File.ReadAllText(asmdefPath));
            string[] references = asmdef["references"]?.Values<string>().ToArray() ?? new string[0];

            Assert.That(references, Is.EqualTo(new[] { "UnityCLILoop.ToolContracts" }));
        }

        [Test]
        public void FirstPartyToolsAsmdef_ReferencesOnlyToolContracts()
        {
            // Tests that bundled plugin tools use the same public contract surface as extension tools.
            string asmdefPath = Path.Combine(
                UnityMcpPathResolver.GetProjectRoot(),
                "Packages",
                "src",
                "Editor",
                "FirstPartyTools",
                "UnityCLILoop.FirstPartyTools.Editor.asmdef");
            JObject asmdef = JObject.Parse(File.ReadAllText(asmdefPath));
            string[] references = asmdef["references"]?.Values<string>().ToArray() ?? new string[0];

            Assert.That(references, Is.EqualTo(new[] { "UnityCLILoop.ToolContracts" }));
        }

        [Test]
        public void GetRegisteredTools_WhenSerialized_DoesNotExposeDescription()
        {
            // Tests that get-tool-details no longer exposes display descriptions from runtime attributes.
            UnityCliLoopToolRegistry registry = new UnityCliLoopToolRegistry();
            ToolInfo tool = registry.GetRegisteredTools()
                .First(item => item.Name == "get-logs");
            JObject serializedTool = JObject.FromObject(tool);

            Assert.That(serializedTool.ContainsKey("description"), Is.False);
        }

        [Test]
        public void GetToolSettingsCatalog_WhenSerialized_DoesNotExposeDescription()
        {
            // Tests that Settings metadata no longer carries tooltip descriptions.
            UnityCliLoopToolRegistry registry = new UnityCliLoopToolRegistry();
            ToolSettingsCatalogItem tool = registry.GetToolSettingsCatalog()
                .First(item => item.Name == "get-logs");
            JObject serializedTool = JObject.FromObject(tool);

            Assert.That(serializedTool.ContainsKey("Description"), Is.False);
        }
    }
}
