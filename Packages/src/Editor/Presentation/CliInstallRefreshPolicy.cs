
namespace io.github.hatayama.UnityCliLoop.Presentation
{
    internal static class CliInstallRefreshPolicy
    {
        internal static bool ShouldRefreshSkillsAfterCliInstall(bool wasCliInstalledBeforeInstall)
        {
            return !wasCliInstalledBeforeInstall;
        }
    }
}
