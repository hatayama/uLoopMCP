using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Responsible for temporal cohesion of MenuItem execution processing
    /// Processing sequence: 1. Parameter validation, 2. Execute via EditorApplication, 3. Execute via Reflection (fallback), 4. Create result
    /// Related classes: ExecuteMenuItemTool, MenuItemDiscoveryService
    /// Design reference: @Packages/docs/ARCHITECTURE_Unity.md - UseCase + Tool Pattern (DDD Integration)
    /// </summary>
    public class ExecuteMenuItemUseCase : AbstractUseCase<ExecuteMenuItemSchema, ExecuteMenuItemResponse>
    {
        /// <summary>
        /// Execute MenuItem execution processing
        /// </summary>
        /// <param name="parameters">MenuItem execution parameters</param>
        /// <param name="cancellationToken">Cancellation control token</param>
        /// <returns>MenuItem execution result</returns>
        public override Task<ExecuteMenuItemResponse> ExecuteAsync(ExecuteMenuItemSchema parameters, CancellationToken cancellationToken)
        {
            ExecuteMenuItemResponse response = new ExecuteMenuItemResponse
            {
                MenuItemPath = parameters.MenuItemPath
            };
            
            // 1. Parameter validation
            if (string.IsNullOrEmpty(parameters.MenuItemPath))
            {
                response.Success = false;
                response.ErrorMessage = "MenuItemPath cannot be empty";
                response.MenuItemFound = false;
                return Task.FromResult(response);
            }
            
            // 2. Execute via EditorApplication
            bool success = TryExecuteViaEditorApplication(parameters.MenuItemPath, response);
            
            // 3. Execute via Reflection (fallback)
            if (!success && parameters.UseReflectionFallback)
            {
                success = TryExecuteViaReflection(parameters.MenuItemPath, response);
            }
            
            // 4. Create result
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
        /// Try to execute menuItem using EditorApplication.ExecuteMenuItem
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
        /// Use Reflection to directly find and execute method
        /// </summary>
        private bool TryExecuteViaReflection(string menuItemPath, ExecuteMenuItemResponse response)
        {
            // Search for MenuItem method via service
            MenuItemInfo menuItemInfo = MenuItemDiscoveryService.FindMenuItemByPath(menuItemPath);
            
            if (menuItemInfo == null)
            {
                response.ExecutionMethod = "Reflection";
                response.ErrorMessage = "MenuItem not found via reflection";
                response.MenuItemFound = false;
                response.Details = $"Could not find MenuItem with path: {menuItemPath}";
                return false;
            }
            
            // Do not execute validation functions
            if (menuItemInfo.IsValidateFunction)
            {
                response.ExecutionMethod = "Reflection";
                response.ErrorMessage = "Cannot execute validation function";
                response.MenuItemFound = true;
                response.Details = "The specified path is a validation function, not an executable MenuItem";
                return false;
            }
            
            // Security: Validate type name before loading
            if (!IsValidMenuItemTypeName(menuItemInfo.TypeName))
            {
                response.ExecutionMethod = "Reflection";
                response.ErrorMessage = "Invalid or restricted type name";
                response.MenuItemFound = true;
                response.Details = $"{McpConstants.SECURITY_LOG_PREFIX} Type name is not allowed for security reasons: {menuItemInfo.TypeName}";
                return false;
            }
            
            // Get method and execute
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
            
            // Execute method
            try
            {
                method.Invoke(null, null);
                
                response.Success = true;
                response.ExecutionMethod = "Reflection";
                response.MenuItemFound = true;
                
                // 基本的な実行成功メッセージ
                string details = $"MenuItem executed successfully via reflection ({menuItemInfo.TypeName}.{menuItemInfo.MethodName})";
                
                // 警告メッセージがある場合は追加
                if (!string.IsNullOrEmpty(menuItemInfo.WarningMessage))
                {
                    details += $"\nWarning: {menuItemInfo.WarningMessage}";
                }
                
                response.Details = details;
                return true;
            }
            catch (System.Exception ex)
            {
                response.ExecutionMethod = "Reflection";
                response.ErrorMessage = $"Exception during method execution: {ex.GetBaseException().Message}";
                response.MenuItemFound = true;
                response.Details = $"Failed to execute {menuItemInfo.TypeName}.{menuItemInfo.MethodName}";
                return false;
            }
        }
        
        /// <summary>
        /// Security: Validate if type name is safe to load
        /// </summary>
        private bool IsValidMenuItemTypeName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return false;
            }
            
            // Reject dangerous system types first
            foreach (string deniedType in McpConstants.DENIED_SYSTEM_TYPES)
            {
                if (typeName.StartsWith(deniedType, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            
            // Check if it starts with allowed namespace
            foreach (string allowedNamespace in McpConstants.ALLOWED_NAMESPACES)
            {
                if (typeName.StartsWith(allowedNamespace, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            
            return false; // Default deny for security
        }
    }
}