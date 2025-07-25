using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using uLoopMCP.Editor.Api.Commands.GetMenuItems;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// MenuItem実行処理の時間的凝集を担当
    /// 処理順序：1. パラメータ検証, 2. EditorApplication経由実行, 3. Reflection経由実行（フォールバック）, 4. 結果作成
    /// 関連クラス: ExecuteMenuItemTool, MenuItemDiscoveryService
    /// 設計書参照: DDDリファクタリング仕様 - UseCase Layer
    /// </summary>
    public class ExecuteMenuItemUseCase : AbstractUseCase<ExecuteMenuItemSchema, ExecuteMenuItemResponse>
    {
        /// <summary>
        /// MenuItem実行処理を実行する
        /// </summary>
        /// <param name="parameters">MenuItem実行パラメータ</param>
        /// <param name="cancellationToken">キャンセレーション制御用トークン</param>
        /// <returns>MenuItem実行結果</returns>
        public override Task<ExecuteMenuItemResponse> ExecuteAsync(ExecuteMenuItemSchema parameters, CancellationToken cancellationToken)
        {
            ExecuteMenuItemResponse response = new ExecuteMenuItemResponse
            {
                MenuItemPath = parameters.MenuItemPath
            };
            
            // 1. パラメータ検証
            if (string.IsNullOrEmpty(parameters.MenuItemPath))
            {
                response.Success = false;
                response.ErrorMessage = "MenuItemPath cannot be empty";
                response.MenuItemFound = false;
                return Task.FromResult(response);
            }
            
            // 2. EditorApplication経由実行
            bool success = TryExecuteViaEditorApplication(parameters.MenuItemPath, response);
            
            // 3. Reflection経由実行（フォールバック）
            if (!success && parameters.UseReflectionFallback)
            {
                success = TryExecuteViaReflection(parameters.MenuItemPath, response);
            }
            
            // 4. 結果作成
            if (!success)
            {
                response.Success = false;
                if (string.IsNullOrEmpty(response.ErrorMessage))
                {
                    response.ErrorMessage = $"Failed to execute MenuItem: {parameters.MenuItemPath}";
                }
            }
            
            return Task.FromResult(response);
        }

        /// <summary>
        /// EditorApplication.ExecuteMenuItemを使用してMenuItem実行を試行する
        /// </summary>
        private bool TryExecuteViaEditorApplication(string menuItemPath, ExecuteMenuItemResponse response)
        {
            bool success = EditorApplication.ExecuteMenuItem(menuItemPath);
            
            if (success)
            {
                response.Success = true;
                response.ExecutionMethod = "EditorApplication";
                response.MenuItemFound = true;
                response.Details = "MenuItem executed successfully via EditorApplication.ExecuteMenuItem";
                return true;
            }
            
            response.ExecutionMethod = "EditorApplication";
            response.ErrorMessage = "EditorApplication.ExecuteMenuItem returned false";
            response.Details = "MenuItem may not exist or may not be executable via EditorApplication";
            return false;
        }

        /// <summary>
        /// Reflectionを使用してメソッドを直接見つけて実行する
        /// </summary>
        private bool TryExecuteViaReflection(string menuItemPath, ExecuteMenuItemResponse response)
        {
            // MenuItemメソッドをサービス経由で検索
            MenuItemInfo menuItemInfo = MenuItemDiscoveryService.FindMenuItemByPath(menuItemPath);
            
            if (menuItemInfo == null)
            {
                response.ExecutionMethod = "Reflection";
                response.ErrorMessage = "MenuItem not found via reflection";
                response.MenuItemFound = false;
                response.Details = $"Could not find MenuItem with path: {menuItemPath}";
                return false;
            }
            
            // バリデーション関数は実行しない
            if (menuItemInfo.IsValidateFunction)
            {
                response.ExecutionMethod = "Reflection";
                response.ErrorMessage = "Cannot execute validation function";
                response.MenuItemFound = true;
                response.Details = "The specified path is a validation function, not an executable MenuItem";
                return false;
            }
            
            // セキュリティ: ロード前に型名を検証
            if (!IsValidMenuItemTypeName(menuItemInfo.TypeName))
            {
                response.ExecutionMethod = "Reflection";
                response.ErrorMessage = "Invalid or restricted type name";
                response.MenuItemFound = true;
                response.Details = $"{McpConstants.SECURITY_LOG_PREFIX} Type name is not allowed for security reasons: {menuItemInfo.TypeName}";
                return false;
            }
            
            // メソッドを取得して実行
            Type type = Type.GetType(menuItemInfo.TypeName);
            if (type == null)
            {
                response.ExecutionMethod = "Reflection";
                response.ErrorMessage = "Could not load type";
                response.MenuItemFound = true;
                response.Details = $"Could not load type: {menuItemInfo.TypeName}";
                return false;
            }
            
            MethodInfo method = type.GetMethod(menuItemInfo.MethodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                response.ExecutionMethod = "Reflection";
                response.ErrorMessage = "Could not find method";
                response.MenuItemFound = true;
                response.Details = $"Could not find method: {menuItemInfo.MethodName}";
                return false;
            }
            
            // メソッドを実行
            method.Invoke(null, null);
            
            response.Success = true;
            response.ExecutionMethod = "Reflection";
            response.MenuItemFound = true;
            response.Details = $"MenuItem executed successfully via reflection ({menuItemInfo.TypeName}.{menuItemInfo.MethodName})";
            return true;
        }
        
        /// <summary>
        /// セキュリティ: 型名がロードしても安全かを検証
        /// </summary>
        private bool IsValidMenuItemTypeName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return false;
            }
            
            // 許可された名前空間で始まるかチェック
            foreach (string allowedNamespace in McpConstants.ALLOWED_NAMESPACES)
            {
                if (typeName.StartsWith(allowedNamespace, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            
            // 危険なシステム型を拒否
            foreach (string deniedType in McpConstants.DENIED_SYSTEM_TYPES)
            {
                if (typeName.StartsWith(deniedType, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            
            return false; // セキュリティのためデフォルトで拒否
        }
    }
}