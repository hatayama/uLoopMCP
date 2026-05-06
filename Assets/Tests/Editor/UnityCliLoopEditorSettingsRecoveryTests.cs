using System.IO;
using System.Security;

using NUnit.Framework;
using UnityEngine;

namespace io.github.hatayama.UnityCliLoop
{
    [TestFixture]
    public class UnityCliLoopEditorSettingsRecoveryTests
    {
        private static readonly string SettingsFilePath =
            Path.Combine(UnityCliLoopConstants.USER_SETTINGS_FOLDER, UnityCliLoopConstants.SETTINGS_FILE_NAME);
        private static readonly string BackupFilePath = SettingsFilePath + ".bak";
        private static readonly string TempFilePath = SettingsFilePath + ".tmp";

        private bool _settingsFileExisted;
        private string _settingsFileContent;
        private bool _backupFileExisted;
        private string _backupFileContent;
        private bool _tempFileExisted;
        private string _tempFileContent;

        [SetUp]
        public void SetUp()
        {
            _settingsFileExisted = File.Exists(SettingsFilePath);
            _settingsFileContent = _settingsFileExisted ? File.ReadAllText(SettingsFilePath) : null;

            _backupFileExisted = File.Exists(BackupFilePath);
            _backupFileContent = _backupFileExisted ? File.ReadAllText(BackupFilePath) : null;

            _tempFileExisted = File.Exists(TempFilePath);
            _tempFileContent = _tempFileExisted ? File.ReadAllText(TempFilePath) : null;

            if (!Directory.Exists(UnityCliLoopConstants.USER_SETTINGS_FOLDER))
            {
                Directory.CreateDirectory(UnityCliLoopConstants.USER_SETTINGS_FOLDER);
            }

            DeleteIfExists(SettingsFilePath);
            DeleteIfExists(BackupFilePath);
            DeleteIfExists(TempFilePath);
            UnityCliLoopEditorSettings.InvalidateCache();
        }

        [TearDown]
        public void TearDown()
        {
            RestoreFile(SettingsFilePath, _settingsFileExisted, _settingsFileContent);
            RestoreFile(BackupFilePath, _backupFileExisted, _backupFileContent);
            RestoreFile(TempFilePath, _tempFileExisted, _tempFileContent);
            UnityCliLoopEditorSettings.InvalidateCache();
        }

        [Test]
        public void RecoverSettingsFileIfNeeded_WhenPrimaryMissingAndBackupExists_ShouldRestoreBackup()
        {
            UnityCliLoopEditorSettingsData backupData = new() { showDeveloperTools = true };
            File.WriteAllText(BackupFilePath, JsonUtility.ToJson(backupData, true));

            UnityCliLoopEditorSettings.RecoverSettingsFileIfNeeded();

            Assert.IsTrue(File.Exists(SettingsFilePath), "Primary settings file should be restored from backup");
            Assert.IsFalse(File.Exists(BackupFilePath), "Backup should be consumed after recovery");

            UnityCliLoopEditorSettingsData restored = JsonUtility.FromJson<UnityCliLoopEditorSettingsData>(
                File.ReadAllText(SettingsFilePath));
            Assert.AreEqual(backupData.showDeveloperTools, restored.showDeveloperTools);
        }

        [Test]
        public void RecoverSettingsFileIfNeeded_WhenPrimaryMissingAndTempExists_ShouldPromoteTemp()
        {
            UnityCliLoopEditorSettingsData oldData = new() { showDeveloperTools = false };
            UnityCliLoopEditorSettingsData newData = new() { showDeveloperTools = true };
            File.WriteAllText(BackupFilePath, JsonUtility.ToJson(oldData, true));
            File.WriteAllText(TempFilePath, JsonUtility.ToJson(newData, true));

            UnityCliLoopEditorSettings.RecoverSettingsFileIfNeeded();

            Assert.IsTrue(File.Exists(SettingsFilePath), "Primary settings file should be restored from temp");
            Assert.IsFalse(File.Exists(BackupFilePath), "Backup should be removed after temp recovery");
            Assert.IsFalse(File.Exists(TempFilePath), "Temp file should be consumed after recovery");

            UnityCliLoopEditorSettingsData restored = JsonUtility.FromJson<UnityCliLoopEditorSettingsData>(
                File.ReadAllText(SettingsFilePath));
            Assert.AreEqual(newData.showDeveloperTools, restored.showDeveloperTools);
        }

