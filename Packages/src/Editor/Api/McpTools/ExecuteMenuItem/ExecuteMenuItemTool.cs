using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using uLoopMCP.Editor.Api.Commands.GetMenuItems;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// ExecuteMenuItem tool handler - Executes Unity MenuItems by path
    /// Supports both EditorApplication.ExecuteMenuItem and reflection-based execution
    /// </summary>
    [McpTool(
        RequiredSecuritySetting = SecuritySettings.AllowMenuItemExecution,
        Description = "Execute Unity MenuItem by path"
    )]
    public class ExecuteMenuItemTool : AbstractUnityTool<ExecuteMenuItemSchema, ExecuteMenuItemResponse>
    {
        public override string ToolName => "execute-menu-item";

        protected override Task<ExecuteMenuItemResponse> ExecuteAsync(ExecuteMenuItemSchema parameters, CancellationToken cancellationToken)
        {
            // Type-safe parameter access
            string menuItemPath = parameters.MenuItemPath;
            bool useReflectionFallback = parameters.UseReflectionFallback;
            
            ExecuteMenuItemResponse response = new ExecuteMenuItemResponse
            {
                MenuItemPath = menuItemPath
            };
            
            // Validate parameters
            if (string.IsNullOrEmpty(menuItemPath))
            {
                response.Success = false;
                response.ErrorMessage = "MenuItemPath cannot be empty";
                response.MenuItemFound = false;
                return Task.FromResult(response);
            }
            
            // First, try using EditorApplication.ExecuteMenuItem
            bool success = TryExecuteViaEditorApplication(menuItemPath, response);
            
            // If that fails and reflection fallback is enabled, try reflection
            if (!success && useReflectionFallback)
            {
                success = TryExecuteViaReflection(menuItemPath, response);
            }
            
            if (!success)
            {
                response.Success = false;
                if (string.IsNullOrEmpty(response.ErrorMessage))
                {
                    response.ErrorMessage = $"Failed to execute MenuItem: {menuItemPath}";
                }
            }
            
            return Task.FromResult(response);
        }

        /// <summary>
        /// Attempts to execute MenuItem using EditorApplication.ExecuteMenuItem
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
        /// Attempts to execute MenuItem using reflection to find and invoke the method directly
        /// </summary>
        private bool TryExecuteViaReflection(string menuItemPath, ExecuteMenuItemResponse response)
        {
            // Find the MenuItem method using the service
            MenuItemInfo menuItemInfo = MenuItemDiscoveryService.FindMenuItemByPath(menuItemPath);
            
            if (menuItemInfo == null)
            {
                response.ExecutionMethod = "Reflection";
                response.ErrorMessage = "MenuItem not found via reflection";
                response.MenuItemFound = false;
                response.Details = $"Could not find MenuItem with path: {menuItemPath}";
                return false;
            }
            
            // Don't execute validation functions
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
            
            // Get the method and invoke it
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
            
            // Invoke the method
            method.Invoke(null, null);
            
            response.Success = true;
            response.ExecutionMethod = "Reflection";
            response.MenuItemFound = true;
            response.Details = $"MenuItem executed successfully via reflection ({menuItemInfo.TypeName}.{menuItemInfo.MethodName})";
            return true;
        }
        
        /// <summary>
        /// Security: Validate if the type name is safe to load
        /// </summary>
        private bool IsValidMenuItemTypeName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return false;
            }
            
            // Check if type starts with allowed namespace
            foreach (string allowedNamespace in McpConstants.ALLOWED_NAMESPACES)
            {
                if (typeName.StartsWith(allowedNamespace, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            
            // Deny dangerous system types
            foreach (string deniedType in McpConstants.DENIED_SYSTEM_TYPES)
            {
                if (typeName.StartsWith(deniedType, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            
            return false; // Default deny for security
        }
    }
}