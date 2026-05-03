using System.IO;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.Tests
{
    public class NativeCliInstallerTests
    {
        [Test]
        public void GetInstallCommand_OnMacKeepsCliOnlyCurlInstallerAvailable()
        {
            // Verifies that CLI-only macOS users still have the direct release script, not npm.
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
        public void GetInstallCommand_OnWindowsKeepsCliOnlyPowerShellInstallerAvailable()
        {
            // Verifies that CLI-only Windows users still have the direct release script, not npm.
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
        public void GetInstallCommand_OnMacCliOnlyInstallerCanOptIntoLegacyNpmRemoval()
        {
            // Verifies that CLI-only macOS installs can opt into removing the legacy npm launcher.
            NativeCliInstallCommand command = NativeCliInstaller.GetInstallCommand(
                RuntimePlatform.OSXEditor,
                "3.0.0-beta.0",
                true);

            Assert.That(command.Arguments, Does.Contain("ULOOP_REMOVE_LEGACY='1'"));
            Assert.That(command.ManualCommand, Does.Contain("ULOOP_REMOVE_LEGACY='1'"));
        }

        [Test]
        public void GetInstallCommand_OnWindowsCliOnlyInstallerCanOptIntoLegacyNpmRemoval()
        {
            // Verifies that CLI-only Windows installs can opt into removing the legacy npm launcher.
            NativeCliInstallCommand command = NativeCliInstaller.GetInstallCommand(
                RuntimePlatform.WindowsEditor,
                "3.0.0-beta.0",
                true);

            Assert.That(command.Arguments, Does.Contain("$env:ULOOP_REMOVE_LEGACY='1'"));
            Assert.That(command.ManualCommand, Does.Contain("$env:ULOOP_REMOVE_LEGACY='1'"));
        }

        [Test]
        public void GetGlobalCliBundlePath_OnMacArm64UsesPackagedDispatcher()
        {
            // Verifies that the editor installer reads the bundled macOS dispatcher from the package.
            string result = NativeCliInstaller.GetGlobalCliBundlePath(
                "/package",
                RuntimePlatform.OSXEditor,
                Architecture.Arm64);

            Assert.That(result, Is.EqualTo(Path.Combine(
                "/package",
                "GoCli~",
                "dist",
                "darwin-arm64",
                "uloop-dispatcher")));
        }

        [Test]
        public void GetGlobalCliBundlePath_OnWindowsUsesPackagedDispatcher()
        {
            // Verifies that the editor installer reads the bundled Windows dispatcher from the package.
            string result = NativeCliInstaller.GetGlobalCliBundlePath(
                "C:\\package",
                RuntimePlatform.WindowsEditor,
                Architecture.X64);

            Assert.That(result, Is.EqualTo(Path.Combine(
                "C:\\package",
                "GoCli~",
                "dist",
                "windows-amd64",
                "uloop-dispatcher.exe")));
        }

        [Test]
        public void InstallGlobalCliFromBundle_OnWindowsCopiesDispatcherAsUloopExe()
        {
            // Verifies that editor install copies the bundled dispatcher as the user-facing uloop command.
            string tempRoot = Path.Combine(
                Path.GetTempPath(),
                "uloop-native-installer-tests",
                System.Guid.NewGuid().ToString("N"));
            string sourceDir = Path.Combine(tempRoot, "source");
            string sourcePath = Path.Combine(sourceDir, "uloop-dispatcher.exe");
            string installDir = Path.Combine(tempRoot, "install");

            Directory.CreateDirectory(sourceDir);
            File.WriteAllText(sourcePath, "fake-binary");

            try
            {
                CliInstallResult result = NativeCliInstaller.InstallGlobalCliFromBundle(
                    sourcePath,
                    installDir,
                    RuntimePlatform.WindowsEditor);

                string installedPath = NativeCliInstaller.GetGlobalCliInstallPath(
                    installDir,
                    RuntimePlatform.WindowsEditor);
                Assert.That(result.Success, Is.True);
                Assert.That(result.ErrorOutput, Is.Empty);
                Assert.That(Path.GetFileName(installedPath), Is.EqualTo("uloop.exe"));
                Assert.That(File.ReadAllText(installedPath), Is.EqualTo("fake-binary"));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Test]
        public void InstallGlobalCliFromBundle_WhenBundleIsMissingReturnsFailure()
        {
            // Verifies that editor install reports a missing packaged dispatcher without creating the install dir.
            string tempRoot = Path.Combine(
                Path.GetTempPath(),
                "uloop-native-installer-tests",
                System.Guid.NewGuid().ToString("N"));
            string sourcePath = Path.Combine(tempRoot, "missing", "uloop-dispatcher.exe");
            string installDir = Path.Combine(tempRoot, "install");

            try
            {
                CliInstallResult result = NativeCliInstaller.InstallGlobalCliFromBundle(
                    sourcePath,
                    installDir,
                    RuntimePlatform.WindowsEditor);

                Assert.That(result.Success, Is.False);
                Assert.That(result.ErrorOutput, Does.Contain("Global CLI dispatcher binary was not found"));
                Assert.That(Directory.Exists(installDir), Is.False);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Test]
        public void BuildPathWithInstallDirectory_OnWindowsPrependsMissingNativeInstallDir()
        {
            // Verifies that Unity's current Windows PATH prefers the freshly installed native CLI.
            string result = NativeCliInstaller.BuildPathWithInstallDirectory(
                "C:\\npm",
                "C:\\Users\\masamichi\\Programs\\uloop\\bin",
                RuntimePlatform.WindowsEditor);

            Assert.That(result, Is.EqualTo("C:\\Users\\masamichi\\Programs\\uloop\\bin;C:\\npm"));
        }

        [Test]
        public void BuildPathWithInstallDirectory_OnWindowsMovesExistingNativeInstallDirToFront()
        {
            // Verifies that a later Windows native install dir does not leave an earlier npm shim first.
            string result = NativeCliInstaller.BuildPathWithInstallDirectory(
                "C:\\npm;C:\\USERS\\MASAMICHI\\PROGRAMS\\ULOOP\\BIN",
                "C:\\Users\\masamichi\\Programs\\uloop\\bin",
                RuntimePlatform.WindowsEditor);

            Assert.That(result, Is.EqualTo("C:\\Users\\masamichi\\Programs\\uloop\\bin;C:\\npm"));
        }

        [Test]
        public void BuildPathWithInstallDirectory_OnMacPrependsMissingNativeInstallDir()
        {
            // Verifies that POSIX PATH prefers the freshly installed native CLI.
            string result = NativeCliInstaller.BuildPathWithInstallDirectory(
                "/usr/local/bin",
                "/Users/masamichi/.local/bin",
                RuntimePlatform.OSXEditor);

            Assert.That(result, Is.EqualTo("/Users/masamichi/.local/bin:/usr/local/bin"));
        }

        [Test]
        public void GetDefaultInstallDirectoryFromRoots_OnMacMatchesInstallerDefault()
        {
            // Verifies that Unity mirrors the POSIX installer default install directory.
            string result = NativeCliInstaller.GetDefaultInstallDirectoryFromRoots(
                RuntimePlatform.OSXEditor,
                "/Users/masamichi",
                null);

            Assert.That(result, Is.EqualTo(System.IO.Path.Combine("/Users/masamichi", ".local", "bin")));
        }

        [Test]
        public void GetDefaultInstallDirectoryFromRoots_OnWindowsMatchesInstallerDefault()
        {
            // Verifies that Unity mirrors the PowerShell installer default install directory.
            string result = NativeCliInstaller.GetDefaultInstallDirectoryFromRoots(
                RuntimePlatform.WindowsEditor,
                null,
                "C:\\Users\\masamichi\\AppData\\Local");

            Assert.That(result, Is.EqualTo(System.IO.Path.Combine(
                "C:\\Users\\masamichi\\AppData\\Local",
                "Programs",
                "uloop",
                "bin")));
        }
    }
}