        [Test]
        public void RecoverSettingsFileIfNeeded_WhenPrimaryExists_ShouldCleanStaleSidecars()
        {
            UnityCliLoopEditorSettingsData primaryData = new() { showDeveloperTools = true };
            File.WriteAllText(SettingsFilePath, JsonUtility.ToJson(primaryData, true));
            File.WriteAllText(BackupFilePath, JsonUtility.ToJson(new UnityCliLoopEditorSettingsData { showDeveloperTools = false }, true));
            File.WriteAllText(TempFilePath, JsonUtility.ToJson(new UnityCliLoopEditorSettingsData { showDeveloperTools = false }, true));

            UnityCliLoopEditorSettings.RecoverSettingsFileIfNeeded();

            Assert.IsFalse(File.Exists(BackupFilePath), "Backup should not linger once primary exists");
            Assert.IsFalse(File.Exists(TempFilePath), "Temp should not linger once primary exists");

            UnityCliLoopEditorSettingsData restored = JsonUtility.FromJson<UnityCliLoopEditorSettingsData>(
                File.ReadAllText(SettingsFilePath));
            Assert.AreEqual(primaryData.showDeveloperTools, restored.showDeveloperTools);
        }

        [Test]
        public void GetInstallSkillsFlat_WhenMissingFromSettings_DefaultsToTrue()
        {
            File.WriteAllText(SettingsFilePath, "{\"showDeveloperTools\":true}");
            UnityCliLoopEditorSettings.InvalidateCache();

            bool installSkillsFlat = UnityCliLoopEditorSettings.GetInstallSkillsFlat();

            Assert.IsTrue(installSkillsFlat);
        }

        [Test]
        public void RecoverSettingsFileIfNeeded_WhenLegacyPortFieldsExist_ShouldRemoveThem()
        {
            File.WriteAllText(
                SettingsFilePath,
                "{" +
                "\"customPort\":18447," +
                "\"serverPort\":18448," +
                "\"serverTransportKind\":\"tcp\"," +
                "\"projectRootPath\":\"/stale/project\"," +
                "\"serverSessionId\":\"stale-session\"," +
                "\"connectedLLMTools\":[{\"Name\":\"codex\",\"Endpoint\":\"/tmp/uloop/test.sock#1\",\"Port\":18449}]" +
                "}");

            UnityCliLoopEditorSettings.RecoverSettingsFileIfNeeded();

            string recoveredJson = File.ReadAllText(SettingsFilePath);
            StringAssert.DoesNotContain("customPort", recoveredJson);
            StringAssert.DoesNotContain("serverPort", recoveredJson);
            StringAssert.DoesNotContain("serverTransportKind", recoveredJson);
            StringAssert.DoesNotContain("projectRootPath", recoveredJson);
            StringAssert.DoesNotContain("serverSessionId", recoveredJson);
            StringAssert.DoesNotContain("connectedLLMTools", recoveredJson);
            StringAssert.DoesNotContain("\"Port\"", recoveredJson);
        }

        [Test]
        public void RecoverSettingsFileIfNeeded_WhenSettingsFileExceedsSizeLimit_ShouldThrowSecurityException()
        {
            File.WriteAllText(SettingsFilePath, new string(' ', UnityCliLoopConstants.MAX_SETTINGS_SIZE_BYTES + 1));

            Assert.Throws<SecurityException>(() => UnityCliLoopEditorSettings.RecoverSettingsFileIfNeeded());
        }

        [Test]
        public void SetInstallSkillsFlat_PersistsValue()
        {
            UnityCliLoopEditorSettings.SetInstallSkillsFlat(true);
            UnityCliLoopEditorSettings.InvalidateCache();

            bool installSkillsFlat = UnityCliLoopEditorSettings.GetInstallSkillsFlat();

            Assert.IsTrue(installSkillsFlat);
        }

        [Test]
        public void UpdateSessionState_WhenStartingServer_ShouldNotPersistRuntimeIdentity()
        {
            UnityCliLoopServerStartupService service = new();

            ServiceResult<bool> result = service.UpdateSessionState(true);

            Assert.IsTrue(result.Success, "Session update should succeed");
            Assert.IsTrue(UnityCliLoopEditorSettings.GetIsServerRunning(), "Server running state should be persisted");
            string savedJson = File.ReadAllText(SettingsFilePath);
            StringAssert.DoesNotContain("projectRootPath", savedJson);
            StringAssert.DoesNotContain("serverSessionId", savedJson);
        }

        private static void RestoreFile(string path, bool existed, string content)
        {
            if (existed)
            {
                File.WriteAllText(path, content);
                return;
            }

            DeleteIfExists(path);
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
