using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using uLoopMCP.Editor.Api.Commands.GetMenuItems;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// MenuItem取得処理の時間的凝集を担当
    /// 処理順序：1. MenuItem発見, 2. フィルタリング適用, 3. 件数制限適用, 4. レスポンス作成
    /// 関連クラス: GetMenuItemsTool, MenuItemDiscoveryService
    /// 設計書参照: DDDリファクタリング仕様 - UseCase Layer
    /// </summary>
    public class GetMenuItemsUseCase : AbstractUseCase<GetMenuItemsSchema, GetMenuItemsResponse>
    {
        /// <summary>
        /// MenuItem取得処理を実行する
        /// </summary>
        /// <param name="parameters">MenuItem取得パラメータ</param>
        /// <param name="cancellationToken">キャンセレーション制御用トークン</param>
        /// <returns>MenuItem取得結果</returns>
        public override Task<GetMenuItemsResponse> ExecuteAsync(GetMenuItemsSchema parameters, CancellationToken cancellationToken)
        {
            // 1. MenuItem発見
            cancellationToken.ThrowIfCancellationRequested();
            
            List<MenuItemInfo> allMenuItems = MenuItemDiscoveryService.DiscoverAllMenuItems();
            
            // 2. フィルタリング適用
            cancellationToken.ThrowIfCancellationRequested();
            
            List<MenuItemInfo> filteredMenuItems = ApplyFiltering(
                allMenuItems, 
                parameters.FilterText, 
                parameters.FilterType, 
                parameters.IncludeValidation);
            
            // 3. 件数制限適用
            if (filteredMenuItems.Count > parameters.MaxCount)
            {
                filteredMenuItems = filteredMenuItems.Take(parameters.MaxCount).ToList();
            }
            
            // 4. レスポンス作成
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
        /// 指定された条件でMenuItemリストにフィルタリングを適用する
        /// </summary>
        private List<MenuItemInfo> ApplyFiltering(
            List<MenuItemInfo> allMenuItems, 
            string filterText, 
            MenuItemFilterType filterType,
            bool includeValidation)
        {
            List<MenuItemInfo> filtered = allMenuItems;
            
            // バリデーション関数の包含でフィルター
            if (!includeValidation)
            {
                filtered = filtered.Where(item => !item.IsValidateFunction).ToList();
            }
            
            // テキストフィルタリングを適用
            if (!string.IsNullOrEmpty(filterText))
            {
                filtered = ApplyTextFilter(filtered, filterText, filterType);
            }
            
            return filtered;
        }

        /// <summary>
        /// 指定されたフィルタータイプに基づいてテキストフィルタリングを適用する
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