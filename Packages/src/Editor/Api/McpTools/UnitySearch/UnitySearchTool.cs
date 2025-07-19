using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Unity Search tool handler - Type-safe implementation using Schema and Response
    /// Provides comprehensive Unity Search functionality via MCP interface
    /// Related classes:
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
            // Check for cancellation before starting
            cancellationToken.ThrowIfCancellationRequested();
            
            // Clean up old export files before executing new search
            UnitySearchService.CleanupOldExports();

            // Check for cancellation before search
            cancellationToken.ThrowIfCancellationRequested();
            
            // Execute search using service layer
            UnitySearchResponse response = await UnitySearchService.ExecuteSearchAsync(parameters);

            // Log search execution for debugging
            if (response.Success)
            {
                string resultInfo = response.ResultsSavedToFile 
                    ? $"Results saved to file: {response.ResultsFilePath} ({response.TotalCount} items)"
                    : $"Returned {response.DisplayedCount} of {response.TotalCount} results inline";
            }
            else
            {
            }

            return response;
        }

        /// <summary>
        /// Apply default values for schema properties if they are null
        /// Ensures reasonable defaults for Unity Search parameters
        /// </summary>
        protected override UnitySearchSchema ApplyDefaultValues(UnitySearchSchema schema)
        {
            // Ensure arrays are not null
            schema.Providers ??= new string[0];
            schema.FileExtensions ??= new string[0];
            schema.AssetTypes ??= new string[0];

            // Apply reasonable defaults
            if (schema.MaxResults <= 0)
                schema.MaxResults = 50;

            if (schema.AutoSaveThreshold < 0)
                schema.AutoSaveThreshold = 100;

            // Ensure search query is not null
            schema.SearchQuery ??= "";
            schema.PathFilter ??= "";

            return schema;
        }
    }
} 