using System.Threading.Tasks;
using System.Threading;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Responsible for temporal cohesion of Unity Search execution processing
    /// Processing sequence: 1. Clean up old files, 2. Execute search, 3. Process results
    /// Related classes: UnitySearchTool, UnitySearchService, SearchResultExporter
    /// Design reference: @Packages/docs/ARCHITECTURE_Unity.md - UseCase + Tool Pattern (DDD Integration)
    /// </summary>
    public class UnitySearchUseCase : AbstractUseCase<UnitySearchSchema, UnitySearchResponse>
    {
        /// <summary>
        /// Execute Unity Search processing
        /// </summary>
        /// <param name="parameters">Search parameters</param>
        /// <param name="cancellationToken">Cancellation control token</param>
        /// <returns>Search result</returns>
        public override async Task<UnitySearchResponse> ExecuteAsync(UnitySearchSchema parameters, CancellationToken cancellationToken)
        {
            // 0. Apply default values (internal processing)
            ApplyDefaultValues(parameters);

            // 1. Clean up old files
            cancellationToken.ThrowIfCancellationRequested();
            UnitySearchService.CleanupOldExports();

            // 2. Execute search
            cancellationToken.ThrowIfCancellationRequested();
            UnitySearchResponse response = await UnitySearchService.ExecuteSearchAsync(parameters);

            // 3. Process results (logging etc. already handled by UnitySearchService)
            return response;
        }

        /// <summary>
        /// Apply default values to schema (internal processing)
        /// </summary>
        /// <param name="schema">Schema</param>
        private void ApplyDefaultValues(UnitySearchSchema schema)
        {
            // Ensure arrays are not null
            schema.Providers ??= new string[0];
            schema.FileExtensions ??= new string[0];
            schema.AssetTypes ??= new string[0];

            // Apply reasonable default values
            if (schema.MaxResults <= 0)
                schema.MaxResults = 50;

            if (schema.AutoSaveThreshold < 0)
                schema.AutoSaveThreshold = 100;

            // Ensure search query is not null
            schema.SearchQuery ??= "";
            schema.PathFilter ??= "";
        }
    }
}