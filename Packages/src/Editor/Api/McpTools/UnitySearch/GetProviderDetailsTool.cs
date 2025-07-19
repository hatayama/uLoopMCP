using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Tool to retrieve detailed information about Unity Search providers
    /// Provides comprehensive metadata about available search providers
    /// Related classes:
    /// - UnitySearchService: Service layer for provider information retrieval
    /// - ProviderInfo: Data structure for provider information
    /// - GetProviderDetailsSchema: Input parameters schema
    /// - GetProviderDetailsResponse: Output response schema
    /// </summary>
    [McpTool(Description = "Get detailed information about Unity Search providers including display names, descriptions, active status, and capabilities")]
    public class GetProviderDetailsTool : AbstractUnityTool<GetProviderDetailsSchema, GetProviderDetailsResponse>
    {
        /// <summary>
        /// Tool name for MCP tool registration
        /// </summary>
        public override string ToolName => "get-provider-details";


        /// <summary>
        /// Execute the provider details retrieval tool
        /// </summary>
        /// <param name="parameters">Tool parameters</param>
        /// <param name="cancellationToken">Cancellation token for timeout control</param>
        /// <returns>Provider details response</returns>
        protected override Task<GetProviderDetailsResponse> ExecuteAsync(GetProviderDetailsSchema parameters, CancellationToken cancellationToken)
        {
            ProviderInfo[] providers;
            string appliedFilter;

            // Get provider details based on parameters
            if (!string.IsNullOrWhiteSpace(parameters.ProviderId))
            {
                // Get specific provider
                ProviderInfo provider = UnitySearchService.GetProviderDetails(parameters.ProviderId);
                if (provider == null)
                {
                    return Task.FromResult(new GetProviderDetailsResponse($"Provider '{parameters.ProviderId}' not found"));
                }
                providers = new[] { provider };
                appliedFilter = parameters.ProviderId;
            }
            else
            {
                // Get all providers
                providers = UnitySearchService.GetProviderDetails();
                appliedFilter = "all";
            }

            // Apply active-only filter if requested
            if (parameters.ActiveOnly)
            {
                providers = providers.Where(p => p.IsActive).ToArray();
                appliedFilter += " (active only)";
            }

            // Remove descriptions if not requested
            if (!parameters.IncludeDescriptions)
            {
                foreach (ProviderInfo provider in providers)
                {
                    provider.Description = "";
                }
            }

            // Sort by priority if requested
            if (parameters.SortByPriority)
            {
                providers = providers.OrderBy(p => p.Priority).ToArray();
            }

            // Log tool execution for debugging

            return Task.FromResult(new GetProviderDetailsResponse(providers, appliedFilter, parameters.SortByPriority));
        }

        /// <summary>
        /// Apply default values for schema properties if they are null
        /// Ensures reasonable defaults for provider details parameters
        /// </summary>
        protected override GetProviderDetailsSchema ApplyDefaultValues(GetProviderDetailsSchema schema)
        {
            // Ensure string properties are not null
            schema.ProviderId ??= "";

            return schema;
        }
    }
} 