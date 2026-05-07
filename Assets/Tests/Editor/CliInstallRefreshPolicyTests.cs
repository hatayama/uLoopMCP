using NUnit.Framework;

using io.github.hatayama.UnityCliLoop.Application;
using io.github.hatayama.UnityCliLoop.CompositionRoot;
using io.github.hatayama.UnityCliLoop.Domain;
using io.github.hatayama.UnityCliLoop.FirstPartyTools;
using io.github.hatayama.UnityCliLoop.Infrastructure;
using io.github.hatayama.UnityCliLoop.Presentation;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    public class CliInstallRefreshPolicyTests
    {
        [Test]
        public void ShouldRefreshSkillsAfterCliInstall_WhenCliWasNotInstalled_ReturnsTrue()
        {
            bool shouldRefresh = CliInstallRefreshPolicy.ShouldRefreshSkillsAfterCliInstall(
                wasCliInstalledBeforeInstall: false);

            Assert.That(shouldRefresh, Is.True);
        }

        [Test]
        public void ShouldRefreshSkillsAfterCliInstall_WhenCliWasAlreadyInstalled_ReturnsFalse()
        {
            bool shouldRefresh = CliInstallRefreshPolicy.ShouldRefreshSkillsAfterCliInstall(
                wasCliInstalledBeforeInstall: true);

            Assert.That(shouldRefresh, Is.False);
        }
    }
}
