using System.IO;

using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP.Tests.Editor
{
    public class ProjectLocalCliInstallerTests
    {
        private string _temporaryRoot;

        [SetUp]
        public void SetUp()
        {
            _temporaryRoot = Path.Combine(
                Path.GetTempPath(),
                "uloop-project-local-cli-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temporaryRoot))
            {
                Directory.Delete(_temporaryRoot, recursive: true);
            }
        }

        [Test]
        public void InstallProjectLocalCliFromBundle_CopiesCliAndWindowsShim()
        {
            string sourceBundlePath = Path.Combine(_temporaryRoot, "source-cli.cjs");
            File.WriteAllText(sourceBundlePath, "#!/usr/bin/env node\nconsole.log('project cli');\n");

            string projectRoot = Path.Combine(_temporaryRoot, "Project");
            Directory.CreateDirectory(projectRoot);

            CliInstallResult result = ProjectLocalCliInstaller.InstallProjectLocalCliFromBundle(
                sourceBundlePath,
                projectRoot);

            string projectLocalCliPath = ProjectLocalCliInstaller.GetProjectLocalCliPath(projectRoot);
            string windowsCommandPath = ProjectLocalCliInstaller.GetProjectLocalWindowsCommandPath(projectRoot);

            Assert.That(result.Success, Is.True, result.ErrorOutput);
            Assert.That(File.Exists(projectLocalCliPath), Is.True);
            Assert.That(File.ReadAllText(projectLocalCliPath), Is.EqualTo(File.ReadAllText(sourceBundlePath)));
            Assert.That(File.Exists(windowsCommandPath), Is.True);
            Assert.That(File.ReadAllText(windowsCommandPath), Does.Contain("node \"%~dp0\\uloop\" %*"));
        }

        [Test]
        public void InstallProjectLocalCliFromBundle_ReturnsFailureWhenBundleIsMissing()
        {
            string missingSourceBundlePath = Path.Combine(_temporaryRoot, "missing.cjs");
            string projectRoot = Path.Combine(_temporaryRoot, "Project");
            Directory.CreateDirectory(projectRoot);

            CliInstallResult result = ProjectLocalCliInstaller.InstallProjectLocalCliFromBundle(
                missingSourceBundlePath,
                projectRoot);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorOutput, Does.Contain(missingSourceBundlePath));
        }

        [Test]
        public void IsProjectLocalCliVersionCurrent_WhenCliIsMissing_ReturnsFalse()
        {
            string projectRoot = Path.Combine(_temporaryRoot, "Project");
            Directory.CreateDirectory(projectRoot);

            bool isCurrent = ProjectLocalCliInstaller.IsProjectLocalCliVersionCurrent(projectRoot, "3.0.0");

            Assert.That(isCurrent, Is.False);
        }
    }
}
