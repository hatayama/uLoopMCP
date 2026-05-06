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
        public void GetToolType_WhenGetLogsComesFromFirstPartyToolsAssembly_ReturnsBundledPluginType()
        {
            // Tests that get-logs is a bundled plugin instead of an application-layer tool.
            UnityCliLoopToolRegistry registry = new UnityCliLoopToolRegistry();

            System.Type toolType = registry.GetToolType("get-logs");

            Assert.That(toolType, Is.Not.Null);
            Assert.That(toolType.Assembly.GetName().Name, Is.EqualTo("UnityCLILoop.FirstPartyTools.Editor"));
            Assert.That(registry.IsThirdPartyTool("get-logs"), Is.False);
        }

        [Test]
        public void GetToolType_WhenCompileComesFromFirstPartyToolsAssembly_ReturnsBundledPluginType()
        {
            // Tests that compile is a bundled plugin instead of an application-layer tool.
            UnityCliLoopToolRegistry registry = new UnityCliLoopToolRegistry();

            System.Type toolType = registry.GetToolType("compile");

            Assert.That(toolType, Is.Not.Null);
            Assert.That(toolType.Assembly.GetName().Name, Is.EqualTo("UnityCLILoop.FirstPartyTools.Editor"));
            Assert.That(registry.IsThirdPartyTool("compile"), Is.False);
        }

        [Test]
        public void GetToolType_WhenExecuteDynamicCodeComesFromFirstPartyToolsAssembly_ReturnsBundledPluginType()
        {
            // Tests that execute-dynamic-code is a bundled plugin instead of an application-layer tool.
            UnityCliLoopToolRegistry registry = new UnityCliLoopToolRegistry();

            System.Type toolType = registry.GetToolType("execute-dynamic-code");

            Assert.That(toolType, Is.Not.Null);
            Assert.That(toolType.Assembly.GetName().Name, Is.EqualTo("UnityCLILoop.FirstPartyTools.Editor"));
            Assert.That(registry.IsThirdPartyTool("execute-dynamic-code"), Is.False);
        }

        [Test]
        public void GetToolType_WhenToolComesFromFirstPartyToolsAssembly_ReturnsBundledPluginType()
        {
            // Tests that a bundled tool can live in the first-party plugin assembly and still register normally.
            UnityCliLoopToolRegistry registry = new UnityCliLoopToolRegistry();

            System.Type toolType = registry.GetToolType("control-play-mode");

            Assert.That(toolType, Is.Not.Null);
            Assert.That(toolType.Assembly.GetName().Name, Is.EqualTo("UnityCLILoop.FirstPartyTools.Editor"));
            Assert.That(registry.IsThirdPartyTool("control-play-mode"), Is.False);
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

            Assert.That(registry.IsToolRegistered(UnityCliLoopConstants.COMMAND_NAME_GET_VERSION), Is.False);
        }

        [Test]
        public void Constructor_WhenGetToolDetailsIsInternalBridgeCommand_DoesNotRegisterItAsTool()
        {
            // Tests that get-tool-details is kept out of the extension-facing runtime registry.
            UnityCliLoopToolRegistry registry = new UnityCliLoopToolRegistry();

            Assert.That(registry.IsToolRegistered(UnityCliLoopConstants.COMMAND_NAME_GET_TOOL_DETAILS), Is.False);
        }

        [Test]
        public async Task ExecuteCommandAsync_WhenCommandIsGetVersion_ReturnsBridgeVersionPayload()
        {
            // Tests that get-version still works as a CLI-only bridge command after leaving the tool registry.
            UnityCliLoopToolResponse response = await UnityApiHandler.ExecuteCommandAsync(
                UnityCliLoopConstants.COMMAND_NAME_GET_VERSION,
                new JObject());

            GetVersionResponse getVersionResponse = response as GetVersionResponse;
            Assert.That(getVersionResponse, Is.Not.Null);
            Assert.That(getVersionResponse.UnityVersion, Is.Not.Empty);
            Assert.That(getVersionResponse.IsEditor, Is.True);
        }

        [Test]
        public async Task ExecuteCommandAsync_WhenCommandIsGetToolDetails_ReturnsCatalogWithoutInternalCommands()
        {
            // Tests that CLI catalog access still works without registering the catalog command as a tool.
            UnityCliLoopToolResponse response = await UnityApiHandler.ExecuteCommandAsync(
                UnityCliLoopConstants.COMMAND_NAME_GET_TOOL_DETAILS,
                new JObject());

            GetToolDetailsResponse getToolDetailsResponse = response as GetToolDetailsResponse;
            Assert.That(getToolDetailsResponse, Is.Not.Null);

            string[] toolNames = getToolDetailsResponse.Tools
                .Select(tool => tool.Name)
                .ToArray();

            Assert.That(toolNames, Does.Contain("get-logs"));
            Assert.That(toolNames, Does.Not.Contain(UnityCliLoopConstants.COMMAND_NAME_GET_TOOL_DETAILS));
            Assert.That(toolNames, Does.Not.Contain(UnityCliLoopConstants.COMMAND_NAME_GET_VERSION));
            Assert.That(toolNames, Does.Not.Contain("focus-window"));
            Assert.That(toolNames, Does.Not.Contain("ping"));
            Assert.That(toolNames, Does.Not.Contain("debug-sleep"));
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
        public async Task ExecuteCommandAsync_WhenSampleToolUsesTypedContract_ReturnsTypedResponse()
        {
            // Tests that third-party sample tools execute through the same typed contract path as bundled tools.
            JObject parameters = JObject.FromObject(new
            {
                name = "Masamichi",
                language = "french",
                includeTimestamp = false
            });

            UnityCliLoopToolResponse response = await UnityApiHandler.ExecuteCommandAsync("hello-world", parameters);
            JObject serializedResponse = JObject.FromObject(response);

            Assert.That(serializedResponse.Value<string>("Message"), Is.EqualTo("Bonjour, Masamichi!"));
            Assert.That(serializedResponse.Value<string>("Language"), Is.EqualTo("french"));
            Assert.That(serializedResponse["Timestamp"]?.Type, Is.EqualTo(JTokenType.Null));
        }

        [Test]
        public void CustomCommandSamplesAsmdef_ReferencesOnlyToolContracts()
        {
            // Tests that third-party sample tools depend only on the public tool contract assembly.
            string asmdefPath = Path.Combine(
                UnityCliLoopPathResolver.GetProjectRoot(),
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
                UnityCliLoopPathResolver.GetProjectRoot(),
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
