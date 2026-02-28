using System;
using System.IO;
using System.Linq;

using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Tool toggle settings management for .uloop/settings.tools.json.
    /// Controls which tools are enabled/disabled in the MCP tool list.
    /// </summary>
    public static class ToolSettings
    {
        private static string SettingsFilePath =>
            Path.Combine(McpConstants.ULOOP_DIR, McpConstants.ULOOP_TOOL_SETTINGS_FILE_NAME);

        private static ToolSettingsData _cachedSettings;

        public static ToolSettingsData GetSettings()
        {
            if (_cachedSettings == null)
            {
                LoadSettings();
            }
            return _cachedSettings;
        }

        public static void SaveSettings(ToolSettingsData settings)
        {
            Debug.Assert(settings != null, "settings must not be null");

            string directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonUtility.ToJson(settings, true);

            Debug.Assert(json.Length <= McpConstants.MAX_SETTINGS_SIZE_BYTES,
                "Settings JSON content exceeds size limit");

            AtomicFileWriter.Write(SettingsFilePath, json);
            _cachedSettings = settings;

            AtomicFileWriter.CleanupBackup(SettingsFilePath + ".bak");
        }

        public static bool IsToolEnabled(string toolName)
        {
            Debug.Assert(!string.IsNullOrEmpty(toolName), "toolName must not be null or empty");

            string[] disabledTools = GetSettings().disabledTools;
            return !disabledTools.Contains(toolName);
        }

        public static void SetToolEnabled(string toolName, bool enabled)
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

        public static string[] GetDisabledTools()
        {
            return GetSettings().disabledTools;
        }

        public static void InvalidateCache()
        {
            _cachedSettings = null;
        }

        private static void LoadSettings()
        {
            // Recover from interrupted atomic write
            string backupPath = SettingsFilePath + ".bak";
            if (!File.Exists(SettingsFilePath) && File.Exists(backupPath))
            {
                File.Move(backupPath, SettingsFilePath);
            }

            if (File.Exists(SettingsFilePath))
            {
                FileInfo fileInfo = new FileInfo(SettingsFilePath);
                Debug.Assert(fileInfo.Length <= McpConstants.MAX_SETTINGS_SIZE_BYTES,
                    $"Settings file exceeds size limit: {fileInfo.Length} bytes");

                string json = File.ReadAllText(SettingsFilePath);

                if (string.IsNullOrWhiteSpace(json))
                {
                    _cachedSettings = new ToolSettingsData();
                    return;
                }

                _cachedSettings = JsonUtility.FromJson<ToolSettingsData>(json);
                return;
            }

            _cachedSettings = new ToolSettingsData();
        }
    }
}
