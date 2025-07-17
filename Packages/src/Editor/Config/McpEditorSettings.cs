using System;
using System.IO;
using System.Security;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Unity MCP Editor settings data.
    /// </summary>
    [Serializable]
    public record McpEditorSettingsData
    {
        public int customPort = McpServerConfig.DEFAULT_PORT;
        public bool autoStartServer = false;
        public bool showDeveloperTools = false;
        public bool enableMcpLogs = false;
        public bool enableCommunicationLogs = false;
        public bool enableDevelopmentMode = false;
        public string lastUsedConfigPath = "";
        
        // Security Settings - Safe-by-Default
        public bool enableTestsExecution = false;
        public bool allowMenuItemExecution = false;
        public bool allowThirdPartyTools = false;
        
        // UI State Settings
        public bool showSecuritySettings = false;
    }

    /// <summary>
    /// Management class for Unity MCP Editor settings.
    /// Saves as a JSON file in the UserSettings folder.
    /// </summary>
    public static class McpEditorSettings
    {
        private static string SettingsFilePath => Path.Combine(McpConstants.USER_SETTINGS_FOLDER, McpConstants.SETTINGS_FILE_NAME);

        private static McpEditorSettingsData _cachedSettings;

        /// <summary>
        /// Gets the settings data.
        /// </summary>
        public static McpEditorSettingsData GetSettings()
        {
            if (_cachedSettings == null)
            {
                LoadSettings();
            }

            return _cachedSettings;
        }

        /// <summary>
        /// Saves the settings data.
        /// </summary>
        public static void SaveSettings(McpEditorSettingsData settings)
        {
            // Security: Validate settings file path
            if (!IsValidSettingsPath(SettingsFilePath))
            {
                throw new SecurityException($"Invalid settings file path: {SettingsFilePath}");
            }
            
            // Security: Ensure directory exists and create it safely
            string directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            string json = JsonUtility.ToJson(settings, true);
            
            // Security: Validate JSON content size
            if (json.Length > McpConstants.MAX_SETTINGS_SIZE_BYTES)
            {
                throw new SecurityException("Settings JSON content exceeds size limit");
            }
            
            File.WriteAllText(SettingsFilePath, json);
            _cachedSettings = settings;

            // MCP Editor settings saved
        }

        /// <summary>
        /// Gets the custom port number.
        /// </summary>
        public static int GetCustomPort()
        {
            return GetSettings().customPort;
        }

        /// <summary>
        /// Saves the custom port number.
        /// </summary>
        public static void SetCustomPort(int port)
        {
            McpEditorSettingsData settings = GetSettings();
            McpEditorSettingsData updatedSettings = settings with { customPort = port };
            SaveSettings(updatedSettings);
        }

        /// <summary>
        /// Gets the auto-start setting.
        /// </summary>
        public static bool GetAutoStartServer()
        {
            return GetSettings().autoStartServer;
        }

        /// <summary>
        /// Saves the auto-start setting.
        /// </summary>
        public static void SetAutoStartServer(bool autoStart)
        {
            McpEditorSettingsData settings = GetSettings();
            McpEditorSettingsData updatedSettings = settings with { autoStartServer = autoStart };
            SaveSettings(updatedSettings);
        }

        /// <summary>
        /// Gets the Developer Tools display setting.
        /// </summary>
        public static bool GetShowDeveloperTools()
        {
            return GetSettings().showDeveloperTools;
        }

        /// <summary>
        /// Saves the Developer Tools display setting.
        /// </summary>
        public static void SetShowDeveloperTools(bool show)
        {
            McpEditorSettingsData settings = GetSettings();
            McpEditorSettingsData updatedSettings = settings with { showDeveloperTools = show };
            SaveSettings(updatedSettings);
        }

        /// <summary>
        /// Gets the MCP log enabled flag.
        /// </summary>
        public static bool GetEnableMcpLogs()
        {
            return GetSettings().enableMcpLogs;
        }

        /// <summary>
        /// Sets the MCP log enabled flag.
        /// </summary>
        public static void SetEnableMcpLogs(bool enableMcpLogs)
        {
            McpEditorSettingsData settings = GetSettings();
            McpEditorSettingsData newSettings = settings with { enableMcpLogs = enableMcpLogs };
            SaveSettings(newSettings);

            // Synchronize McpLogger settings as well.
            McpLogger.EnableDebugLog = enableMcpLogs;
        }

        /// <summary>
        /// Gets the communication logs enabled flag.
        /// </summary>
        public static bool GetEnableCommunicationLogs()
        {
            return GetSettings().enableCommunicationLogs;
        }

        /// <summary>
        /// Sets the communication logs enabled flag.
        /// </summary>
        public static void SetEnableCommunicationLogs(bool enableCommunicationLogs)
        {
            McpEditorSettingsData settings = GetSettings();
            McpEditorSettingsData newSettings = settings with { enableCommunicationLogs = enableCommunicationLogs };
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Gets the development mode enabled flag.
        /// </summary>
        public static bool GetEnableDevelopmentMode()
        {
            return GetSettings().enableDevelopmentMode;
        }

        /// <summary>
        /// Sets the development mode enabled flag.
        /// </summary>
        public static void SetEnableDevelopmentMode(bool enableDevelopmentMode)
        {
            McpEditorSettingsData settings = GetSettings();
            McpEditorSettingsData newSettings = settings with { enableDevelopmentMode = enableDevelopmentMode };
            SaveSettings(newSettings);
        }

        // Security Settings Methods

        /// <summary>
        /// Gets the tests execution enabled flag.
        /// </summary>
        public static bool GetEnableTestsExecution()
        {
            return GetSettings().enableTestsExecution;
        }

        /// <summary>
        /// Sets the tests execution enabled flag.
        /// </summary>
        public static void SetEnableTestsExecution(bool enableTestsExecution)
        {
            McpEditorSettingsData settings = GetSettings();
            McpEditorSettingsData newSettings = settings with { enableTestsExecution = enableTestsExecution };
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Gets the menu item execution allowed flag.
        /// </summary>
        public static bool GetAllowMenuItemExecution()
        {
            return GetSettings().allowMenuItemExecution;
        }

        /// <summary>
        /// Sets the menu item execution allowed flag.
        /// </summary>
        public static void SetAllowMenuItemExecution(bool allowMenuItemExecution)
        {
            McpEditorSettingsData settings = GetSettings();
            McpEditorSettingsData newSettings = settings with { allowMenuItemExecution = allowMenuItemExecution };
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Gets the third party tools execution allowed flag.
        /// </summary>
        public static bool GetAllowThirdPartyTools()
        {
            return GetSettings().allowThirdPartyTools;
        }

        /// <summary>
        /// Sets the third party tools execution allowed flag.
        /// </summary>
        public static void SetAllowThirdPartyTools(bool allowThirdPartyTools)
        {
            McpEditorSettingsData settings = GetSettings();
            McpEditorSettingsData newSettings = settings with { allowThirdPartyTools = allowThirdPartyTools };
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Gets the show security settings flag.
        /// </summary>
        public static bool GetShowSecuritySettings()
        {
            return GetSettings().showSecuritySettings;
        }

        /// <summary>
        /// Sets the show security settings flag.
        /// </summary>
        public static void SetShowSecuritySettings(bool showSecuritySettings)
        {
            McpEditorSettingsData settings = GetSettings();
            McpEditorSettingsData newSettings = settings with { showSecuritySettings = showSecuritySettings };
            SaveSettings(newSettings);
        }


        /// <summary>
        /// Loads the settings file.
        /// </summary>
        private static void LoadSettings()
        {
            try
            {
                // Security: Validate settings file path
                if (!IsValidSettingsPath(SettingsFilePath))
                {
                    throw new SecurityException($"Invalid settings file path: {SettingsFilePath}");
                }
                
                if (File.Exists(SettingsFilePath))
                {
                    // Security: Check file size before reading
                    FileInfo fileInfo = new FileInfo(SettingsFilePath);
                    if (fileInfo.Length > McpConstants.MAX_SETTINGS_SIZE_BYTES)
                    {
                        throw new SecurityException("Settings file exceeds size limit");
                    }
                    
                    string json = File.ReadAllText(SettingsFilePath);
                    
                    // Security: Validate JSON content
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        throw new InvalidDataException("Settings file contains invalid JSON content");
                    }
                    
                    _cachedSettings = JsonUtility.FromJson<McpEditorSettingsData>(json);
                    // MCP Editor settings loaded
                }
                else
                {
                    // Create default settings.
                    _cachedSettings = new McpEditorSettingsData();
                    SaveSettings(_cachedSettings);
                    // Created default MCP Editor settings
                }
            }
            catch (Exception ex)
            {
                McpLogger.LogError($"Failed to load MCP Editor settings: {ex.Message}");
                // Don't suppress this exception - corrupted settings should be reported
                throw new InvalidOperationException(
                    $"Failed to load MCP Editor settings from: {SettingsFilePath}. Settings file may be corrupted.", ex);
            }
        }
        
        /// <summary>
        /// Security: Validate if the settings file path is safe
        /// </summary>
        private static bool IsValidSettingsPath(string path)
        {
            try
            {
                // Normalize the path to prevent path traversal
                string normalizedPath = Path.GetFullPath(path);
                
                // Must be under UserSettings directory
                string expectedUserSettingsPath = Path.GetFullPath(McpConstants.USER_SETTINGS_FOLDER);
                
                // Check if path is within the expected directory
                return normalizedPath.StartsWith(expectedUserSettingsPath, StringComparison.OrdinalIgnoreCase) &&
                       normalizedPath.EndsWith(McpConstants.SETTINGS_FILE_NAME, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"{McpConstants.SECURITY_LOG_PREFIX} Error validating settings path {path}: {ex.Message}");
                return false;
            }
        }
    }
}