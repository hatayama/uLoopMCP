using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Response for GetMenuItems tool
    /// Contains discovered Unity MenuItems with filter information
    /// </summary>
    public class GetMenuItemsResponse : BaseToolResponse
    {
        /// <summary>
        /// List of discovered MenuItems matching the filter criteria
        /// </summary>
        public List<MenuItemInfo> MenuItems { get; set; }

        /// <summary>
        /// Total number of MenuItems discovered before filtering
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Number of MenuItems returned after filtering  
        /// </summary>
        public int FilteredCount { get; set; }

        /// <summary>
        /// The filter text that was applied
        /// </summary>
        public string AppliedFilter { get; set; }

        /// <summary>
        /// The filter type that was applied
        /// </summary>
        public string AppliedFilterType { get; set; }

        public GetMenuItemsResponse()
        {
            MenuItems = new List<MenuItemInfo>();
            AppliedFilter = string.Empty;
            AppliedFilterType = string.Empty;
        }
    }
}