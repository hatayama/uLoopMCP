using System;
using System.Linq;

namespace io.github.hatayama.uLoopMCP
{
    public class UnitySearchProviderDetailsResponse : BaseToolResponse
    {
        public ProviderInfo[] Providers { get; set; }

        public int TotalCount { get; set; }

        public int ActiveCount { get; set; }

        public int InactiveCount { get; set; }

        public bool Success { get; set; }

        public string ErrorMessage { get; set; }

        public string AppliedFilter { get; set; }

        public bool SortedByPriority { get; set; }

        public UnitySearchProviderDetailsResponse()
        {
            Providers = Array.Empty<ProviderInfo>();
            ErrorMessage = string.Empty;
            AppliedFilter = string.Empty;
            Success = true;
        }

        public UnitySearchProviderDetailsResponse(ProviderInfo[] providers, string appliedFilter, bool sortedByPriority)
        {
            Providers = providers ?? Array.Empty<ProviderInfo>();
            TotalCount = Providers.Length;
            ActiveCount = Providers.Count(p => p.IsActive);
            InactiveCount = TotalCount - ActiveCount;
            Success = true;
            ErrorMessage = string.Empty;
            AppliedFilter = appliedFilter ?? "all";
            SortedByPriority = sortedByPriority;
        }

        public UnitySearchProviderDetailsResponse(string errorMessage)
        {
            Providers = Array.Empty<ProviderInfo>();
            TotalCount = 0;
            ActiveCount = 0;
            InactiveCount = 0;
            Success = false;
            ErrorMessage = errorMessage ?? string.Empty;
            AppliedFilter = string.Empty;
            SortedByPriority = false;
        }
    }
}
