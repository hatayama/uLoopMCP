using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    [McpTool(Description = "Get detailed information about Unity Search providers including display names, descriptions, active status, and capabilities")]
    public class UnitySearchProviderDetailsTool : AbstractUnityTool<UnitySearchProviderDetailsSchema, UnitySearchProviderDetailsResponse>
    {
        public override string ToolName => "get-provider-details";

        protected override Task<UnitySearchProviderDetailsResponse> ExecuteAsync(UnitySearchProviderDetailsSchema parameters, CancellationToken cancellationToken)
        {
            ProviderInfo[] providers;
            string appliedFilter;

            if (!string.IsNullOrWhiteSpace(parameters.ProviderId))
            {
                ProviderInfo provider = UnitySearchService.GetProviderDetails(parameters.ProviderId);
                if (provider == null)
                {
                    return Task.FromResult(new UnitySearchProviderDetailsResponse($"Provider '{parameters.ProviderId}' not found"));
                }
                providers = new[] { provider };
                appliedFilter = parameters.ProviderId;
            }
            else
            {
                providers = UnitySearchService.GetProviderDetails();
                appliedFilter = "all";
            }

            if (parameters.ActiveOnly)
            {
                providers = providers.Where(p => p.IsActive).ToArray();
                appliedFilter += " (active only)";
            }

            if (!parameters.IncludeDescriptions)
            {
                foreach (ProviderInfo provider in providers)
                {
                    provider.Description = "";
                }
            }

            if (parameters.SortByPriority)
            {
                providers = providers.OrderBy(p => p.Priority).ToArray();
            }

            return Task.FromResult(new UnitySearchProviderDetailsResponse(providers, appliedFilter, parameters.SortByPriority));
        }

        protected override UnitySearchProviderDetailsSchema ApplyDefaultValues(UnitySearchProviderDetailsSchema schema)
        {
            schema.ProviderId ??= "";

            return schema;
        }
    }
}
