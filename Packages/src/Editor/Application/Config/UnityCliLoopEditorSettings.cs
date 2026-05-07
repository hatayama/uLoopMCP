using System;
using System.IO;
using System.Security;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace io.github.hatayama.UnityCliLoop
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
    /// Unity CLI Loop Editor settings data.
    /// </summary>
    [Serializable]
    public record UnityCliLoopEditorSettingsData
    {
        public bool showDeveloperTools = false;
        public string lastSeenSetupWizardVersion = "";
        public bool suppressSetupWizardAutoShow = false;

        // UI State Settings
        public bool showUnityCliLoopSecuritySetting = true;
        public bool showToolSettings = true;

        // Default to flat installation so first-time setup does not add an extra grouping layer unless requested.
        public bool installSkillsFlat = true;
        
        // Default to true so the server starts automatically on fresh install
        public bool isServerRunning = true;
        public bool isAfterCompile = false;
        public bool isDomainReloadInProgress = false;
        public bool isReconnecting = false;
        public bool showReconnectingUI = false;
        public bool showPostCompileReconnectingUI = false;
        public bool compileWindowHasData = false;
        public string[] pendingCompileRequestIds = new string[0];
        public CompileRequestData[] compileRequests = new CompileRequestData[0];
    }

    /// <summary>
    /// Management class for Unity CLI Loop Editor settings.
    /// Saves as a JSON file in the UserSettings folder.
    /// </summary>
    public sealed class UnityCliLoopEditorSettingsRepository
    {
        private string SettingsFilePath => Path.Combine(UnityCliLoopConstants.USER_SETTINGS_FOLDER, UnityCliLoopConstants.SETTINGS_FILE_NAME);
        private readonly string[] _legacyTransientSettingKeys =
        {
            "customPort",
            "serverPort",
            "port",
            "Port",
            "serverTransportKind",
            "projectRootPath",
            "serverSessionId",
            "connectedLLMTools"
        };

        private UnityCliLoopEditorSettingsData _cachedSettings;

        public void InvalidateCache()
        {
            _cachedSettings = null;
        }

        public void RecoverSettingsFileIfNeeded()
        {
            if (!IsValidSettingsPath(SettingsFilePath))
            {
                throw new SecurityException($"Invalid settings file path: {SettingsFilePath}");
            }

            AtomicFileWriter.RecoverSidecarFiles(SettingsFilePath);
            RemoveLegacyTransientFieldsIfNeeded(SettingsFilePath);
        }

        /// <summary>
        /// Gets the settings data.
        /// </summary>
        public UnityCliLoopEditorSettingsData GetSettings()
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
        public void SaveSettings(UnityCliLoopEditorSettingsData settings)
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
            if (json.Length > UnityCliLoopConstants.MAX_SETTINGS_SIZE_BYTES)
            {
                throw new SecurityException("Settings JSON content exceeds size limit");
            }
            
            AtomicFileWriter.Write(SettingsFilePath, json);
            _cachedSettings = settings;

            // Best-effort cleanup: even if this fails, .bak is overwritten on next save
            AtomicFileWriter.CleanupBackup(SettingsFilePath + ".bak");
        }

        /// <summary>
        /// Applies a transformation to the current settings and saves once.
        /// Use when multiple fields need to be updated together to avoid redundant writes.
        /// </summary>
        public void UpdateSettings(Func<UnityCliLoopEditorSettingsData, UnityCliLoopEditorSettingsData> transform)
        {
            Debug.Assert(transform != null, "transform must not be null");

            UnityCliLoopEditorSettingsData current = GetSettings();
            UnityCliLoopEditorSettingsData updated = transform(current);
            SaveSettings(updated);
        }

        /// <summary>
        /// Gets the Developer Tools display setting.
        /// </summary>
        public bool GetShowDeveloperTools()
        {
            return GetSettings().showDeveloperTools;
        }

        /// <summary>
        /// Saves the Developer Tools display setting.
        /// </summary>
        public void SetShowDeveloperTools(bool show)
        {
            UnityCliLoopEditorSettingsData settings = GetSettings();
            UnityCliLoopEditorSettingsData updatedSettings = settings with { showDeveloperTools = show };
            SaveSettings(updatedSettings);
        }

        public string GetLastSeenSetupWizardVersion()
        {
            return GetSettings().lastSeenSetupWizardVersion ?? string.Empty;
        }

        public void SetLastSeenSetupWizardVersion(string version)
        {
            string normalizedVersion = version ?? string.Empty;
            UnityCliLoopEditorSettingsData settings = GetSettings();
            UnityCliLoopEditorSettingsData updatedSettings = settings with { lastSeenSetupWizardVersion = normalizedVersion };
            SaveSettings(updatedSettings);
        }

        public bool GetSuppressSetupWizardAutoShow()
        {
            return GetSettings().suppressSetupWizardAutoShow;
        }

        public void SetSuppressSetupWizardAutoShow(bool suppressAutoShow)
        {
            UnityCliLoopEditorSettingsData settings = GetSettings();
            UnityCliLoopEditorSettingsData updatedSettings = settings with { suppressSetupWizardAutoShow = suppressAutoShow };
            SaveSettings(updatedSettings);
        }

        /// <summary>
        /// Gets the show security settings flag.
        /// </summary>
        public bool GetShowUnityCliLoopSecuritySetting()
        {
            return GetSettings().showUnityCliLoopSecuritySetting;
        }

        /// <summary>
        /// Sets the show security settings flag.
        /// </summary>
        public void SetShowUnityCliLoopSecuritySetting(bool showUnityCliLoopSecuritySetting)
        {
            UnityCliLoopEditorSettingsData settings = GetSettings();
            UnityCliLoopEditorSettingsData newSettings = settings with { showUnityCliLoopSecuritySetting = showUnityCliLoopSecuritySetting };
            SaveSettings(newSettings);
        }

        public bool GetShowToolSettings()
        {
            return GetSettings().showToolSettings;
        }

        public void SetShowToolSettings(bool showToolSettings)
        {
            UnityCliLoopEditorSettingsData settings = GetSettings();
            UnityCliLoopEditorSettingsData newSettings = settings with { showToolSettings = showToolSettings };
            SaveSettings(newSettings);
        }

        public bool GetInstallSkillsFlat()
        {
            return GetSettings().installSkillsFlat;
        }

        public void SetInstallSkillsFlat(bool installSkillsFlat)
        {
            UnityCliLoopEditorSettingsData settings = GetSettings();
            UnityCliLoopEditorSettingsData newSettings = settings with { installSkillsFlat = installSkillsFlat };
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Gets the server running state.
        /// </summary>
        public bool GetIsServerRunning()
        {
            return GetSettings().isServerRunning;
        }

        /// <summary>
        /// Sets the server running state.
        /// </summary>
        public void SetIsServerRunning(bool isServerRunning)
        {
            UnityCliLoopEditorSettingsData settings = GetSettings();
            UnityCliLoopEditorSettingsData newSettings = settings with { isServerRunning = isServerRunning };
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Gets the after compile flag.
        /// </summary>
        public bool GetIsAfterCompile()
        {
            return GetSettings().isAfterCompile;
        }

        /// <summary>
        /// Sets the after compile flag.
        /// </summary>
        public void SetIsAfterCompile(bool isAfterCompile)
        {
            UnityCliLoopEditorSettingsData settings = GetSettings();
            UnityCliLoopEditorSettingsData newSettings = settings with { isAfterCompile = isAfterCompile };
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Gets the domain reload in progress flag.
        /// </summary>
        public bool GetIsDomainReloadInProgress()
        {
            return GetSettings().isDomainReloadInProgress;
        }

        /// <summary>
        /// Sets the domain reload in progress flag.
        /// </summary>
        public void SetIsDomainReloadInProgress(bool isDomainReloadInProgress)
        {
            UnityCliLoopEditorSettingsData settings = GetSettings();
            UnityCliLoopEditorSettingsData newSettings = settings with { isDomainReloadInProgress = isDomainReloadInProgress };
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Gets the reconnecting flag.
        /// </summary>
        public bool GetIsReconnecting()
        {
            return GetSettings().isReconnecting;
        }

        /// <summary>
        /// Sets the reconnecting flag.
        /// </summary>
        public void SetIsReconnecting(bool isReconnecting)
        {
            UnityCliLoopEditorSettingsData settings = GetSettings();
            UnityCliLoopEditorSettingsData newSettings = settings with { isReconnecting = isReconnecting };
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Gets the show reconnecting UI flag.
        /// </summary>
        public bool GetShowReconnectingUI()
        {
            return GetSettings().showReconnectingUI;
        }

        /// <summary>
        /// Sets the show reconnecting UI flag.
        /// </summary>
        public void SetShowReconnectingUI(bool showReconnectingUI)
        {
            UnityCliLoopEditorSettingsData settings = GetSettings();
            UnityCliLoopEditorSettingsData newSettings = settings with { showReconnectingUI = showReconnectingUI };
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Gets the show post compile reconnecting UI flag.
        /// </summary>
        public bool GetShowPostCompileReconnectingUI()
        {
            return GetSettings().showPostCompileReconnectingUI;
        }

        /// <summary>
        /// Sets the show post compile reconnecting UI flag.
        /// </summary>
        public void SetShowPostCompileReconnectingUI(bool showPostCompileReconnectingUI)
        {
            UnityCliLoopEditorSettingsData settings = GetSettings();
            UnityCliLoopEditorSettingsData newSettings = settings with { showPostCompileReconnectingUI = showPostCompileReconnectingUI };
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Gets the compile window has data flag.
        /// </summary>
        public bool GetCompileWindowHasData()
        {
            return GetSettings().compileWindowHasData;
        }

        /// <summary>
        /// Sets the compile window has data flag.
        /// </summary>
        public void SetCompileWindowHasData(bool compileWindowHasData)
        {
            UnityCliLoopEditorSettingsData settings = GetSettings();
            UnityCliLoopEditorSettingsData newSettings = settings with { compileWindowHasData = compileWindowHasData };
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Clear server session.
        /// </summary>
        public void ClearServerSession()
        {
            UpdateSettings(settings => settings with
            {
                isServerRunning = false
            });
        }

        /// <summary>
        /// Clear after compile flag.
        /// </summary>
        public void ClearAfterCompileFlag()
        {
            SetIsAfterCompile(false);
        }

        /// <summary>
        /// Clear reconnecting flags.
        /// </summary>
        public void ClearReconnectingFlags()
        {
            UpdateSettings(s => s with
            {
                isReconnecting = false,
                showReconnectingUI = false
            });
        }

        /// <summary>
        /// Clear post compile reconnecting UI.
        /// </summary>
        public void ClearPostCompileReconnectingUI()
        {
            SetShowPostCompileReconnectingUI(false);
        }

        /// <summary>
        /// Clear domain reload flag.
        /// </summary>
        public void ClearDomainReloadFlag()
        {
            SetIsDomainReloadInProgress(false);
        }

        /// <summary>
        /// Clear compile window data.
        /// </summary>
        public void ClearCompileWindowData()
        {
            SetCompileWindowHasData(false);
        }

        // Compile request management methods

        /// <summary>
        /// Gets the pending compile request IDs.
        /// </summary>
        public string[] GetPendingCompileRequestIds()
        {
            return GetSettings().pendingCompileRequestIds;
        }

        /// <summary>
        /// Sets the pending compile request IDs.
        /// </summary>
        public void SetPendingCompileRequestIds(string[] pendingCompileRequestIds)
        {
            UnityCliLoopEditorSettingsData settings = GetSettings();
            UnityCliLoopEditorSettingsData newSettings = settings with { pendingCompileRequestIds = pendingCompileRequestIds };
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Gets the compile requests.
        /// </summary>
        public CompileRequestData[] GetCompileRequests()
        {
            return GetSettings().compileRequests;
        }

        /// <summary>
        /// Sets the compile requests.
        /// </summary>
        public void SetCompileRequests(CompileRequestData[] compileRequests)
        {
            UnityCliLoopEditorSettingsData settings = GetSettings();
            UnityCliLoopEditorSettingsData newSettings = settings with { compileRequests = compileRequests };
            SaveSettings(newSettings);
        }

        /// <summary>
        /// Gets the compile request JSON by request ID.
        /// </summary>
        public string GetCompileRequestJson(string requestId)
        {
            CompileRequestData[] requests = GetCompileRequests();
            CompileRequestData request = System.Array.Find(requests, r => r.requestId == requestId);
            return request?.json;
        }

        /// <summary>
        /// Sets the compile request JSON for a specific request ID.
        /// </summary>
        public void SetCompileRequestJson(string requestId, string json)
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
        public void ClearAllCompileRequests()
        {
            SetCompileRequests(new CompileRequestData[0]);
            SetPendingCompileRequestIds(new string[0]);
        }

        /// <summary>
        /// Adds a pending compile request.
        /// </summary>
        public void AddPendingCompileRequest(string requestId)
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
        public void RemovePendingCompileRequest(string requestId)
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

        /// <summary>
        /// Loads the settings file.
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                // Security: Validate settings file path
                if (!IsValidSettingsPath(SettingsFilePath))
                {
                    throw new SecurityException($"Invalid settings file path: {SettingsFilePath}");
                }

                RecoverSettingsFileIfNeeded();

                if (File.Exists(SettingsFilePath))
                {
                    // Security: Check file size before reading
                    FileInfo fileInfo = new FileInfo(SettingsFilePath);
                    if (fileInfo.Length > UnityCliLoopConstants.MAX_SETTINGS_SIZE_BYTES)
                    {
                        throw new SecurityException("Settings file exceeds size limit");
                    }

                    string json = File.ReadAllText(SettingsFilePath);

                    // Security: Validate JSON content
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        throw new InvalidDataException("Settings file contains invalid JSON content");
                    }

                    _cachedSettings = JsonUtility.FromJson<UnityCliLoopEditorSettingsData>(json);

                    // Migrate security fields before any potential SaveSettings call from this class.
                    // If SaveSettings runs first, legacy security fields are stripped from JSON
                    // because UnityCliLoopEditorSettingsData no longer defines them.
                    ULoopSettings.GetSettings();
                }
                else
                {
                    _cachedSettings = new UnityCliLoopEditorSettingsData();
                }
            }
            catch (Exception ex)
            {
                // Don't suppress this exception - corrupted settings should be reported
                throw new InvalidOperationException(
                    $"Failed to load Unity CLI Loop Editor settings from: {SettingsFilePath}. Settings file may be corrupted.", ex);
            }
        }

        private void RemoveLegacyTransientFieldsIfNeeded(string settingsPath)
        {
            if (!File.Exists(settingsPath))
            {
                return;
            }

            FileInfo fileInfo = new FileInfo(settingsPath);
            if (fileInfo.Length > UnityCliLoopConstants.MAX_SETTINGS_SIZE_BYTES)
            {
                throw new SecurityException("Settings file exceeds size limit");
            }

            JToken settingsToken;
            using (StreamReader reader = File.OpenText(settingsPath))
            {
                settingsToken = JToken.ReadFrom(new JsonTextReader(reader));
            }

            bool removed = RemoveLegacyTransientFields(settingsToken);
            if (!removed)
            {
                return;
            }

            AtomicFileWriter.Write(settingsPath, settingsToken.ToString(Formatting.Indented));
        }

        private bool RemoveLegacyTransientFields(JToken token)
        {
            Debug.Assert(token != null, "token must not be null");

            bool removed = false;
            if (token is JObject jsonObject)
            {
                foreach (string legacyKey in _legacyTransientSettingKeys)
                {
                    removed |= jsonObject.Remove(legacyKey);
                }

                foreach (JProperty property in jsonObject.Properties())
                {
                    removed |= RemoveLegacyTransientFields(property.Value);
                }

                return removed;
            }

            if (token is JArray jsonArray)
            {
                foreach (JToken item in jsonArray)
                {
                    removed |= RemoveLegacyTransientFields(item);
                }
            }

            return removed;
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
                string expectedUserSettingsPath = Path.GetFullPath(UnityCliLoopConstants.USER_SETTINGS_FOLDER);
                
                // Check if path is within the expected directory
                return normalizedPath.StartsWith(expectedUserSettingsPath, StringComparison.OrdinalIgnoreCase) &&
                       normalizedPath.EndsWith(UnityCliLoopConstants.SETTINGS_FILE_NAME, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"{UnityCliLoopConstants.SECURITY_LOG_PREFIX} Error validating settings path {path}: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Compatibility entrypoint for callers that have not received UnityCliLoopEditorSettingsRepository through DI yet.
    /// </summary>
    public static class UnityCliLoopEditorSettings
    {
        private static readonly UnityCliLoopEditorSettingsRepository RepositoryValue =
            new UnityCliLoopEditorSettingsRepository();

        public static UnityCliLoopEditorSettingsRepository Repository
        {
            get { return RepositoryValue; }
        }

        internal static void InvalidateCache()
        {
            Repository.InvalidateCache();
        }

        internal static void ScheduleSettingsFileRecoveryForEditorStartup()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess())
            {
                return;
            }

            EditorApplication.delayCall += RecoverSettingsFileIfNeeded;
        }

        internal static void RecoverSettingsFileIfNeeded()
        {
            Repository.RecoverSettingsFileIfNeeded();
        }

        public static UnityCliLoopEditorSettingsData GetSettings()
        {
            return Repository.GetSettings();
        }

        public static void SaveSettings(UnityCliLoopEditorSettingsData settings)
        {
            Repository.SaveSettings(settings);
        }

        public static void UpdateSettings(Func<UnityCliLoopEditorSettingsData, UnityCliLoopEditorSettingsData> transform)
        {
            Repository.UpdateSettings(transform);
        }

        public static bool GetShowDeveloperTools()
        {
            return Repository.GetShowDeveloperTools();
        }

        public static void SetShowDeveloperTools(bool show)
        {
            Repository.SetShowDeveloperTools(show);
        }

        public static string GetLastSeenSetupWizardVersion()
        {
            return Repository.GetLastSeenSetupWizardVersion();
        }

        public static void SetLastSeenSetupWizardVersion(string version)
        {
            Repository.SetLastSeenSetupWizardVersion(version);
        }

        public static bool GetSuppressSetupWizardAutoShow()
        {
            return Repository.GetSuppressSetupWizardAutoShow();
        }

        public static void SetSuppressSetupWizardAutoShow(bool suppressAutoShow)
        {
            Repository.SetSuppressSetupWizardAutoShow(suppressAutoShow);
        }

        public static bool GetShowUnityCliLoopSecuritySetting()
        {
            return Repository.GetShowUnityCliLoopSecuritySetting();
        }

        public static void SetShowUnityCliLoopSecuritySetting(bool showUnityCliLoopSecuritySetting)
        {
            Repository.SetShowUnityCliLoopSecuritySetting(showUnityCliLoopSecuritySetting);
        }

        public static bool GetShowToolSettings()
        {
            return Repository.GetShowToolSettings();
        }

        public static void SetShowToolSettings(bool showToolSettings)
        {
            Repository.SetShowToolSettings(showToolSettings);
        }

        public static bool GetInstallSkillsFlat()
        {
            return Repository.GetInstallSkillsFlat();
        }

        public static void SetInstallSkillsFlat(bool installSkillsFlat)
        {
            Repository.SetInstallSkillsFlat(installSkillsFlat);
        }

        public static bool GetIsServerRunning()
        {
            return Repository.GetIsServerRunning();
        }

        public static void SetIsServerRunning(bool isServerRunning)
        {
            Repository.SetIsServerRunning(isServerRunning);
        }

        public static bool GetIsAfterCompile()
        {
            return Repository.GetIsAfterCompile();
        }

        public static void SetIsAfterCompile(bool isAfterCompile)
        {
            Repository.SetIsAfterCompile(isAfterCompile);
        }

        public static bool GetIsDomainReloadInProgress()
        {
            return Repository.GetIsDomainReloadInProgress();
        }

        public static void SetIsDomainReloadInProgress(bool isDomainReloadInProgress)
        {
            Repository.SetIsDomainReloadInProgress(isDomainReloadInProgress);
        }

        public static bool GetIsReconnecting()
        {
            return Repository.GetIsReconnecting();
        }

        public static void SetIsReconnecting(bool isReconnecting)
        {
            Repository.SetIsReconnecting(isReconnecting);
        }

        public static bool GetShowReconnectingUI()
        {
            return Repository.GetShowReconnectingUI();
        }

        public static void SetShowReconnectingUI(bool showReconnectingUI)
        {
            Repository.SetShowReconnectingUI(showReconnectingUI);
        }

        public static bool GetShowPostCompileReconnectingUI()
        {
            return Repository.GetShowPostCompileReconnectingUI();
        }

        public static void SetShowPostCompileReconnectingUI(bool showPostCompileReconnectingUI)
        {
            Repository.SetShowPostCompileReconnectingUI(showPostCompileReconnectingUI);
        }

        public static bool GetCompileWindowHasData()
        {
            return Repository.GetCompileWindowHasData();
        }

        public static void SetCompileWindowHasData(bool compileWindowHasData)
        {
            Repository.SetCompileWindowHasData(compileWindowHasData);
        }

        public static void ClearServerSession()
        {
            Repository.ClearServerSession();
        }

        public static void ClearAfterCompileFlag()
        {
            Repository.ClearAfterCompileFlag();
        }

        public static void ClearReconnectingFlags()
        {
            Repository.ClearReconnectingFlags();
        }

        public static void ClearPostCompileReconnectingUI()
        {
            Repository.ClearPostCompileReconnectingUI();
        }

        public static void ClearDomainReloadFlag()
        {
            Repository.ClearDomainReloadFlag();
        }

        public static void ClearCompileWindowData()
        {
            Repository.ClearCompileWindowData();
        }

        public static string[] GetPendingCompileRequestIds()
        {
            return Repository.GetPendingCompileRequestIds();
        }

        public static void SetPendingCompileRequestIds(string[] pendingCompileRequestIds)
        {
            Repository.SetPendingCompileRequestIds(pendingCompileRequestIds);
        }

        public static CompileRequestData[] GetCompileRequests()
        {
            return Repository.GetCompileRequests();
        }

        public static void SetCompileRequests(CompileRequestData[] compileRequests)
        {
            Repository.SetCompileRequests(compileRequests);
        }

        public static string GetCompileRequestJson(string requestId)
        {
            return Repository.GetCompileRequestJson(requestId);
        }

        public static void SetCompileRequestJson(string requestId, string json)
        {
            Repository.SetCompileRequestJson(requestId, json);
        }

        public static void ClearAllCompileRequests()
        {
            Repository.ClearAllCompileRequests();
        }

        public static void AddPendingCompileRequest(string requestId)
        {
            Repository.AddPendingCompileRequest(requestId);
        }

        public static void RemovePendingCompileRequest(string requestId)
        {
            Repository.RemovePendingCompileRequest(requestId);
        }
    }
}
