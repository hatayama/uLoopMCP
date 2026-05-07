using UnityEditor;

namespace io.github.hatayama.UnityCliLoop
{
    // Infrastructure scheduler for delayed settings file recovery during Editor startup.
    internal static class UnityCliLoopEditorSettingsRecoveryScheduler
    {
        internal static void ScheduleForEditorStartup()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess())
            {
                return;
            }

            EditorApplication.delayCall += UnityCliLoopEditorSettings.RecoverSettingsFileIfNeeded;
        }
    }
}
