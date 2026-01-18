using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Compile request data for session management.
    /// </summary>
    [Serializable]
    public class CompileRequestData
    {
        public string requestId;
        public string json;
    }

    /// <summary>
    /// Unity MCP Editor settings data.
    /// </summary>
    [Serializable]
    public record McpEditorSettingsData
    {
        public int customPort = McpServerConfig.DEFAULT_PORT;
        public bool autoStartServer = true;
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
        
        // Dynamic Code Security Settings
        public int dynamicCodeSecurityLevel = (int)DynamicCodeSecurityLevel.Disabled;
        
        // Repository Root Toggle
        public bool addRepositoryRoot = false;
        
        // First Launch Completion Flag
        public bool hasCompletedFirstLaunch = false;
        
        // Session State Settings (moved from McpSessionManager)
        public bool isServerRunning = false;
        public int serverPort = McpServerConfig.DEFAULT_PORT;
        public bool isAfterCompile = false;
        public bool isDomainReloadInProgress = false;
        public bool isReconnecting = false;
        public bool showReconnectingUI = false;
        public bool showPostCompileReconnectingUI = false;
        public int selectedEditorType = (int)McpEditorType.Cursor;
        public float communicationLogHeight = McpUIConstants.DEFAULT_COMMUNICATION_LOG_HEIGHT;
        public string communicationLogsJson = "[]";
        public string pendingRequestsJson = "{}";
        public bool compileWindowHasData = false;
        public string[] pendingCompileRequestIds = new string[0];
        public CompileRequestData[] compileRequests = new CompileRequestData[0];
        public ConnectedLLMToolData[] connectedLLMTools = new ConnectedLLMToolData[0];
    }

    /// <summary>
    /// Management class for Unity MCP Editor settings.
    /// Saves as a JSON file in the UserSettings folder.
    /// </summary>
    public static class McpEditorSettings
    {
        private const string ROSLYN_SYMBOL = "ULOOPMCP_HAS_ROSLYN";

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
            
            _ = WriteSettingsFileWithRetryAsync(SettingsFilePath, json);
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
        /// Gets the repository root usage flag.
        /// </summary>
        public static bool GetAddRepositoryRoot()
        {
            return GetSettings().addRepositoryRoot;
        }
        
        /// <summary>
        /// Sets the repository root usage flag.
        /// </summary>
        public static void SetAddRepositoryRoot(bool addRepositoryRoot)
        {
            McpEditorSettingsData settings = GetSettings();
            McpEditorSettingsData newSettings = settings with { addRepositoryRoot = addRepositoryRoot };
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Gets the first launch completion flag.
        /// </summary>
        public static bool GetHasCompletedFirstLaunch()
        {
            return GetSettings().hasCompletedFirstLaunch;
        }

        /// <summary>
        /// Sets the first launch completion flag.
        /// </summary>
        public static void SetHasCompletedFirstLaunch(bool hasCompletedFirstLaunch)
        {
            McpEditorSettingsData settings = GetSettings();
            McpEditorSettingsData newSettings = settings with { hasCompletedFirstLaunch = hasCompletedFirstLaunch };
            SaveSettings(newSettings);
        }
        
        // Session State Settings Methods (moved from McpSessionManager)

        /// <summary>
        /// Gets the server running state.
        /// </summary>
        public static bool GetIsServerRunning()
        {
            return GetSettings().isServerRunning;
        }

        /// <summary>
        /// Sets the server running state.
        /// </summary>
        public static void SetIsServerRunning(bool isServerRunning)
        {
            McpEditorSettingsData settings = GetSettings();
            McpEditorSettingsData newSettings = settings with { isServerRunning = isServerRunning };
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Gets the server port.
        /// </summary>
        public static int GetServerPort()
        {
            return GetSettings().serverPort;
        }

        /// <summary>
        /// Sets the server port.
        /// </summary>
        public static void SetServerPort(int serverPort)
        {
            McpEditorSettingsData settings = GetSettings();
            McpEditorSettingsData newSettings = settings with { serverPort = serverPort };
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Gets the after compile flag.
        /// </summary>
        public static bool GetIsAfterCompile()
        {
            return GetSettings().isAfterCompile;
        }

        /// <summary>
        /// Sets the after compile flag.
        /// </summary>
        public static void SetIsAfterCompile(bool isAfterCompile)
        {
            McpEditorSettingsData settings = GetSettings();
            McpEditorSettingsData newSettings = settings with { isAfterCompile = isAfterCompile };
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Gets the domain reload in progress flag.
        /// </summary>
        public static bool GetIsDomainReloadInProgress()
        {
            return GetSettings().isDomainReloadInProgress;
        }

        /// <summary>
        /// Sets the domain reload in progress flag.
        /// </summary>
        public static void SetIsDomainReloadInProgress(bool isDomainReloadInProgress)
        {
            McpEditorSettingsData settings = GetSettings();
            McpEditorSettingsData newSettings = settings with { isDomainReloadInProgress = isDomainReloadInProgress };
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Gets the reconnecting flag.
        /// </summary>
        public static bool GetIsReconnecting()
        {
            return GetSettings().isReconnecting;
        }

        /// <summary>
        /// Sets the reconnecting flag.
        /// </summary>
        public static void SetIsReconnecting(bool isReconnecting)
        {
            McpEditorSettingsData settings = GetSettings();
            McpEditorSettingsData newSettings = settings with { isReconnecting = isReconnecting };
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Gets the show reconnecting UI flag.
        /// </summary>
        public static bool GetShowReconnectingUI()
        {
            return GetSettings().showReconnectingUI;
        }

        /// <summary>
        /// Sets the show reconnecting UI flag.
        /// </summary>
        public static void SetShowReconnectingUI(bool showReconnectingUI)
        {
            McpEditorSettingsData settings = GetSettings();
            McpEditorSettingsData newSettings = settings with { showReconnectingUI = showReconnectingUI };
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Gets the show post compile reconnecting UI flag.
        /// </summary>
        public static bool GetShowPostCompileReconnectingUI()
        {
            return GetSettings().showPostCompileReconnectingUI;
        }

        /// <summary>
        /// Sets the show post compile reconnecting UI flag.
        /// </summary>
        public static void SetShowPostCompileReconnectingUI(bool showPostCompileReconnectingUI)
        {
            McpEditorSettingsData settings = GetSettings();
            McpEditorSettingsData newSettings = settings with { showPostCompileReconnectingUI = showPostCompileReconnectingUI };
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Gets the selected editor type.
        /// </summary>
        public static McpEditorType GetSelectedEditorType()
        {
            return (McpEditorType)GetSettings().selectedEditorType;
        }

        /// <summary>
        /// Sets the selected editor type.
        /// </summary>
        public static void SetSelectedEditorType(McpEditorType selectedEditorType)
        {
            McpEditorSettingsData settings = GetSettings();
            McpEditorSettingsData newSettings = settings with { selectedEditorType = (int)selectedEditorType };
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Gets the communication log height.
        /// </summary>
        public static float GetCommunicationLogHeight()
        {
            return GetSettings().communicationLogHeight;
        }

        /// <summary>
        /// Sets the communication log height.
        /// </summary>
        public static void SetCommunicationLogHeight(float communicationLogHeight)
        {
            McpEditorSettingsData settings = GetSettings();
            McpEditorSettingsData newSettings = settings with { communicationLogHeight = communicationLogHeight };
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Gets the communication logs JSON.
        /// </summary>
        public static string GetCommunicationLogsJson()
        {
            return GetSettings().communicationLogsJson;
        }

        /// <summary>
        /// Sets the communication logs JSON.
        /// </summary>
        public static void SetCommunicationLogsJson(string communicationLogsJson)
        {
            McpEditorSettingsData settings = GetSettings();
            McpEditorSettingsData newSettings = settings with { communicationLogsJson = communicationLogsJson };
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Gets the pending requests JSON.
        /// </summary>
        public static string GetPendingRequestsJson()
        {
            return GetSettings().pendingRequestsJson;
        }

        /// <summary>
        /// Sets the pending requests JSON.
        /// </summary>
        public static void SetPendingRequestsJson(string pendingRequestsJson)
        {
            McpEditorSettingsData settings = GetSettings();
            McpEditorSettingsData newSettings = settings with { pendingRequestsJson = pendingRequestsJson };
            SaveSettings(newSettings);
        }


        /// <summary>
        /// Gets the compile window has data flag.
        /// </summary>
        public static bool GetCompileWindowHasData()
        {
            return GetSettings().compileWindowHasData;
        }

        /// <summary>
        /// Sets the compile window has data flag.
        /// </summary>
        public static void SetCompileWindowHasData(bool compileWindowHasData)
        {
            McpEditorSettingsData settings = GetSettings();
            McpEditorSettingsData newSettings = settings with { compileWindowHasData = compileWindowHasData };
            SaveSettings(newSettings);
        }

        // Helper methods that mirror McpSessionManager functionality

        /// <summary>
        /// Clear server session.
        /// </summary>
        public static void ClearServerSession()
        {
            SetIsServerRunning(false);
        }

        /// <summary>
        /// Clear after compile flag.
        /// </summary>
        public static void ClearAfterCompileFlag()
        {
            SetIsAfterCompile(false);
        }

        /// <summary>
        /// Clear reconnecting flags.
        /// </summary>
        public static void ClearReconnectingFlags()
        {
            SetIsReconnecting(false);
            SetShowReconnectingUI(false);
        }

        /// <summary>
        /// Clear post compile reconnecting UI.
        /// </summary>
        public static void ClearPostCompileReconnectingUI()
        {
            SetShowPostCompileReconnectingUI(false);
        }

        /// <summary>
        /// Clear domain reload flag.
        /// </summary>
        public static void ClearDomainReloadFlag()
        {
            SetIsDomainReloadInProgress(false);
        }

        /// <summary>
        /// Clear communication logs.
        /// </summary>
        public static void ClearCommunicationLogs()
        {
            SetCommunicationLogsJson("[]");
            SetPendingRequestsJson("{}");
        }

        /// <summary>
        /// Clear compile window data.
        /// </summary>
        public static void ClearCompileWindowData()
        {
            SetCompileWindowHasData(false);
        }

        // Compile request management methods

        /// <summary>
        /// Gets the pending compile request IDs.
        /// </summary>
        public static string[] GetPendingCompileRequestIds()
        {
            return GetSettings().pendingCompileRequestIds;
        }

        /// <summary>
        /// Sets the pending compile request IDs.
        /// </summary>
        public static void SetPendingCompileRequestIds(string[] pendingCompileRequestIds)
        {
            McpEditorSettingsData settings = GetSettings();
            McpEditorSettingsData newSettings = settings with { pendingCompileRequestIds = pendingCompileRequestIds };
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Gets the compile requests.
        /// </summary>
        public static CompileRequestData[] GetCompileRequests()
        {
            return GetSettings().compileRequests;
        }

        /// <summary>
        /// Sets the compile requests.
        /// </summary>
        public static void SetCompileRequests(CompileRequestData[] compileRequests)
        {
            McpEditorSettingsData settings = GetSettings();
            McpEditorSettingsData newSettings = settings with { compileRequests = compileRequests };
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Gets the compile request JSON by request ID.
        /// </summary>
        public static string GetCompileRequestJson(string requestId)
        {
            CompileRequestData[] requests = GetCompileRequests();
            CompileRequestData request = System.Array.Find(requests, r => r.requestId == requestId);
            return request?.json;
        }

        /// <summary>
        /// Sets the compile request JSON for a specific request ID.
        /// </summary>
        public static void SetCompileRequestJson(string requestId, string json)
        {
            CompileRequestData[] requests = GetCompileRequests();
            CompileRequestData existingRequest = System.Array.Find(requests, r => r.requestId == requestId);
            
            if (existingRequest != null)
            {
                existingRequest.json = json;
            }
            else
            {
                CompileRequestData[] newRequests = new CompileRequestData[requests.Length + 1];
                System.Array.Copy(requests, newRequests, requests.Length);
                newRequests[requests.Length] = new CompileRequestData { requestId = requestId, json = json };
                requests = newRequests;
            }
            
            SetCompileRequests(requests);
        }

        /// <summary>
        /// Clears all compile requests.
        /// </summary>
        public static void ClearAllCompileRequests()
        {
            SetCompileRequests(new CompileRequestData[0]);
            SetPendingCompileRequestIds(new string[0]);
        }

        /// <summary>
        /// Adds a pending compile request.
        /// </summary>
        public static void AddPendingCompileRequest(string requestId)
        {
            string[] pendingIds = GetPendingCompileRequestIds();
            if (System.Array.IndexOf(pendingIds, requestId) == -1)
            {
                string[] newPendingIds = new string[pendingIds.Length + 1];
                System.Array.Copy(pendingIds, newPendingIds, pendingIds.Length);
                newPendingIds[pendingIds.Length] = requestId;
                SetPendingCompileRequestIds(newPendingIds);
            }
        }

        /// <summary>
        /// Removes a pending compile request.
        /// </summary>
        public static void RemovePendingCompileRequest(string requestId)
        {
            string[] pendingIds = GetPendingCompileRequestIds();
            int index = System.Array.IndexOf(pendingIds, requestId);
            if (index != -1)
            {
                string[] newPendingIds = new string[pendingIds.Length - 1];
                System.Array.Copy(pendingIds, 0, newPendingIds, 0, index);
                System.Array.Copy(pendingIds, index + 1, newPendingIds, index, pendingIds.Length - index - 1);
                SetPendingCompileRequestIds(newPendingIds);
            }
        }

        // Connected LLM Tools management methods

        /// <summary>
        /// Gets the connected LLM tools.
        /// </summary>
        public static ConnectedLLMToolData[] GetConnectedLLMTools()
        {
            return GetSettings().connectedLLMTools;
        }

        /// <summary>
        /// Sets the connected LLM tools.
        /// </summary>
        public static void SetConnectedLLMTools(ConnectedLLMToolData[] connectedLLMTools)
        {
            McpEditorSettingsData settings = GetSettings();
            McpEditorSettingsData newSettings = settings with { connectedLLMTools = connectedLLMTools };
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Adds a connected LLM tool.
        /// </summary>
        public static void AddConnectedLLMTool(ConnectedLLMToolData toolData)
        {
            if (toolData == null || string.IsNullOrEmpty(toolData.Name))
            {
                return;
            }

            ConnectedLLMToolData[] tools = GetConnectedLLMTools();
            
            // Remove existing tool with same name if present
            ConnectedLLMToolData[] filteredTools = System.Array.FindAll(tools, t => t.Name != toolData.Name);
            
            // Add new tool
            ConnectedLLMToolData[] newTools = new ConnectedLLMToolData[filteredTools.Length + 1];
            System.Array.Copy(filteredTools, newTools, filteredTools.Length);
            newTools[filteredTools.Length] = toolData;
            
            SetConnectedLLMTools(newTools);
        }

        /// <summary>
        /// Removes a connected LLM tool by name.
        /// </summary>
        public static void RemoveConnectedLLMTool(string toolName)
        {
            if (string.IsNullOrEmpty(toolName))
            {
                return;
            }

            ConnectedLLMToolData[] tools = GetConnectedLLMTools();
            ConnectedLLMToolData[] newTools = System.Array.FindAll(tools, t => t.Name != toolName);
            
            if (newTools.Length != tools.Length)
            {
                SetConnectedLLMTools(newTools);
            }
        }

        /// <summary>
        /// Clears all connected LLM tools.
        /// </summary>
        public static void ClearConnectedLLMTools()
        {
            SetConnectedLLMTools(new ConnectedLLMToolData[0]);
        }

        /// <summary>
        /// Gets the dynamic code security level.
        /// </summary>
        public static DynamicCodeSecurityLevel GetDynamicCodeSecurityLevel()
        {
            return (DynamicCodeSecurityLevel)GetSettings().dynamicCodeSecurityLevel;
        }

        /// <summary>
        /// Sets the dynamic code security level.
        /// </summary>
        public static void SetDynamicCodeSecurityLevel(DynamicCodeSecurityLevel level)
        {
            McpEditorSettingsData settings = GetSettings();
            McpEditorSettingsData newSettings = settings with { dynamicCodeSecurityLevel = (int)level };
            SaveSettings(newSettings);
            
            // Manage ULOOPMCP_HAS_ROSLYN Define Symbol
            UpdateRoslynDefineSymbol(level);
            
            VibeLogger.LogInfo(
                "editor_settings_security_level_changed",
                $"Security level changed to: {level}",
                new { level = level.ToString() },
                correlationId: McpConstants.GenerateCorrelationId(),
                humanNote: "Security level updated in editor settings",
                aiTodo: "Monitor security level changes"
            );
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

                    // JsonUtility uses default field values when keys are missing from JSON,
                    // so hasCompletedFirstLaunch will be false for legacy configs that lack the field.
                    bool isLegacyConfig = !json.Contains("hasCompletedFirstLaunch");
                    
                    _cachedSettings = JsonUtility.FromJson<McpEditorSettingsData>(json);
                    
                    // Existing users upgrading from older versions should retain auto-start behavior
                    if (isLegacyConfig)
                    {
                        _cachedSettings = _cachedSettings with { hasCompletedFirstLaunch = true };
                        SaveSettings(_cachedSettings);
                    }
                }
                else
                {
                    // Create default settings.
                    _cachedSettings = new McpEditorSettingsData();
                    SaveSettings(_cachedSettings);
                }
            }
            catch (Exception ex)
            {
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

        /// <summary>
        /// Writes settings file with retry mechanism to handle sharing violations.
        /// </summary>
        /// <param name="filePath">Path to settings file</param>
        /// <param name="content">JSON content to write</param>
        private static async Task WriteSettingsFileWithRetryAsync(string filePath, string content)
        {
            const int maxRetries = 3;
            const int retryDelayMs = 50;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    File.WriteAllText(filePath, content);
                    return; // Success
                }
                catch (IOException ex) when (IsFileSharingViolation(ex))
                {
                    if (attempt == maxRetries)
                    {
                        UnityEngine.Debug.LogError($"Failed to write settings file after {maxRetries} attempts: {ex.Message}");
                        return; // Give up gracefully instead of throwing
                    }

                    // Wait before retry using TimerDelay (Unity-safe async delay)
                    await TimerDelay.Wait(retryDelayMs * attempt);
                }
            }
        }

        /// <summary>
        /// Checks if IOException is a file sharing violation based on HResult.
        /// </summary>
        /// <param name="ex">IOException to check</param>
        /// <returns>True if it's a sharing violation</returns>
        private static bool IsFileSharingViolation(IOException ex)
        {
            // Windows ERROR_SHARING_VIOLATION = 0x80070020
            // Windows ERROR_LOCK_VIOLATION = 0x80070021
            // macOS/Unix may have different error codes but IOException base handling should work
            const int ERROR_SHARING_VIOLATION = unchecked((int)0x80070020);
            const int ERROR_LOCK_VIOLATION = unchecked((int)0x80070021);
            
            return ex.HResult == ERROR_SHARING_VIOLATION || 
                   ex.HResult == ERROR_LOCK_VIOLATION;
        }
        
        /// <summary>
/// Retrieve NamedBuildTarget for configured platforms
        /// </summary>
        private static NamedBuildTarget[] GetAllKnownTargets()
        {
            List<NamedBuildTarget> targets = new();

            // Always include the active build target (even if it has no symbols yet)
            BuildTargetGroup activeGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            if (activeGroup == BuildTargetGroup.Unknown)
            {
                activeGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            }
            
            if (activeGroup != BuildTargetGroup.Unknown)
            {
                NamedBuildTarget activeTarget = NamedBuildTarget.FromBuildTargetGroup(activeGroup);
                if (!targets.Contains(activeTarget))
                {
                    targets.Add(activeTarget);
                }
            }
            else
            {
                // Fallback to Standalone when no active group is detected
                if (!targets.Contains(NamedBuildTarget.Standalone))
                {
                    targets.Add(NamedBuildTarget.Standalone);
                }
            }

            // Platforms supported in Unity 2022.3 LTS
            NamedBuildTarget[] candidateTargets = new[]
            {
                NamedBuildTarget.Standalone,
                NamedBuildTarget.Server,
                NamedBuildTarget.iOS,
                NamedBuildTarget.Android,
                NamedBuildTarget.WebGL,
                NamedBuildTarget.WindowsStoreApps,
                NamedBuildTarget.tvOS,
                NamedBuildTarget.PS4,
                NamedBuildTarget.XboxOne,
            };
            

            foreach (NamedBuildTarget target in candidateTargets)
            {
                string symbols = PlayerSettings.GetScriptingDefineSymbols(target);
                if (!string.IsNullOrEmpty(symbols) && !targets.Contains(target))
                {
                    targets.Add(target);
                }
            }
            

            return targets.ToArray();
        }
        
        /// <summary>
        /// Updates ULOOPMCP_HAS_ROSLYN Define Symbol based on security level
        /// </summary>
        private static void UpdateRoslynDefineSymbol(DynamicCodeSecurityLevel level)
        {
            string correlationId = McpConstants.GenerateCorrelationId();
            
            // Retrieve all configured platforms
            NamedBuildTarget[] targets = GetAllKnownTargets();
            
            foreach (NamedBuildTarget target in targets)
            {
                string currentSymbols = PlayerSettings.GetScriptingDefineSymbols(target);
                List<string> symbols = currentSymbols
                    .Split(';')
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
                
                bool hasRoslynSymbol = symbols.Contains(ROSLYN_SYMBOL);
                bool shouldAddSymbol = level != DynamicCodeSecurityLevel.Disabled;
                
                // Add symbol only for levels other than Disabled, when symbol does not yet exist
                // Do not remove symbol when changing to Disabled (as per specification)
                if (shouldAddSymbol && !hasRoslynSymbol)
                {
                    // Add Roslyn symbol
                    symbols.Add(ROSLYN_SYMBOL);
                    string newSymbols = string.Join(";", symbols);
                    PlayerSettings.SetScriptingDefineSymbols(target, newSymbols);
                    
                    VibeLogger.LogInfo(
                        "roslyn_symbol_added_to_platform",
                        $"Added {ROSLYN_SYMBOL} to {target}",
                        new { 
                            platform = target.ToString(),
                            symbols = newSymbols,
                            level = level.ToString()
                        },
                        correlationId: correlationId,
                        humanNote: $"Activate Roslyn functionality on {target} platform",
                        aiTodo: "Verify symbol addition per platform"
                    );
                }
                // Note: Do not remove symbol even when changing to Disabled level (as per specification)
            }
        }
        
        /// <summary>
        /// Symbol check and automatic addition after domain reload
        /// </summary>
        [InitializeOnLoadMethod]
        private static void CheckRoslynSymbolOnDomainReload()
        {
            EditorApplication.delayCall += () =>
            {
                DynamicCodeSecurityLevel currentLevel = GetDynamicCodeSecurityLevel();
                
                // Add symbol unless Disabled (do not remove even in Disabled state)
                UpdateRoslynDefineSymbol(currentLevel);
            };
        }
    }
}