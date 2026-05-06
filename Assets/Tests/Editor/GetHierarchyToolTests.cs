using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace io.github.hatayama.UnityCliLoop
{
    public class GetHierarchyToolTests
    {
        private GetHierarchyTool _tool;
        private FakeHierarchyService _hierarchyService;
        
        [SetUp]
        public void SetUp()
        {
            _hierarchyService = new FakeHierarchyService();
            _tool = new GetHierarchyTool();
            _tool.InitializeHostServices(new FakeToolHostServices(_hierarchyService));
        }
        
        [Test]
        public void ToolName_ReturnsCorrectName()
        {
            // Tests that the bundled hierarchy tool keeps the CLI command name stable.
            Assert.That(_tool.ToolName, Is.EqualTo("get-hierarchy"));
        }
        
        [Test]
        public async Task ExecuteAsync_WithDefaultParameters_ReturnsHostServiceResponse()
        {
            // Tests that the tool delegates execution to the injected hierarchy host service.
            JObject parameters = new JObject();
            
            UnityCliLoopToolResponse baseResponse = await _tool.ExecuteAsync(parameters);
            GetHierarchyResponse response = baseResponse as GetHierarchyResponse;
            
            Assert.That(response, Is.Not.Null);
            Assert.That(response.hierarchyFilePath, Is.EqualTo("HierarchyResults/fake.json"));
            Assert.That(response.message, Is.EqualTo("fake hierarchy message"));
            Assert.That(_hierarchyService.LastRequest, Is.Not.Null);
        }
        
        [Test]
        public async Task ExecuteAsync_WithMaxDepthParameter_MapsRequest()
        {
            // Tests that MaxDepth crosses the first-party tool boundary through the host request DTO.
            JObject parameters = new JObject
            {
                ["MaxDepth"] = 1
            };
            
            await _tool.ExecuteAsync(parameters);
            
            Assert.That(_hierarchyService.LastRequest.MaxDepth, Is.EqualTo(1));
        }
        
        [Test]
        public async Task ExecuteAsync_WithIncludeComponentsFalse_MapsRequest()
        {
            // Tests that component inclusion crosses the first-party tool boundary through the host request DTO.
            JObject parameters = new JObject
            {
                ["IncludeComponents"] = false
            };
            
            await _tool.ExecuteAsync(parameters);
            
            Assert.That(_hierarchyService.LastRequest.IncludeComponents, Is.False);
        }
        
        [Test]
        public void ParameterSchema_HasCorrectProperties()
        {
            // Tests that moving the tool assembly does not change the public parameter schema.
            ToolParameterSchema schema = _tool.ParameterSchema;
            
            Assert.That(schema, Is.Not.Null);
            Assert.That(schema.Properties, Is.Not.Null);
            Assert.That(schema.Properties.ContainsKey("IncludeInactive"), Is.True);
            Assert.That(schema.Properties.ContainsKey("MaxDepth"), Is.True);
            Assert.That(schema.Properties.ContainsKey("RootPath"), Is.True);
            Assert.That(schema.Properties.ContainsKey("IncludeComponents"), Is.True);
            Assert.That(schema.Properties.ContainsKey("IncludePaths"), Is.True);
            Assert.That(schema.Properties.ContainsKey("UseComponentsLut"), Is.True);
        }

        private sealed class FakeHierarchyService : IUnityCliLoopHierarchyService
        {
            public UnityCliLoopHierarchyRequest LastRequest { get; private set; }

            public Task<UnityCliLoopHierarchyResult> GetHierarchyAsync(UnityCliLoopHierarchyRequest request, CancellationToken ct)
            {
                LastRequest = request;
                UnityCliLoopHierarchyResult result = new UnityCliLoopHierarchyResult(
                    "HierarchyResults/fake.json",
                    "fake hierarchy message");
                return Task.FromResult(result);
            }
        }

        private sealed class FakeToolHostServices : IUnityCliLoopToolHostServices
        {
            public IUnityCliLoopConsoleLogService ConsoleLogs => throw new NotSupportedException();
            public IUnityCliLoopConsoleClearService ConsoleClear => throw new NotSupportedException();
            public IUnityCliLoopCompilationService Compilation => throw new NotSupportedException();
            public IUnityCliLoopDynamicCodeExecutionService DynamicCodeExecution => throw new NotSupportedException();
            public IUnityCliLoopHierarchyService Hierarchy { get; }
            public IUnityCliLoopTestExecutionService TestExecution => throw new NotSupportedException();
            public IUnityCliLoopGameObjectSearchService GameObjectSearch => throw new NotSupportedException();

            public FakeToolHostServices(IUnityCliLoopHierarchyService hierarchy)
            {
                Hierarchy = hierarchy ?? throw new ArgumentNullException(nameof(hierarchy));
            }
        }
    }
}
