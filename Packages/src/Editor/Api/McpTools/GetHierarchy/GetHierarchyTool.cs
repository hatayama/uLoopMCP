using System.Threading.Tasks;
using System.Threading;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Tool to retrieve Unity Hierarchy information in AI-friendly format
    /// Related classes:
    /// - HierarchyService: Core logic for hierarchy traversal
    /// - HierarchySerializer: JSON formatting logic
    /// - HierarchyNode: Data structure for hierarchy nodes
    /// - HierarchyNodeNested: Nested hierarchy structure
    /// - HierarchyResultExporter: File export functionality
    /// </summary>
    [McpTool(Description = "Get Unity Hierarchy structure in AI-friendly format")]
    public class GetHierarchyTool : AbstractUnityTool<GetHierarchySchema, GetHierarchyResponse>
    {
        public override string ToolName => "get-hierarchy";
        
        protected override async Task<GetHierarchyResponse> ExecuteAsync(GetHierarchySchema parameters, CancellationToken cancellationToken)
        {
            // GetHierarchyUseCaseインスタンスを生成して実行
            GetHierarchyUseCase useCase = new();
            return await useCase.ExecuteAsync(parameters, cancellationToken);
        }
    }
}