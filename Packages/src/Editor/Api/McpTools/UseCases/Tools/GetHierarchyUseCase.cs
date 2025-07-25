using System.Threading.Tasks;
using System.Threading;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Responsible for temporal cohesion of Unity Hierarchy retrieval processing
    /// Processing sequence: 1. Hierarchy information retrieval, 2. Data conversion, 3. Response size determination and file output
    /// Related classes: GetHierarchyTool, HierarchyService, HierarchySerializer, HierarchyResultExporter
    /// Design reference: @Packages/docs/ARCHITECTURE_Unity.md - UseCase + Tool Pattern (DDD Integration)
    /// </summary>
    public class GetHierarchyUseCase : AbstractUseCase<GetHierarchySchema, GetHierarchyResponse>
    {
        private readonly HierarchyService _hierarchyService;
        private readonly HierarchySerializer _hierarchySerializer;

        public GetHierarchyUseCase(HierarchyService hierarchyService, HierarchySerializer hierarchySerializer)
        {
            _hierarchyService = hierarchyService ?? throw new System.ArgumentNullException(nameof(hierarchyService));
            _hierarchySerializer = hierarchySerializer ?? throw new System.ArgumentNullException(nameof(hierarchySerializer));
        }
        /// <summary>
        /// Execute Unity Hierarchy retrieval processing
        /// </summary>
        /// <param name="parameters">Hierarchy retrieval parameters</param>
        /// <param name="cancellationToken">Cancellation control token</param>
        /// <returns>Hierarchy retrieval result</returns>
        public override Task<GetHierarchyResponse> ExecuteAsync(GetHierarchySchema parameters, CancellationToken cancellationToken)
        {
            if (parameters == null)
            {
                throw new System.ArgumentNullException(nameof(parameters));
            }

            try
            {
                // 1. Hierarchy information retrieval
                HierarchyOptions options = new HierarchyOptions
                {
                    IncludeInactive = parameters.IncludeInactive,
                    MaxDepth = parameters.MaxDepth,
                    RootPath = parameters.RootPath,
                    IncludeComponents = parameters.IncludeComponents
                };
                
                cancellationToken.ThrowIfCancellationRequested();
                
                var nodes = _hierarchyService.GetHierarchyNodes(options);
                var context = _hierarchyService.GetCurrentContext();
                
                // 2. Data conversion
                cancellationToken.ThrowIfCancellationRequested();
                
                var nestedNodes = _hierarchySerializer.ConvertToNestedStructure(nodes);
                
                // 3. Response size determination and file output
                cancellationToken.ThrowIfCancellationRequested();
                
                GetHierarchyResponse nestedResponse = new GetHierarchyResponse(nestedNodes, context);
                
                // Calculate response size
                var settings = new Newtonsoft.Json.JsonSerializerSettings
                {
                    ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
                    MaxDepth = McpServerConfig.DEFAULT_JSON_MAX_DEPTH
                };
                string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(nestedResponse, Newtonsoft.Json.Formatting.None, settings);
                int estimatedSizeBytes = System.Text.Encoding.UTF8.GetByteCount(jsonString);
                int estimatedSizeKB = estimatedSizeBytes / 1024;
                
                // Save to file if size limit is exceeded
                if (estimatedSizeKB >= parameters.MaxResponseSizeKB)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    string filePath = HierarchyResultExporter.ExportHierarchyResults(nestedNodes, context);
                    return Task.FromResult(new GetHierarchyResponse(filePath, "auto_threshold", context));
                }
                else
                {
                    return Task.FromResult(nestedResponse);
                }
            }
            catch (System.OperationCanceledException)
            {
                throw; // Re-throw cancellation exceptions
            }
            catch (System.Exception ex)
            {
                // Log the error and re-throw
                VibeLogger.LogError("get_hierarchy_failed", $"Failed to get hierarchy: {ex.Message}", ex);
                throw new System.InvalidOperationException($"Failed to retrieve hierarchy: {ex.Message}", ex);
            }
        }
    }
}