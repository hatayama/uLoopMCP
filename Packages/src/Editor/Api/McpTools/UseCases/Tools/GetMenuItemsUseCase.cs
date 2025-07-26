using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Responsible for temporal cohesion of MenuItem retrieval processing
    /// Processing sequence: 1. MenuItem discovery, 2. Apply filtering, 3. Apply count limit, 4. Create response
    /// Related classes: GetMenuItemsTool, MenuItemDiscoveryService
    /// Design reference: @Packages/docs/ARCHITECTURE_Unity.md - UseCase + Tool Pattern (DDD Integration)
    /// </summary>
    public class GetMenuItemsUseCase : AbstractUseCase<GetMenuItemsSchema, GetMenuItemsResponse>
    {
        /// <summary>
        /// Execute MenuItem retrieval processing
        /// </summary>
        /// <param name="parameters">MenuItem retrieval parameters</param>
        /// <param name="cancellationToken">Cancellation control token</param>
        /// <returns>MenuItem retrieval result</returns>
        public override Task<GetMenuItemsResponse> ExecuteAsync(GetMenuItemsSchema parameters, CancellationToken cancellationToken)
        {
            // 1. MenuItem discovery
            cancellationToken.ThrowIfCancellationRequested();
            
            List<MenuItemInfo> allMenuItems = MenuItemDiscoveryService.DiscoverAllMenuItems();
            
            // 2. Apply filtering
            cancellationToken.ThrowIfCancellationRequested();
            
            List<MenuItemInfo> filteredMenuItems = ApplyFiltering(
                allMenuItems, 
                parameters.FilterText, 
                parameters.FilterType, 
                parameters.IncludeValidation);
            
            // 3. Apply count limit
            if (filteredMenuItems.Count > parameters.MaxCount)
            {
                filteredMenuItems = filteredMenuItems.Take(parameters.MaxCount).ToList();
            }
            
            // 4. Create response
            GetMenuItemsResponse response = new GetMenuItemsResponse
            {
                MenuItems = filteredMenuItems,
                TotalCount = allMenuItems.Count,
                FilteredCount = filteredMenuItems.Count,
                AppliedFilter = parameters.FilterText,
                AppliedFilterType = parameters.FilterType.ToString()
            };
            
            return Task.FromResult(response);
        }

        /// <summary>
        /// Apply filtering to MenuItem list with specified conditions
        /// </summary>
        private List<MenuItemInfo> ApplyFiltering(
            List<MenuItemInfo> allMenuItems, 
            string filterText, 
            MenuItemFilterType filterType,
            bool includeValidation)
        {
            List<MenuItemInfo> filtered = allMenuItems;
            
            // Filter by validation function inclusion
            if (!includeValidation)
            {
                filtered = filtered.Where(item => !item.IsValidateFunction).ToList();
            }
            
            // Apply text filtering
            if (!string.IsNullOrEmpty(filterText))
            {
                filtered = ApplyTextFilter(filtered, filterText, filterType);
            }
            
            return filtered;
        }

        /// <summary>
        /// Apply text filtering based on specified filter type
        /// </summary>
        private List<MenuItemInfo> ApplyTextFilter(
            List<MenuItemInfo> menuItems, 
            string filterText, 
            MenuItemFilterType filterType)
        {
            return filterType switch
            {
                MenuItemFilterType.exact => menuItems.Where(item => 
                    string.Equals(item.Path, filterText, System.StringComparison.OrdinalIgnoreCase)).ToList(),
                    
                MenuItemFilterType.startswith => menuItems.Where(item => 
                    item.Path.StartsWith(filterText, System.StringComparison.OrdinalIgnoreCase)).ToList(),
                    
                MenuItemFilterType.contains => menuItems.Where(item => 
                    item.Path.IndexOf(filterText, System.StringComparison.OrdinalIgnoreCase) >= 0).ToList(),
                    
                _ => menuItems
            };
        }
    }
}