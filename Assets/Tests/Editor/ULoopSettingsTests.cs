using NUnit.Framework;
using System.IO;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    [TestFixture]
    public class ULoopSettingsTests
    {
        private static readonly string SettingsFilePath =
            Path.Combine(McpConstants.ULOOP_DIR, McpConstants.ULOOP_SETTINGS_FILE_NAME);
        private static readonly string LegacySettingsFilePath =
            Path.Combine(McpConstants.USER_SETTINGS_FOLDER, McpConstants.SETTINGS_FILE_NAME);
        private static readonly string SettingsBackupPath = SettingsFilePath + ".bak";
        private static readonly string SettingsTmpPath = SettingsFilePath + ".tmp";
        private static readonly string LegacyBackupPath = LegacySettingsFilePath + ".bak";
        private static readonly string LegacyTmpPath = LegacySettingsFilePath + ".tmp";

        // Sidecar file paths to backup/restore
        private static readonly string[] AllSidecarPaths = new[]
        {
            SettingsBackupPath, SettingsTmpPath, LegacyBackupPath, LegacyTmpPath
        };

        private bool _settingsFileExisted;
        private string _settingsFileContent;
        private bool _legacyFileExisted;
        private string _legacyFileContent;
        private bool[] _sidecarExisted;
        private string[] _sidecarContents;

        [SetUp]
        public void SetUp()
        {
            _settingsFileExisted = File.Exists(SettingsFilePath);
            _settingsFileContent = _settingsFileExisted ? File.ReadAllText(SettingsFilePath) : null;

            _legacyFileExisted = File.Exists(LegacySettingsFilePath);
            _legacyFileContent = _legacyFileExisted ? File.ReadAllText(LegacySettingsFilePath) : null;

            _sidecarExisted = new bool[AllSidecarPaths.Length];
            _sidecarContents = new string[AllSidecarPaths.Length];
            for (int i = 0; i < AllSidecarPaths.Length; i++)
            {
                _sidecarExisted[i] = File.Exists(AllSidecarPaths[i]);
                _sidecarContents[i] = _sidecarExisted[i] ? File.ReadAllText(AllSidecarPaths[i]) : null;
            }

            string uloopDir = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(uloopDir) && !Directory.Exists(uloopDir))
            {
                Directory.CreateDirectory(uloopDir);
            }
            if (!Directory.Exists(McpConstants.USER_SETTINGS_FOLDER))
            {
                Directory.CreateDirectory(McpConstants.USER_SETTINGS_FOLDER);
            }

            ULoopSettings.InvalidateCache();
            McpEditorSettings.InvalidateCache();
        }

        [TearDown]
        public void TearDown()
        {
            RestoreFile(SettingsFilePath, _settingsFileExisted, _settingsFileContent);
            RestoreFile(LegacySettingsFilePath, _legacyFileExisted, _legacyFileContent);

            for (int i = 0; i < AllSidecarPaths.Length; i++)
            {
                RestoreFile(AllSidecarPaths[i], _sidecarExisted[i], _sidecarContents[i]);
            }

            ULoopSettings.InvalidateCache();
            McpEditorSettings.InvalidateCache();
        }

        private static void RestoreFile(string path, bool existed, string content)
        {
            if (existed)
            {
                File.WriteAllText(path, content);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private void InvalidateBothCaches()
        {
            ULoopSettings.InvalidateCache();
            McpEditorSettings.InvalidateCache();
        }

        // ── Test 1: Migration ────────────────────────────────────────────

        [Test]
        public void GetSettings_WhenNewFileAbsentAndLegacyExists_ShouldMigrateOnce()
        {
            DeleteIfExists(SettingsFilePath);

            string legacyJson = JsonUtility.ToJson(new LegacySettingsFixture
            {
                enableTestsExecution = true,
                allowMenuItemExecution = true,
                allowThirdPartyTools = true,
                dynamicCodeSecurityLevel = (int)DynamicCodeSecurityLevel.Restricted,
                customPort = 12345
            }, true);
            File.WriteAllText(LegacySettingsFilePath, legacyJson);
            InvalidateBothCaches();

            ULoopSettingsData result = ULoopSettings.GetSettings();

            Assert.IsTrue(result.enableTestsExecution);
            Assert.IsTrue(result.allowMenuItemExecution);
            Assert.IsTrue(result.allowThirdPartyTools);
            Assert.AreEqual((int)DynamicCodeSecurityLevel.Restricted, result.dynamicCodeSecurityLevel);
            Assert.IsTrue(File.Exists(SettingsFilePath), ".uloop/settings.json should be created by migration");
        }

        // ── Test 2: Idempotency ──────────────────────────────────────────

        [Test]
        public void GetSettings_AfterMigration_ShouldNotReMigrate()
        {
            DeleteIfExists(SettingsFilePath);

            string legacyJson = JsonUtility.ToJson(new LegacySettingsFixture
            {
                enableTestsExecution = true,
                allowMenuItemExecution = false,
                allowThirdPartyTools = true,
                dynamicCodeSecurityLevel = (int)DynamicCodeSecurityLevel.Restricted,
                customPort = 12345
            }, true);
            File.WriteAllText(LegacySettingsFilePath, legacyJson);
            InvalidateBothCaches();

            // First call triggers migration
            ULoopSettings.GetSettings();

            // Modify legacy file with different values
            string alteredLegacy = JsonUtility.ToJson(new LegacySettingsFixture
            {
                enableTestsExecution = false,
                allowMenuItemExecution = true,
                allowThirdPartyTools = false,
                dynamicCodeSecurityLevel = (int)DynamicCodeSecurityLevel.FullAccess,
                customPort = 99999
            }, true);
            File.WriteAllText(LegacySettingsFilePath, alteredLegacy);
            InvalidateBothCaches();

            // Second call should read from .uloop/settings.json, not re-migrate
            ULoopSettingsData result = ULoopSettings.GetSettings();

            Assert.IsTrue(result.enableTestsExecution, "Should retain original migrated value, not the altered legacy");
            Assert.IsFalse(result.allowMenuItemExecution);
            Assert.IsTrue(result.allowThirdPartyTools);
            Assert.AreEqual((int)DynamicCodeSecurityLevel.Restricted, result.dynamicCodeSecurityLevel);
        }

        // ── Test 3: New settings priority ────────────────────────────────

        [Test]
        public void GetSettings_WhenNewFileExists_ShouldIgnoreLegacy()
        {
            ULoopSettingsData newSettings = new ULoopSettingsData
            {
                enableTestsExecution = true,
                allowMenuItemExecution = false,
                allowThirdPartyTools = true,
                dynamicCodeSecurityLevel = (int)DynamicCodeSecurityLevel.FullAccess
            };
            string newJson = JsonUtility.ToJson(newSettings, true);
            File.WriteAllText(SettingsFilePath, newJson);

            string legacyJson = JsonUtility.ToJson(new LegacySettingsFixture
            {
                enableTestsExecution = false,
                allowMenuItemExecution = true,
                allowThirdPartyTools = false,
                dynamicCodeSecurityLevel = (int)DynamicCodeSecurityLevel.Disabled,
                customPort = 12345
            }, true);
            File.WriteAllText(LegacySettingsFilePath, legacyJson);
            InvalidateBothCaches();

            ULoopSettingsData result = ULoopSettings.GetSettings();

            Assert.IsTrue(result.enableTestsExecution, "Should read from .uloop/settings.json");
            Assert.IsFalse(result.allowMenuItemExecution);
            Assert.IsTrue(result.allowThirdPartyTools);
            Assert.AreEqual((int)DynamicCodeSecurityLevel.FullAccess, result.dynamicCodeSecurityLevel);
        }

        // ── Test 4: Round-trip ───────────────────────────────────────────

        [Test]
        public void SetAndGet_RoundTrip_ShouldPreserveValues()
        {
            ULoopSettingsData written = new ULoopSettingsData
            {
                enableTestsExecution = true,
                allowMenuItemExecution = true,
                allowThirdPartyTools = true,
                dynamicCodeSecurityLevel = (int)DynamicCodeSecurityLevel.Restricted
            };
            ULoopSettings.SaveSettings(written);
            ULoopSettings.InvalidateCache();

            ULoopSettingsData readBack = ULoopSettings.GetSettings();

            Assert.AreEqual(written.enableTestsExecution, readBack.enableTestsExecution);
            Assert.AreEqual(written.allowMenuItemExecution, readBack.allowMenuItemExecution);
            Assert.AreEqual(written.allowThirdPartyTools, readBack.allowThirdPartyTools);
            Assert.AreEqual(written.dynamicCodeSecurityLevel, readBack.dynamicCodeSecurityLevel);
        }

        // ── Test 5: Legacy cleanup ───────────────────────────────────────

        [Test]
        public void Migration_ShouldPurgeSecurityFieldsAndPreserveOtherSettings()
        {
            DeleteIfExists(SettingsFilePath);

            int expectedPort = 18080;
            string legacyJson = JsonUtility.ToJson(new LegacySettingsFixture
            {
                enableTestsExecution = true,
                allowMenuItemExecution = true,
                allowThirdPartyTools = true,
                dynamicCodeSecurityLevel = (int)DynamicCodeSecurityLevel.Restricted,
                customPort = expectedPort,
                showDeveloperTools = true
            }, true);
            File.WriteAllText(LegacySettingsFilePath, legacyJson);
            InvalidateBothCaches();

            ULoopSettings.GetSettings();

            string updatedLegacy = File.ReadAllText(LegacySettingsFilePath);

            StringAssert.DoesNotContain("\"enableTestsExecution\"", updatedLegacy);
            StringAssert.DoesNotContain("\"allowMenuItemExecution\"", updatedLegacy);
            StringAssert.DoesNotContain("\"allowThirdPartyTools\"", updatedLegacy);
            StringAssert.DoesNotContain("\"dynamicCodeSecurityLevel\"", updatedLegacy);

            // Non-security settings should be preserved
            McpEditorSettingsData legacySettings = JsonUtility.FromJson<McpEditorSettingsData>(updatedLegacy);
            Assert.AreEqual(expectedPort, legacySettings.customPort, "customPort should be preserved");
            Assert.IsTrue(legacySettings.showDeveloperTools, "showDeveloperTools should be preserved");
        }

        // ── Test 6: Both files absent (fresh install) ────────────────────

        [Test]
        public void GetSettings_WhenBothFilesAbsent_ShouldReturnDefaults()
        {
            DeleteIfExists(SettingsFilePath);
            DeleteIfExists(LegacySettingsFilePath);
            InvalidateBothCaches();

            ULoopSettingsData result = ULoopSettings.GetSettings();

            Assert.IsFalse(result.enableTestsExecution);
            Assert.IsFalse(result.allowMenuItemExecution);
            Assert.IsFalse(result.allowThirdPartyTools);
            Assert.AreEqual((int)DynamicCodeSecurityLevel.Disabled, result.dynamicCodeSecurityLevel);
            Assert.IsFalse(File.Exists(LegacySettingsFilePath),
                "Legacy file should not be created when both files are absent");
        }

        // ── Test 7: .bak recovery ────────────────────────────────────────

        [Test]
        public void GetSettings_WhenPrimaryMissingAndBackupExists_ShouldRecoverFromBackup()
        {
            DeleteIfExists(SettingsFilePath);

            ULoopSettingsData backupData = new ULoopSettingsData
            {
                enableTestsExecution = true,
                allowMenuItemExecution = true,
                allowThirdPartyTools = false,
                dynamicCodeSecurityLevel = (int)DynamicCodeSecurityLevel.FullAccess
            };
            string backupJson = JsonUtility.ToJson(backupData, true);
            File.WriteAllText(SettingsBackupPath, backupJson);
            InvalidateBothCaches();

            ULoopSettingsData result = ULoopSettings.GetSettings();

            Assert.IsTrue(File.Exists(SettingsFilePath), ".uloop/settings.json should be recovered from .bak");
            Assert.IsTrue(result.enableTestsExecution);
            Assert.IsTrue(result.allowMenuItemExecution);
            Assert.IsFalse(result.allowThirdPartyTools);
            Assert.AreEqual((int)DynamicCodeSecurityLevel.FullAccess, result.dynamicCodeSecurityLevel);
            Assert.IsFalse(File.Exists(SettingsBackupPath), ".bak should be consumed by recovery");
        }

        /// <summary>
        /// Fixture that includes both security and non-security fields,
        /// matching the legacy UserSettings/UnityMcpSettings.json structure.
        /// </summary>
        [System.Serializable]
        private class LegacySettingsFixture
        {
            public bool enableTestsExecution = false;
            public bool allowMenuItemExecution = false;
            public bool allowThirdPartyTools = false;
            public int dynamicCodeSecurityLevel = (int)DynamicCodeSecurityLevel.Disabled;
            public int customPort = McpServerConfig.DEFAULT_PORT;
            public bool showDeveloperTools = false;
        }
    }
}
