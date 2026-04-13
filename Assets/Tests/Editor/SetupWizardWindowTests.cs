using System.IO;

using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP.Tests.Editor
{
    public class SetupWizardWindowTests
    {
        private static readonly string SettingsFilePath =
            Path.Combine(McpConstants.USER_SETTINGS_FOLDER, McpConstants.SETTINGS_FILE_NAME);

        private bool _settingsFileExisted;
        private string _settingsFileContent;

        [SetUp]
        public void SetUp()
        {
            _settingsFileExisted = File.Exists(SettingsFilePath);
            _settingsFileContent = _settingsFileExisted ? File.ReadAllText(SettingsFilePath) : null;

            if (!Directory.Exists(McpConstants.USER_SETTINGS_FOLDER))
            {
                Directory.CreateDirectory(McpConstants.USER_SETTINGS_FOLDER);
            }

            DeleteIfExists(SettingsFilePath);
            McpEditorSettings.InvalidateCache();
        }

        [TearDown]
        public void TearDown()
        {
            RestoreFile(SettingsFilePath, _settingsFileExisted, _settingsFileContent);
            McpEditorSettings.InvalidateCache();
        }

        [TestCase("", "1.7.3", false, true)]
        [TestCase("1.7.2", "1.7.3", false, true)]
        [TestCase("1.7.4", "1.7.3", false, true)]
        [TestCase("1.7.3", "1.7.3", false, false)]
        [TestCase("", "1.7.3", true, false)]
        [TestCase("1.7.2", "1.7.3", true, false)]
        public void ShouldAutoShowForVersion_ReturnsExpectedValue(
            string lastSeenVersion,
            string currentVersion,
            bool suppressAutoShow,
            bool expected)
        {
            bool shouldAutoShow =
                SetupWizardWindow.ShouldAutoShowForVersion(currentVersion, lastSeenVersion, suppressAutoShow);

            Assert.That(shouldAutoShow, Is.EqualTo(expected));
        }

        [Test]
        public void MaybeRecordLastSeenVersion_WhenAutoShow_UpdatesStoredVersion()
        {
            McpEditorSettings.SaveSettings(new McpEditorSettingsData
            {
                lastSeenSetupWizardVersion = "1.7.2"
            });

            SetupWizardWindow.MaybeRecordLastSeenVersion(true, "1.7.3");

            Assert.That(McpEditorSettings.GetLastSeenSetupWizardVersion(), Is.EqualTo("1.7.3"));
        }

        [Test]
        public void MaybeRecordLastSeenVersion_WhenManualShow_KeepsStoredVersion()
        {
            McpEditorSettings.SaveSettings(new McpEditorSettingsData
            {
                lastSeenSetupWizardVersion = "1.7.2"
            });

            SetupWizardWindow.MaybeRecordLastSeenVersion(false, "1.7.3");

            Assert.That(McpEditorSettings.GetLastSeenSetupWizardVersion(), Is.EqualTo("1.7.2"));
        }

        [Test]
        public void MaybeRecordSuppressedVersion_WhenAutoShowSuppressed_UpdatesStoredVersion()
        {
            McpEditorSettings.SaveSettings(new McpEditorSettingsData
            {
                lastSeenSetupWizardVersion = "1.7.2"
            });

            SetupWizardWindow.MaybeRecordSuppressedVersion(true, "1.7.3");

            Assert.That(McpEditorSettings.GetLastSeenSetupWizardVersion(), Is.EqualTo("1.7.3"));
        }

        [Test]
        public void MaybeRecordSuppressedVersion_WhenAutoShowAllowed_KeepsStoredVersion()
        {
            McpEditorSettings.SaveSettings(new McpEditorSettingsData
            {
                lastSeenSetupWizardVersion = "1.7.2"
            });

            SetupWizardWindow.MaybeRecordSuppressedVersion(false, "1.7.3");

            Assert.That(McpEditorSettings.GetLastSeenSetupWizardVersion(), Is.EqualTo("1.7.2"));
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
