using System.ComponentModel;

namespace io.github.hatayama.uLoopMCP
{
    public class UnitySearchProviderDetailsSchema : BaseToolSchema
    {
        [Description("Specific provider ID to get details for (empty = all providers). Examples: 'asset', 'scene', 'menu', 'settings'")]
        public string ProviderId { get; set; } = "";

        [Description("Whether to include only active providers")]
        public bool ActiveOnly { get; set; } = false;

        [Description("Sort providers by priority (lower number = higher priority)")]
        public bool SortByPriority { get; set; } = true;

        [Description("Include detailed descriptions for each provider")]
        public bool IncludeDescriptions { get; set; } = true;
    }
}
