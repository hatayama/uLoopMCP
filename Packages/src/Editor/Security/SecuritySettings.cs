using System.ComponentModel;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Defines enum for security settings types
    /// Each security setting controls the execution of specific commands
    /// 
    /// Related classes:
    /// - McpSecurityChecker: Main security check logic
    /// - McpToolAttribute: Attribute to specify command security settings
    /// - ExecuteMenuItemTool: Menu item execution tool
    /// - RunTestsTool: Test execution tool
    /// </summary>
    public enum SecuritySettings
    {
        /// <summary>
        /// No security setting required (default)
        /// </summary>
        [Description("")]
        None,

        /// <summary>
        /// Allow test execution setting
        /// Used by run-tests command
        /// </summary>
        [Description("enableTestsExecution")]
        EnableTestsExecution,

        /// <summary>
        /// Allow menu item execution setting
        /// Used by execute-menu-item command
        /// </summary>
        [Description("allowMenuItemExecution")]
        AllowMenuItemExecution,

        /// <summary>
        /// Allow play mode control setting
        /// Used by play-stop-unity command
        /// </summary>
        [Description("allowPlayModeControl")]
        AllowPlayModeControl
    }

    /// <summary>
    /// Extension methods for SecuritySettings enum
    /// </summary>
    public static class SecuritySettingsExtensions
    {
        /// <summary>
        /// Get string value from SecuritySettings enum
        /// </summary>
        /// <param name="setting">SecuritySettings enum value</param>
        /// <returns>Corresponding string value</returns>
        public static string ToStringValue(this SecuritySettings setting)
        {
            var field = setting.GetType().GetField(setting.ToString());
            var attribute = (DescriptionAttribute)System.Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute));
            return attribute?.Description ?? setting.ToString();
        }
    }
}