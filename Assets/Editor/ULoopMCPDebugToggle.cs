using UnityEditor;
using UnityEngine;
using System.Linq;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Unity Editor menu items for toggling ULOOPMCP_DEBUG scripting define symbol
    /// Provides easy access to enable/disable debug features in uLoopMCP
    /// Related classes:
    /// - McpEditorWindow: Uses ULOOPMCP_DEBUG to show/hide developer tools
    /// - McpLogger: Debug logging behavior controlled by this symbol
    /// </summary>
    public static class ULoopMCPDebugToggle
    {
        private const string MENU_PATH_ENABLE = "uLoopMCP/Tools/Debug Settings/Enable Debug Mode";
        private const string MENU_PATH_DISABLE = "uLoopMCP/Tools/Debug Settings/Disable Debug Mode";

        /// <summary>
        /// Check if ULOOPMCP_DEBUG symbol is currently defined
        /// </summary>
        private static bool IsDebugModeEnabled()
        {
            BuildTargetGroup targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
            return defines.Split(';').Contains(McpConstants.SCRIPTING_DEFINE_ULOOPMCP_DEBUG);
        }

        /// <summary>
        /// Enable ULOOPMCP_DEBUG scripting define symbol
        /// </summary>
        [MenuItem(MENU_PATH_ENABLE)]
        public static void EnableDebugMode()
        {
            if (IsDebugModeEnabled())
            {
                Debug.Log("[uLoopMCP] Debug mode is already enabled");
                return;
            }

            BuildTargetGroup targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
            
            if (string.IsNullOrEmpty(defines))
            {
                defines = McpConstants.SCRIPTING_DEFINE_ULOOPMCP_DEBUG;
            }
            else
            {
                defines += ";" + McpConstants.SCRIPTING_DEFINE_ULOOPMCP_DEBUG;
            }
            
            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, defines);
            Debug.Log("[uLoopMCP] Debug mode enabled. Unity will recompile scripts.");
        }

        /// <summary>
        /// Disable ULOOPMCP_DEBUG scripting define symbol
        /// </summary>
        [MenuItem(MENU_PATH_DISABLE)]
        public static void DisableDebugMode()
        {
            if (!IsDebugModeEnabled())
            {
                Debug.Log("[uLoopMCP] Debug mode is already disabled");
                return;
            }

            BuildTargetGroup targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
            
            string[] defineArray = defines.Split(';');
            defineArray = defineArray.Where(d => d != McpConstants.SCRIPTING_DEFINE_ULOOPMCP_DEBUG).ToArray();
            defines = string.Join(";", defineArray);
            
            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, defines);
            Debug.Log("[uLoopMCP] Debug mode disabled. Unity will recompile scripts.");
        }

        /// <summary>
        /// Validate menu item - only show Enable when debug mode is disabled
        /// </summary>
        [MenuItem(MENU_PATH_ENABLE, true)]
        public static bool ValidateEnableDebugMode()
        {
            return !IsDebugModeEnabled();
        }

        /// <summary>
        /// Validate menu item - only show Disable when debug mode is enabled
        /// </summary>
        [MenuItem(MENU_PATH_DISABLE, true)]
        public static bool ValidateDisableDebugMode()
        {
            return IsDebugModeEnabled();
        }
    }
}