using System;
using System.Reflection;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Represents information about a Unity MenuItem discovered via reflection.
    /// </summary>
    [Serializable]
    public class MenuItemInfo
    {
        public string Path { get; set; } = string.Empty;
        public bool IsValidateFunction { get; set; }
        public string MethodName { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string WarningMessage { get; set; } = string.Empty;

        public MenuItemInfo(string path, MethodInfo method, bool isValidateFunction)
        {
            Path = path ?? string.Empty;
            IsValidateFunction = isValidateFunction;
            MethodName = method?.Name ?? string.Empty;
            TypeName = method?.DeclaringType?.FullName ?? string.Empty;
        }
    }
}
