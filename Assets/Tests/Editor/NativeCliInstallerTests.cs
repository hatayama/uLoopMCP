using NUnit.Framework;
using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.Tests
{
    public class NativeCliInstallerTests
    {
        [Test]
        public void GetInstallCommand_OnMacUsesDirectInstallScriptWithoutNpm()
        {
            // Verifies that macOS installs through the native release script, not npm.
            NativeCliInstallCommand command = NativeCliInstaller.GetInstallCommand(
                RuntimePlatform.OSXEditor,
                "3.0.0-beta.0",
                false);

            Assert.That(command.FileName, Is.EqualTo("/bin/sh"));
            Assert.That(command.Arguments, Does.Contain("https://github.com/hatayama/unity-cli-loop/releases/download/v3.0.0-beta.0/install.sh"));
            Assert.That(command.Arguments, Does.Contain("ULOOP_VERSION='v3.0.0-beta.0'"));
            Assert.That(command.Arguments, Does.Not.Contain("ULOOP_REMOVE_LEGACY"));
            Assert.That(command.ManualCommand, Does.Contain("curl -fsSL"));
            Assert.That(command.ManualCommand, Does.Not.Contain("npm"));
        }

        [Test]
        public void GetInstallCommand_OnWindowsUsesPowerShellInstallScriptWithoutNpm()
        {
            // Verifies that Windows installs through the native release script, not npm.
            NativeCliInstallCommand command = NativeCliInstaller.GetInstallCommand(
                RuntimePlatform.WindowsEditor,
                "3.0.0-beta.0",
                false);

            Assert.That(command.FileName, Is.EqualTo("powershell"));
            Assert.That(command.Arguments, Does.Contain("https://github.com/hatayama/unity-cli-loop/releases/download/v3.0.0-beta.0/install.ps1"));
            Assert.That(command.Arguments, Does.Contain("$env:ULOOP_VERSION='v3.0.0-beta.0'"));
            Assert.That(command.Arguments, Does.Not.Contain("ULOOP_REMOVE_LEGACY"));
            Assert.That(command.ManualCommand, Does.Contain("irm"));
            Assert.That(command.ManualCommand, Does.Not.Contain("npm"));
        }

        [Test]
        public void GetInstallCommand_OnMacCanOptIntoLegacyNpmRemoval()
        {
            // Verifies that UI-triggered macOS installs can opt into removing the legacy npm launcher.
            NativeCliInstallCommand command = NativeCliInstaller.GetInstallCommand(
                RuntimePlatform.OSXEditor,
                "3.0.0-beta.0",
                true);

            Assert.That(command.Arguments, Does.Contain("ULOOP_REMOVE_LEGACY='1'"));
            Assert.That(command.ManualCommand, Does.Contain("ULOOP_REMOVE_LEGACY='1'"));
        }

        [Test]
        public void GetInstallCommand_OnWindowsCanOptIntoLegacyNpmRemoval()
        {
            // Verifies that UI-triggered Windows installs can opt into removing the legacy npm launcher.
            NativeCliInstallCommand command = NativeCliInstaller.GetInstallCommand(
                RuntimePlatform.WindowsEditor,
                "3.0.0-beta.0",
                true);

            Assert.That(command.Arguments, Does.Contain("$env:ULOOP_REMOVE_LEGACY='1'"));
            Assert.That(command.ManualCommand, Does.Contain("$env:ULOOP_REMOVE_LEGACY='1'"));
        }
    }
}
