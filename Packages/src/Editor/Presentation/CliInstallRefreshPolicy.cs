using io.github.hatayama.UnityCliLoop.Application;
using io.github.hatayama.UnityCliLoop.Domain;
using io.github.hatayama.UnityCliLoop.ToolContracts;

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
