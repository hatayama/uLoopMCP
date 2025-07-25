using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Unity Search tool handler - Type-safe implementation using Schema and Response
    /// Provides comprehensive Unity Search functionality via MCP interface
    /// 
    /// Design Reference: @Packages/docs/ARCHITECTURE_Unity.md - UseCase + Tool Pattern (DDD Integration)
    /// 
    /// This Tool class delegates to UnitySearchUseCase for business logic execution,
    /// following the UseCase + Tool pattern for separation of concerns.
    /// 
    /// Related classes:
    /// - UnitySearchUseCase: Business logic and orchestration
    /// - UnitySearchService: Service layer for Unity Search API integration
    /// - SearchResultItem: Individual search result data structure
    /// - SearchResultExporter: File export functionality for large result sets
    /// - UnitySearchSchema: Type-safe parameter schema
    /// - UnitySearchResponse: Type-safe response structure
    /// </summary>
    [McpTool(Description = "Search Unity project using Unity Search API with comprehensive filtering and export options")]
    public class UnitySearchTool : AbstractUnityTool<UnitySearchSchema, UnitySearchResponse>
    {
        public override string ToolName => "unity-search";

        /// <summary>
        /// Execute Unity search tool with type-safe parameters
        /// </summary>
        /// <param name="parameters">Type-safe search parameters</param>
        /// <param name="cancellationToken">Cancellation token for timeout control</param>
        /// <returns>Search results or file path if exported</returns>
        protected override async Task<UnitySearchResponse> ExecuteAsync(UnitySearchSchema parameters, CancellationToken cancellationToken)
        {
            // Create and execute UnitySearchUseCase instance
            UnitySearchUseCase useCase = new();
            return await useCase.ExecuteAsync(parameters, cancellationToken);
        }

    }
} 