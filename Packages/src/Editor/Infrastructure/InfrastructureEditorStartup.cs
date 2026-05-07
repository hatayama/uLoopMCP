using io.github.hatayama.UnityCliLoop.Application;
using io.github.hatayama.UnityCliLoop.Domain;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Infrastructure
{
    // Groups infrastructure startup behind one facade so outer boot order stays explicit.
    internal static class InfrastructureEditorStartup
    {
        internal static void Initialize()
        {
            UnityCliLoopEditorSettingsRecoveryScheduler.ScheduleForEditorStartup();
            CompilationLockService.RegisterForEditorStartup();
            ProjectLocalCliAutoInstaller.ScheduleForEditorStartup();
        }
    }
}
