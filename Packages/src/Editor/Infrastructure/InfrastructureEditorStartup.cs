namespace io.github.hatayama.UnityCliLoop
{
    // Groups infrastructure startup behind one facade so outer boot order stays explicit.
    internal static class InfrastructureEditorStartup
    {
        internal static void Initialize()
        {
            ProjectLocalCliAutoInstaller.ScheduleForEditorStartup();
        }
    }
}
