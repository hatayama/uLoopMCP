using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Security settings management for .uloop/settings.permissions.json.
    /// This file is stored in the project root so it can be git-tracked
    /// and shared across team members as a security policy.
    /// </summary>
    public static class ULoopSettings
    {
        private static string SettingsFilePath =>
            Path.Combine(McpConstants.ULOOP_DIR, McpConstants.ULOOP_SETTINGS_FILE_NAME);

        private static string LegacySettingsFilePath =>
            Path.Combine(McpConstants.USER_SETTINGS_FOLDER, McpConstants.SETTINGS_FILE_NAME);

        private static ULoopSettingsData _cachedSettings;

        public static ULoopSettingsData GetSettings()
        {
            if (_cachedSettings == null)
            {
                LoadSettings();
            }
            return _cachedSettings;
        }

        public static void SaveSettings(ULoopSettingsData settings)
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

        public static void UpdateSettings(Func<ULoopSettingsData, ULoopSettingsData> transform)
        {
            Debug.Assert(transform != null, "transform must not be null");

            ULoopSettingsData current = GetSettings();
            ULoopSettingsData updated = transform(current);
            SaveSettings(updated);
        }

        // Security Settings Getters/Setters

        public static bool GetEnableTestsExecution()
        {
            return GetSettings().enableTestsExecution;
        }

        public static void SetEnableTestsExecution(bool value)
        {
            ULoopSettingsData settings = GetSettings();
            ULoopSettingsData updated = settings with { enableTestsExecution = value };
            SaveSettings(updated);
        }

        public static bool GetAllowMenuItemExecution()
        {
            return GetSettings().allowMenuItemExecution;
        }

        public static void SetAllowMenuItemExecution(bool value)
        {
            ULoopSettingsData settings = GetSettings();
            ULoopSettingsData updated = settings with { allowMenuItemExecution = value };
            SaveSettings(updated);
        }

        public static bool GetAllowThirdPartyTools()
        {
            return GetSettings().allowThirdPartyTools;
        }

        public static void SetAllowThirdPartyTools(bool value)
        {
            ULoopSettingsData settings = GetSettings();
            ULoopSettingsData updated = settings with { allowThirdPartyTools = value };
            SaveSettings(updated);
        }

        public static DynamicCodeSecurityLevel GetDynamicCodeSecurityLevel()
        {
            return (DynamicCodeSecurityLevel)GetSettings().dynamicCodeSecurityLevel;
        }

        public static void SetDynamicCodeSecurityLevel(DynamicCodeSecurityLevel level)
        {
            ULoopSettingsData settings = GetSettings();
            ULoopSettingsData updated = settings with { dynamicCodeSecurityLevel = (int)level };
            SaveSettings(updated);

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

        // Loading & Migration

        private static void LoadSettings()
        {
            // v0.68.0 used "settings.security.json"; rename once so existing users keep their settings.
            // This migration block can be removed after a few releases.
            string oldSettingsPath = Path.Combine(McpConstants.ULOOP_DIR, "settings.security.json");
            string oldBackupPath = oldSettingsPath + ".bak";
            if (!File.Exists(SettingsFilePath))
            {
                if (File.Exists(oldSettingsPath))
                {
                    File.Move(oldSettingsPath, SettingsFilePath);
                }
                else if (File.Exists(oldBackupPath))
                {
                    File.Move(oldBackupPath, SettingsFilePath);
                }
            }

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
                    _cachedSettings = new ULoopSettingsData();
                    return;
                }

                _cachedSettings = JsonUtility.FromJson<ULoopSettingsData>(json);
                return;
            }

            // .uloop/settings.permissions.json does not exist yet — attempt migration from legacy file
            MigrateFromLegacySettings();
        }

        /// <summary>
        /// McpEditorSettingsData no longer contains security fields, so we need
        /// a dedicated probe class to extract them from the legacy JSON.
        /// </summary>
        [Serializable]
        private class LegacySecuritySettingsProbe
        {
            public bool enableTestsExecution = false;
            public bool allowMenuItemExecution = false;
            public bool allowThirdPartyTools = false;
            public int dynamicCodeSecurityLevel = (int)DynamicCodeSecurityLevel.Disabled;
        }

        /// <summary>
        /// One-time migration: .uloop/settings.permissions.json absence is used as the trigger
        /// to guarantee this runs exactly once — after migration the file exists and
        /// this path is never taken again.
        /// </summary>
        private static void MigrateFromLegacySettings()
        {
            if (!File.Exists(LegacySettingsFilePath))
            {
                _cachedSettings = new ULoopSettingsData();
                return;
            }

            string legacyJson = File.ReadAllText(LegacySettingsFilePath);
            if (string.IsNullOrWhiteSpace(legacyJson))
            {
                _cachedSettings = new ULoopSettingsData();
                return;
            }

            LegacySecuritySettingsProbe probe = JsonUtility.FromJson<LegacySecuritySettingsProbe>(legacyJson);

            _cachedSettings = new ULoopSettingsData
            {
                enableTestsExecution = probe.enableTestsExecution,
                allowMenuItemExecution = probe.allowMenuItemExecution,
                allowThirdPartyTools = probe.allowThirdPartyTools,
                dynamicCodeSecurityLevel = probe.dynamicCodeSecurityLevel
            };

            SaveSettings(_cachedSettings);

            // Re-save legacy file to purge security fields that are no longer in
            // McpEditorSettingsData — JsonUtility.ToJson only serializes defined fields,
            // so the 4 removed fields disappear from the JSON on re-serialization.
            McpEditorSettings.SaveSettings(McpEditorSettings.GetSettings());
        }

        internal static void InvalidateCache()
        {
            _cachedSettings = null;
        }

        // Roslyn Define Symbol Management

        /// <summary>
        /// Symbol check and automatic addition after domain reload
        /// </summary>
        [InitializeOnLoadMethod]
        private static void CheckRoslynSymbolOnDomainReload()
        {
            EditorApplication.delayCall += () =>
            {
                DynamicCodeSecurityLevel currentLevel = GetDynamicCodeSecurityLevel();
                UpdateRoslynDefineSymbol(currentLevel);
            };
        }

        private static void UpdateRoslynDefineSymbol(DynamicCodeSecurityLevel level)
        {
            string correlationId = McpConstants.GenerateCorrelationId();

            NamedBuildTarget[] targets = GetAllKnownTargets();

            foreach (NamedBuildTarget target in targets)
            {
                string currentSymbols = PlayerSettings.GetScriptingDefineSymbols(target);
                List<string> symbols = currentSymbols
                    .Split(';')
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();

                bool hasRoslynSymbol = symbols.Contains(McpConstants.SCRIPTING_DEFINE_ULOOPMCP_HAS_ROSLYN);
                bool shouldAddSymbol = level != DynamicCodeSecurityLevel.Disabled;

                // Add symbol only for levels other than Disabled, when symbol does not yet exist
                // Do not remove symbol when changing to Disabled (as per specification)
                if (shouldAddSymbol && !hasRoslynSymbol)
                {
                    symbols.Add(McpConstants.SCRIPTING_DEFINE_ULOOPMCP_HAS_ROSLYN);
                    string newSymbols = string.Join(";", symbols);
                    PlayerSettings.SetScriptingDefineSymbols(target, newSymbols);

                    VibeLogger.LogInfo(
                        "roslyn_symbol_added_to_platform",
                        $"Added {McpConstants.SCRIPTING_DEFINE_ULOOPMCP_HAS_ROSLYN} to {target}",
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
            }
        }

        private static NamedBuildTarget[] GetAllKnownTargets()
        {
            List<NamedBuildTarget> targets = new();

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
                if (!targets.Contains(NamedBuildTarget.Standalone))
                {
                    targets.Add(NamedBuildTarget.Standalone);
                }
            }

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
    }
}
