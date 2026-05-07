using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace io.github.hatayama.UnityCliLoop
{
    internal static class ToolRegistryTestFactory
    {
        public static UnityCliLoopToolRegistry Create()
        {
            return new UnityCliLoopToolRegistry(new ToolHostServicesForTests());
        }

        private sealed class ToolHostServicesForTests : IUnityCliLoopToolHostServices
        {
            public IUnityCliLoopConsoleLogService ConsoleLogs { get; } =
                new EmptyConsoleLogService();
            public IUnityCliLoopConsoleClearService ConsoleClear { get; } =
                new SuccessfulConsoleClearService();
            public IUnityCliLoopCompilationService Compilation { get; } =
                new SuccessfulCompilationService();
            public IUnityCliLoopDynamicCodeExecutionService DynamicCodeExecution { get; } =
                new SuccessfulDynamicCodeExecutionService();
            public IUnityCliLoopHierarchyService Hierarchy { get; } =
                new EmptyHierarchyService();
            public IUnityCliLoopTestExecutionService TestExecution { get; } =
                new SuccessfulTestExecutionService();
            public IUnityCliLoopGameObjectSearchService GameObjectSearch { get; } =
                new EmptyGameObjectSearchService();
            public IUnityCliLoopScreenshotService Screenshot { get; } =
                new EmptyScreenshotService();
            public IUnityCliLoopRecordInputService RecordInput { get; } =
                new SuccessfulRecordInputService();
            public IUnityCliLoopReplayInputService ReplayInput { get; } =
                new SuccessfulReplayInputService();
            public IUnityCliLoopKeyboardSimulationService KeyboardSimulation { get; } =
                new SuccessfulKeyboardSimulationService();
            public IUnityCliLoopMouseInputSimulationService MouseInputSimulation { get; } =
                new SuccessfulMouseInputSimulationService();
            public IUnityCliLoopMouseUiSimulationService MouseUiSimulation { get; } =
                new SuccessfulMouseUiSimulationService();
        }

        private sealed class EmptyConsoleLogService : IUnityCliLoopConsoleLogService
        {
            public UnityCliLoopConsoleLogResult GetLogs(string logType)
            {
                return new UnityCliLoopConsoleLogResult(new UnityCliLoopConsoleLogEntry[0], 0);
            }

            public UnityCliLoopConsoleLogResult GetLogsWithSearch(
                string logType,
                string searchText,
                bool useRegex,
                bool searchInStackTrace)
            {
                return new UnityCliLoopConsoleLogResult(new UnityCliLoopConsoleLogEntry[0], 0);
            }
        }

        private sealed class SuccessfulConsoleClearService : IUnityCliLoopConsoleClearService
        {
            public UnityCliLoopConsoleClearResult Clear(bool addConfirmationMessage)
            {
                UnityCliLoopConsoleClearCounts counts = new UnityCliLoopConsoleClearCounts(0, 0, 0);
                return new UnityCliLoopConsoleClearResult(true, 0, counts, string.Empty);
            }
        }

        private sealed class SuccessfulCompilationService : IUnityCliLoopCompilationService
        {
            public Task<UnityCliLoopCompileResult> CompileAsync(UnityCliLoopCompileRequest request, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                UnityCliLoopCompileResult result = new UnityCliLoopCompileResult
                {
                    Success = true,
                    ErrorCount = 0,
                    WarningCount = 0,
                    Errors = new UnityCliLoopCompileIssue[0],
                    Warnings = new UnityCliLoopCompileIssue[0],
                    Message = string.Empty,
                    ProjectRoot = UnityCliLoopPathResolver.GetProjectRoot()
                };
                return Task.FromResult(result);
            }
        }

        private sealed class SuccessfulDynamicCodeExecutionService : IUnityCliLoopDynamicCodeExecutionService
        {
            public Task<ExecuteDynamicCodeResponse> ExecuteAsync(ExecuteDynamicCodeSchema parameters, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                ExecuteDynamicCodeResponse response = new ExecuteDynamicCodeResponse
                {
                    Success = true,
                    Result = "ok",
                    ErrorMessage = string.Empty,
                    SecurityLevel = ULoopSettings.GetDynamicCodeSecurityLevel().ToString()
                };
                return Task.FromResult(response);
            }
        }

        private sealed class EmptyHierarchyService : IUnityCliLoopHierarchyService
        {
            public Task<UnityCliLoopHierarchyResult> GetHierarchyAsync(UnityCliLoopHierarchyRequest request, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                UnityCliLoopHierarchyResult result = new UnityCliLoopHierarchyResult(string.Empty, string.Empty);
                return Task.FromResult(result);
            }
        }

        private sealed class SuccessfulTestExecutionService : IUnityCliLoopTestExecutionService
        {
            public Task<UnityCliLoopTestExecutionResult> RunTestsAsync(UnityCliLoopTestExecutionRequest request, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                UnityCliLoopTestExecutionResult result = new UnityCliLoopTestExecutionResult
                {
                    Success = true,
                    Message = string.Empty,
                    CompletedAt = string.Empty,
                    TestCount = 0,
                    PassedCount = 0,
                    FailedCount = 0,
                    SkippedCount = 0,
                    XmlPath = string.Empty
                };
                return Task.FromResult(result);
            }
        }

        private sealed class EmptyGameObjectSearchService : IUnityCliLoopGameObjectSearchService
        {
            public Task<UnityCliLoopGameObjectSearchResult> FindGameObjectsAsync(
                UnityCliLoopGameObjectSearchRequest request,
                CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                UnityCliLoopGameObjectSearchResult result = new UnityCliLoopGameObjectSearchResult
                {
                    Results = new UnityCliLoopGameObjectResult[0],
                    TotalFound = 0,
                    ErrorMessage = string.Empty,
                    ResultsFilePath = string.Empty,
                    Message = string.Empty,
                    ProcessingErrors = new UnityCliLoopGameObjectProcessingError[0]
                };
                return Task.FromResult(result);
            }
        }

        private sealed class EmptyScreenshotService : IUnityCliLoopScreenshotService
        {
            public Task<UnityCliLoopScreenshotResult> CaptureAsync(UnityCliLoopScreenshotRequest request, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(new UnityCliLoopScreenshotResult());
            }
        }

        private sealed class SuccessfulRecordInputService : IUnityCliLoopRecordInputService
        {
            public Task<UnityCliLoopRecordInputResult> RecordInputAsync(UnityCliLoopRecordInputRequest request, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                UnityCliLoopRecordInputResult result = new UnityCliLoopRecordInputResult
                {
                    Success = true,
                    Message = string.Empty,
                    Action = request.Action.ToString()
                };
                return Task.FromResult(result);
            }
        }

        private sealed class SuccessfulReplayInputService : IUnityCliLoopReplayInputService
        {
            public Task<UnityCliLoopReplayInputResult> ReplayInputAsync(UnityCliLoopReplayInputRequest request, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                UnityCliLoopReplayInputResult result = new UnityCliLoopReplayInputResult
                {
                    Success = true,
                    Message = string.Empty,
                    Action = request.Action.ToString()
                };
                return Task.FromResult(result);
            }
        }

        private sealed class SuccessfulKeyboardSimulationService : IUnityCliLoopKeyboardSimulationService
        {
            public Task<UnityCliLoopKeyboardSimulationResult> SimulateKeyboardAsync(
                UnityCliLoopKeyboardSimulationRequest request,
                CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                UnityCliLoopKeyboardSimulationResult result = new UnityCliLoopKeyboardSimulationResult
                {
                    Success = true,
                    Message = string.Empty,
                    Action = request.Action.ToString(),
                    KeyName = request.Key
                };
                return Task.FromResult(result);
            }
        }

        private sealed class SuccessfulMouseInputSimulationService : IUnityCliLoopMouseInputSimulationService
        {
            public Task<UnityCliLoopMouseInputSimulationResult> SimulateMouseInputAsync(
                UnityCliLoopMouseInputSimulationRequest request,
                CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                UnityCliLoopMouseInputSimulationResult result = new UnityCliLoopMouseInputSimulationResult
                {
                    Success = true,
                    Message = string.Empty,
                    Action = request.Action.ToString(),
                    Button = request.Button.ToString(),
                    PositionX = request.X,
                    PositionY = request.Y
                };
                return Task.FromResult(result);
            }
        }

        private sealed class SuccessfulMouseUiSimulationService : IUnityCliLoopMouseUiSimulationService
        {
            public Task<UnityCliLoopMouseUiSimulationResult> SimulateMouseUiAsync(
                UnityCliLoopMouseUiSimulationRequest request,
                CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                UnityCliLoopMouseUiSimulationResult result = new UnityCliLoopMouseUiSimulationResult
                {
                    Success = true,
                    Message = string.Empty,
                    Action = request.Action.ToString(),
                    HitGameObjectName = string.Empty,
                    PositionX = request.X,
                    PositionY = request.Y,
                    EndPositionX = request.X,
                    EndPositionY = request.Y
                };
                return Task.FromResult(result);
            }
        }
    }

    [TestFixture]
    public sealed class UnityCliLoopToolRegistryTests
    {
        [Test]
        public void Constructor_WhenFirstPartyToolsUseToolAttribute_RegistersThem()
        {
            // Tests that bundled tools use the same attribute-based registry path as extension tools.
            UnityCliLoopToolRegistry registry = ToolRegistryTestFactory.Create();

            Assert.That(registry.IsToolRegistered("compile"), Is.True);
            Assert.That(registry.IsToolRegistered("get-logs"), Is.True);
            Assert.That(registry.IsToolRegistered("execute-dynamic-code"), Is.True);
            Assert.That(registry.IsToolRegistered("clear-console"), Is.True);
            Assert.That(registry.IsToolRegistered("get-hierarchy"), Is.True);
            Assert.That(registry.IsToolRegistered("run-tests"), Is.True);
            Assert.That(registry.IsToolRegistered("find-game-objects"), Is.True);
            Assert.That(registry.IsToolRegistered("screenshot"), Is.True);
            Assert.That(registry.IsToolRegistered("record-input"), Is.True);
            Assert.That(registry.IsToolRegistered("replay-input"), Is.True);
            Assert.That(registry.IsToolRegistered("simulate-keyboard"), Is.True);
            Assert.That(registry.IsToolRegistered("simulate-mouse-input"), Is.True);
            Assert.That(registry.IsToolRegistered("simulate-mouse-ui"), Is.True);
        }

        [Test]
        public void GetToolType_WhenGetLogsComesFromFirstPartyToolsAssembly_ReturnsBundledPluginType()
        {
            // Tests that get-logs is a bundled plugin instead of an application-layer tool.
            UnityCliLoopToolRegistry registry = ToolRegistryTestFactory.Create();

            System.Type toolType = registry.GetToolType("get-logs");

            Assert.That(toolType, Is.Not.Null);
            Assert.That(toolType.Assembly.GetName().Name, Is.EqualTo("UnityCLILoop.FirstPartyTools.Editor"));
            Assert.That(registry.IsThirdPartyTool("get-logs"), Is.False);
        }

        [Test]
        public void GetToolType_WhenCompileComesFromFirstPartyToolsAssembly_ReturnsBundledPluginType()
        {
            // Tests that compile is a bundled plugin instead of an application-layer tool.
            UnityCliLoopToolRegistry registry = ToolRegistryTestFactory.Create();

            System.Type toolType = registry.GetToolType("compile");

            Assert.That(toolType, Is.Not.Null);
            Assert.That(toolType.Assembly.GetName().Name, Is.EqualTo("UnityCLILoop.FirstPartyTools.Editor"));
            Assert.That(registry.IsThirdPartyTool("compile"), Is.False);
        }

        [Test]
        public void GetToolType_WhenExecuteDynamicCodeComesFromFirstPartyToolsAssembly_ReturnsBundledPluginType()
        {
            // Tests that execute-dynamic-code is a bundled plugin instead of an application-layer tool.
            UnityCliLoopToolRegistry registry = ToolRegistryTestFactory.Create();

            System.Type toolType = registry.GetToolType("execute-dynamic-code");

            Assert.That(toolType, Is.Not.Null);
            Assert.That(toolType.Assembly.GetName().Name, Is.EqualTo("UnityCLILoop.FirstPartyTools.Editor"));
            Assert.That(registry.IsThirdPartyTool("execute-dynamic-code"), Is.False);
        }

        [Test]
        public void GetToolType_WhenToolComesFromFirstPartyToolsAssembly_ReturnsBundledPluginType()
        {
            // Tests that a bundled tool can live in the first-party plugin assembly and still register normally.
            UnityCliLoopToolRegistry registry = ToolRegistryTestFactory.Create();

            System.Type toolType = registry.GetToolType("control-play-mode");

            Assert.That(toolType, Is.Not.Null);
            Assert.That(toolType.Assembly.GetName().Name, Is.EqualTo("UnityCLILoop.FirstPartyTools.Editor"));
            Assert.That(registry.IsThirdPartyTool("control-play-mode"), Is.False);
        }

        [Test]
        public void GetToolType_WhenClearConsoleComesFromFirstPartyToolsAssembly_ReturnsBundledPluginType()
        {
            // Tests that clear-console is a bundled plugin instead of an application-layer tool.
            UnityCliLoopToolRegistry registry = ToolRegistryTestFactory.Create();

            System.Type toolType = registry.GetToolType("clear-console");

            Assert.That(toolType, Is.Not.Null);
            Assert.That(toolType.Assembly.GetName().Name, Is.EqualTo("UnityCLILoop.FirstPartyTools.Editor"));
            Assert.That(registry.IsThirdPartyTool("clear-console"), Is.False);
        }

        [Test]
        public void GetToolType_WhenGetHierarchyComesFromFirstPartyToolsAssembly_ReturnsBundledPluginType()
        {
            // Tests that get-hierarchy is a bundled plugin instead of an application-layer tool.
            UnityCliLoopToolRegistry registry = ToolRegistryTestFactory.Create();

            System.Type toolType = registry.GetToolType("get-hierarchy");

            Assert.That(toolType, Is.Not.Null);
            Assert.That(toolType.Assembly.GetName().Name, Is.EqualTo("UnityCLILoop.FirstPartyTools.Editor"));
            Assert.That(registry.IsThirdPartyTool("get-hierarchy"), Is.False);
        }

        [Test]
        public void GetToolType_WhenRunTestsComesFromFirstPartyToolsAssembly_ReturnsBundledPluginType()
        {
            // Tests that run-tests is a bundled plugin instead of an application-layer tool.
            UnityCliLoopToolRegistry registry = ToolRegistryTestFactory.Create();

            System.Type toolType = registry.GetToolType("run-tests");

            Assert.That(toolType, Is.Not.Null);
            Assert.That(toolType.Assembly.GetName().Name, Is.EqualTo("UnityCLILoop.FirstPartyTools.Editor"));
            Assert.That(registry.IsThirdPartyTool("run-tests"), Is.False);
        }

        [Test]
        public void GetToolType_WhenFindGameObjectsComesFromFirstPartyToolsAssembly_ReturnsBundledPluginType()
        {
            // Tests that find-game-objects is a bundled plugin instead of an application-layer tool.
            UnityCliLoopToolRegistry registry = ToolRegistryTestFactory.Create();

            System.Type toolType = registry.GetToolType("find-game-objects");

            Assert.That(toolType, Is.Not.Null);
            Assert.That(toolType.Assembly.GetName().Name, Is.EqualTo("UnityCLILoop.FirstPartyTools.Editor"));
            Assert.That(registry.IsThirdPartyTool("find-game-objects"), Is.False);
        }

        [Test]
        public void GetToolType_WhenScreenshotComesFromFirstPartyToolsAssembly_ReturnsBundledPluginType()
        {
            // Tests that screenshot is a bundled plugin instead of an application-layer tool.
            UnityCliLoopToolRegistry registry = ToolRegistryTestFactory.Create();

            System.Type toolType = registry.GetToolType("screenshot");

            Assert.That(toolType, Is.Not.Null);
            Assert.That(toolType.Assembly.GetName().Name, Is.EqualTo("UnityCLILoop.FirstPartyTools.Editor"));
            Assert.That(registry.IsThirdPartyTool("screenshot"), Is.False);
        }

        [Test]
        public void GetToolType_WhenRecordInputComesFromFirstPartyToolsAssembly_ReturnsBundledPluginType()
        {
            // Tests that record-input is a bundled plugin instead of an application-layer tool.
            UnityCliLoopToolRegistry registry = ToolRegistryTestFactory.Create();

            System.Type toolType = registry.GetToolType("record-input");

            Assert.That(toolType, Is.Not.Null);
            Assert.That(toolType.Assembly.GetName().Name, Is.EqualTo("UnityCLILoop.FirstPartyTools.Editor"));
            Assert.That(registry.IsThirdPartyTool("record-input"), Is.False);
        }

        [Test]
        public void GetToolType_WhenReplayInputComesFromFirstPartyToolsAssembly_ReturnsBundledPluginType()
        {
            // Tests that replay-input is a bundled plugin instead of an application-layer tool.
            UnityCliLoopToolRegistry registry = ToolRegistryTestFactory.Create();

            System.Type toolType = registry.GetToolType("replay-input");

            Assert.That(toolType, Is.Not.Null);
            Assert.That(toolType.Assembly.GetName().Name, Is.EqualTo("UnityCLILoop.FirstPartyTools.Editor"));
            Assert.That(registry.IsThirdPartyTool("replay-input"), Is.False);
        }

        [Test]
        public void GetToolType_WhenSimulateKeyboardComesFromFirstPartyToolsAssembly_ReturnsBundledPluginType()
        {
            // Tests that simulate-keyboard is a bundled plugin instead of an application-layer tool.
            UnityCliLoopToolRegistry registry = ToolRegistryTestFactory.Create();

            System.Type toolType = registry.GetToolType("simulate-keyboard");

            Assert.That(toolType, Is.Not.Null);
            Assert.That(toolType.Assembly.GetName().Name, Is.EqualTo("UnityCLILoop.FirstPartyTools.Editor"));
            Assert.That(registry.IsThirdPartyTool("simulate-keyboard"), Is.False);
        }

        [Test]
        public void GetToolType_WhenSimulateMouseInputComesFromFirstPartyToolsAssembly_ReturnsBundledPluginType()
        {
            // Tests that simulate-mouse-input is a bundled plugin instead of an application-layer tool.
            UnityCliLoopToolRegistry registry = ToolRegistryTestFactory.Create();

            System.Type toolType = registry.GetToolType("simulate-mouse-input");

            Assert.That(toolType, Is.Not.Null);
            Assert.That(toolType.Assembly.GetName().Name, Is.EqualTo("UnityCLILoop.FirstPartyTools.Editor"));
            Assert.That(registry.IsThirdPartyTool("simulate-mouse-input"), Is.False);
        }

        [Test]
        public void GetToolType_WhenSimulateMouseUiComesFromFirstPartyToolsAssembly_ReturnsBundledPluginType()
        {
            // Tests that simulate-mouse-ui is a bundled plugin instead of an application-layer tool.
            UnityCliLoopToolRegistry registry = ToolRegistryTestFactory.Create();

            System.Type toolType = registry.GetToolType("simulate-mouse-ui");

            Assert.That(toolType, Is.Not.Null);
            Assert.That(toolType.Assembly.GetName().Name, Is.EqualTo("UnityCLILoop.FirstPartyTools.Editor"));
            Assert.That(registry.IsThirdPartyTool("simulate-mouse-ui"), Is.False);
        }

        [Test]
        public void Constructor_WhenFocusWindowIsNativeCliCommand_DoesNotRegisterItAsTool()
        {
            // Tests that focus-window stays a native CLI command instead of an extension-facing Unity tool.
            UnityCliLoopToolRegistry registry = ToolRegistryTestFactory.Create();

            Assert.That(registry.IsToolRegistered("focus-window"), Is.False);
        }

        [Test]
        public void Constructor_WhenGetVersionIsInternalBridgeCommand_DoesNotRegisterItAsTool()
        {
            // Tests that get-version is kept out of the extension-facing runtime registry.
            UnityCliLoopToolRegistry registry = ToolRegistryTestFactory.Create();

            Assert.That(registry.IsToolRegistered(UnityCliLoopConstants.COMMAND_NAME_GET_VERSION), Is.False);
        }

        [Test]
        public void Constructor_WhenGetToolDetailsIsInternalBridgeCommand_DoesNotRegisterItAsTool()
        {
            // Tests that get-tool-details is kept out of the extension-facing runtime registry.
            UnityCliLoopToolRegistry registry = ToolRegistryTestFactory.Create();

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
            UnityCliLoopToolRegistry registry = ToolRegistryTestFactory.Create();

            Assert.That(registry.IsToolRegistered("ping"), Is.False);
            Assert.That(registry.IsToolRegistered("debug-sleep"), Is.False);
        }

        [Test]
        public void Constructor_WhenSampleToolUsesToolContractsAssembly_RegistersAsThirdParty()
        {
            // Tests that a sample extension tool uses the same registry path while remaining outside first-party assemblies.
            UnityCliLoopToolRegistry registry = ToolRegistryTestFactory.Create();

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
        public async Task ExecuteToolAsync_WhenToolReturnsResponse_AssignsVersionToResponseInstance()
        {
            // Tests that response versioning is assigned per response instead of using global contract state.
            UnityCliLoopToolRegistry registry = ToolRegistryTestFactory.Create();
            JObject parameters = JObject.FromObject(new
            {
                name = "Masamichi",
                language = "english",
                includeTimestamp = false
            });

            UnityCliLoopToolResponse response = await registry.ExecuteToolAsync("hello-world", parameters);

            Assert.That(response.Ver, Is.EqualTo(UnityCliLoopVersion.VERSION));
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
            UnityCliLoopToolRegistry registry = ToolRegistryTestFactory.Create();
            ToolInfo tool = registry.GetRegisteredTools()
                .First(item => item.Name == "get-logs");
            JObject serializedTool = JObject.FromObject(tool);

            Assert.That(serializedTool.ContainsKey("description"), Is.False);
        }

        [Test]
        public void GetToolSettingsCatalog_WhenSerialized_DoesNotExposeDescription()
        {
            // Tests that Settings metadata no longer carries tooltip descriptions.
            UnityCliLoopToolRegistry registry = ToolRegistryTestFactory.Create();
            ToolSettingsCatalogItem tool = registry.GetToolSettingsCatalog()
                .First(item => item.Name == "get-logs");
            JObject serializedTool = JObject.FromObject(tool);

            Assert.That(serializedTool.ContainsKey("Description"), Is.False);
        }
    }
}
