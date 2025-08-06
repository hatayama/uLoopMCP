using System;
using System.Reflection;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Represents information about a Unity MenuItem
    /// </summary>
    [Serializable]
    public class MenuItemInfo
    {
        /// <summary>
        /// The menu item path (e.g., "Window/General/Console")
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// The priority of the menu item (for ordering)
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Whether this is a validation function
        /// </summary>
        public bool IsValidateFunction { get; set; }

        /// <summary>
        /// The name of the method that implements this menu item
        /// </summary>
        public string MethodName { get; set; }

        /// <summary>
        /// The full name of the type that contains the method
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// The assembly name where the method is defined
        /// </summary>
        public string AssemblyName { get; set; }

        /// <summary>
        /// Whether the menu item can be executed via EditorApplication.ExecuteMenuItem
        /// </summary>
        public bool CanExecuteViaEditorApplication { get; set; }

        /// <summary>
        /// Warning message if there are issues with this MenuItem (e.g., duplicate attributes)
        /// </summary>
        public string WarningMessage { get; set; }

        public MenuItemInfo()
        {
            Path = string.Empty;
            MethodName = string.Empty;
            TypeName = string.Empty;
            AssemblyName = string.Empty;
            WarningMessage = string.Empty;
        }

        public MenuItemInfo(string path, MethodInfo method, int priority, bool isValidateFunction)
        {
            Path = path ?? string.Empty;
            Priority = priority;
            IsValidateFunction = isValidateFunction;
            MethodName = method?.Name ?? string.Empty;
            TypeName = method?.DeclaringType?.FullName ?? string.Empty;
            AssemblyName = method?.DeclaringType?.Assembly?.GetName()?.Name ?? string.Empty;
            CanExecuteViaEditorApplication = true; // Default to true, can be overridden
            WarningMessage = string.Empty;
        }

        public override string ToString()
        {
            return $"MenuItem: {Path} (Priority: {Priority}, Validate: {IsValidateFunction}, Method: {TypeName}.{MethodName})";
        }
    }
}