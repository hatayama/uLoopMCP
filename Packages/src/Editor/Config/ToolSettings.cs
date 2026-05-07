using System;
using System.IO;
using System.Linq;

using UnityEngine;

namespace io.github.hatayama.UnityCliLoop
{
    /// <summary>
    /// Tool toggle settings management for .uloop/settings.tools.json.
    /// Controls which tools are enabled or disabled in the CLI tool list.
    /// </summary>
    public sealed class ToolSettingsRepository
    {
        private string SettingsFilePath =>
            Path.Combine(UnityCliLoopConstants.ULOOP_DIR, UnityCliLoopConstants.ULOOP_TOOL_SETTINGS_FILE_NAME);

        private ToolSettingsData _cachedSettings;

        public ToolSettingsData GetSettings()
        {
            if (_cachedSettings == null)
            {
                LoadSettings();
            }
            return _cachedSettings;
        }

        public void SaveSettings(ToolSettingsData settings)
        {
            Debug.Assert(settings != null, "settings must not be null");

            string directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonUtility.ToJson(settings, true);

            Debug.Assert(json.Length <= UnityCliLoopConstants.MAX_SETTINGS_SIZE_BYTES,
                "Settings JSON content exceeds size limit");

            AtomicFileWriter.Write(SettingsFilePath, json);
            _cachedSettings = settings;

            AtomicFileWriter.CleanupBackup(SettingsFilePath + ".bak");
        }

        public bool IsToolEnabled(string toolName)
        {
            Debug.Assert(!string.IsNullOrEmpty(toolName), "toolName must not be null or empty");

            string[] disabledTools = GetSettings().disabledTools;
            return !disabledTools.Contains(toolName);
        }

        public void SetToolEnabled(string toolName, bool enabled)
        {
            Debug.Assert(!string.IsNullOrEmpty(toolName), "toolName must not be null or empty");

            ToolSettingsData settings = GetSettings();
            string[] currentDisabled = settings.disabledTools;

            string[] newDisabled;
            if (enabled)
            {
                newDisabled = currentDisabled.Where(t => t != toolName).ToArray();
            }
            else
            {
                if (currentDisabled.Contains(toolName))
                {
                    return;
                }
                newDisabled = currentDisabled.Append(toolName).ToArray();
            }

            ToolSettingsData updated = settings with { disabledTools = newDisabled };
            SaveSettings(updated);
        }

        public string[] GetDisabledTools()
        {
            return GetSettings().disabledTools;
        }

        public void InvalidateCache()
        {
            _cachedSettings = null;
        }

        private void LoadSettings()
        {
            AtomicFileWriter.RecoverSidecarFiles(SettingsFilePath);

            if (File.Exists(SettingsFilePath))
            {
                FileInfo fileInfo = new FileInfo(SettingsFilePath);
                Debug.Assert(fileInfo.Length <= UnityCliLoopConstants.MAX_SETTINGS_SIZE_BYTES,
                    $"Settings file exceeds size limit: {fileInfo.Length} bytes");

                string json = File.ReadAllText(SettingsFilePath);

                if (string.IsNullOrWhiteSpace(json))
                {
                    _cachedSettings = new ToolSettingsData();
                    return;
                }

                ToolSettingsData loaded = JsonUtility.FromJson<ToolSettingsData>(json);
                // disabledTools can be null when JSON is hand-edited with "disabledTools": null
                if (loaded == null || loaded.disabledTools == null)
                {
                    _cachedSettings = new ToolSettingsData();
                    return;
                }
                _cachedSettings = loaded;
                return;
            }

            _cachedSettings = new ToolSettingsData();
        }
    }

    /// <summary>
    /// Compatibility entrypoint for callers that have not received ToolSettingsRepository through DI yet.
    /// </summary>
    public static class ToolSettings
    {
        private static readonly ToolSettingsRepository RepositoryValue = new ToolSettingsRepository();

        public static ToolSettingsRepository Repository
        {
            get { return RepositoryValue; }
        }

        public static ToolSettingsData GetSettings()
        {
            return Repository.GetSettings();
        }

        public static void SaveSettings(ToolSettingsData settings)
        {
            Repository.SaveSettings(settings);
        }

        public static bool IsToolEnabled(string toolName)
        {
            return Repository.IsToolEnabled(toolName);
        }

        public static void SetToolEnabled(string toolName, bool enabled)
        {
            Repository.SetToolEnabled(toolName, enabled);
        }

        public static string[] GetDisabledTools()
        {
            return Repository.GetDisabledTools();
        }

        public static void InvalidateCache()
        {
            Repository.InvalidateCache();
        }
    }

    /// <summary>
    /// Application facade for tool catalog and security settings workflows.
    /// Presentation code uses this facade instead of depending on registry and settings internals directly.
    /// </summary>
    public static class ToolSettingsApplicationFacade
    {
        public readonly struct ToolCatalogItem
        {
            public readonly string Name;
            public readonly bool DisplayDevelopmentOnly;
            public readonly bool IsThirdParty;

            public ToolCatalogItem(
                string name,
                bool displayDevelopmentOnly,
                bool isThirdParty)
            {
                Name = name;
                DisplayDevelopmentOnly = displayDevelopmentOnly;
                IsThirdParty = isThirdParty;
            }
        }

        public static event Action OnToolsChanged
        {
            add => UnityCliLoopToolRegistrar.OnToolsChanged += value;
            remove => UnityCliLoopToolRegistrar.OnToolsChanged -= value;
        }

        public static DynamicCodeSecurityLevel GetDynamicCodeSecurityLevel()
        {
            return ULoopSettings.GetDynamicCodeSecurityLevel();
        }

        public static void SetDynamicCodeSecurityLevel(DynamicCodeSecurityLevel level)
        {
            ULoopSettings.SetDynamicCodeSecurityLevel(level);
        }

        public static bool IsToolEnabled(string toolName)
        {
            return ToolSettings.IsToolEnabled(toolName);
        }

        public static void SetToolEnabled(string toolName, bool enabled)
        {
            ToolSettings.SetToolEnabled(toolName, enabled);
        }

        public static void WarmupRegistry()
        {
            UnityCliLoopToolRegistrar.WarmupRegistry();
        }

        public static bool TryGetToolCatalog(out ToolCatalogItem[] catalog)
        {
            UnityCliLoopToolRegistry registry = UnityCliLoopToolRegistrar.TryGetRegistry();
            if (registry == null)
            {
                catalog = Array.Empty<ToolCatalogItem>();
                return false;
            }

            catalog = registry.GetToolSettingsCatalog()
                .Select(ToFacadeItem)
                .ToArray();
            return true;
        }

        private static ToolCatalogItem ToFacadeItem(ToolSettingsCatalogItem item)
        {
            return new ToolCatalogItem(
                item.Name,
                item.DisplayDevelopmentOnly,
                item.IsThirdParty);
        }
    }
}
