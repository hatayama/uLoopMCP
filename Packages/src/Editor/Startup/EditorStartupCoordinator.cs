using System;
using System.Collections.Generic;

using UnityEditor;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Centralizes heavyweight editor startup entrypoints so the synchronous InitializeOnLoad
    /// path stays small and measurable after domain reloads and editor launches.
    /// </summary>
    internal static class EditorStartupCoordinator
    {
        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            InitializeOnEditorLoad(
                AssetDatabase.IsAssetImportWorkerProcess(),
                Application.isBatchMode,
                McpEditorSettings.RecoverSettingsFileIfNeeded,
                McpServerController.EnsureInitialized,
                SetupWizardWindow.ScheduleAutoShowOnEditorLoad,
                McpServerController.ScheduleStartupRecovery,
                LogTimingEntries);
        }

        internal static bool InitializeOnEditorLoad(
            bool isAssetImportWorkerProcess,
            bool isBatchMode,
            Action recoverSettings,
            Action ensureServerControllerInitialized,
            Action scheduleSetupWizard,
            Action scheduleServerRecovery,
            Action<IReadOnlyCollection<string>> logTimingEntries)
        {
            if (isAssetImportWorkerProcess || isBatchMode)
            {
                return false;
            }

            Debug.Assert(recoverSettings != null, "recoverSettings must not be null");
            Debug.Assert(ensureServerControllerInitialized != null, "ensureServerControllerInitialized must not be null");
            Debug.Assert(scheduleSetupWizard != null, "scheduleSetupWizard must not be null");
            Debug.Assert(scheduleServerRecovery != null, "scheduleServerRecovery must not be null");
            Debug.Assert(logTimingEntries != null, "logTimingEntries must not be null");

            EditorStartupTelemetry.Reset();
            EditorStartupTelemetry.MarkSyncStarted();
            recoverSettings();
            ensureServerControllerInitialized();
            scheduleSetupWizard();
            scheduleServerRecovery();
            EditorStartupTelemetry.MarkSyncCompleted();

            logTimingEntries(EditorStartupTelemetry.CreateTimingEntries());
            return true;
        }

        private static void LogTimingEntries(IReadOnlyCollection<string> timingEntries)
        {
            foreach (string timingEntry in timingEntries)
            {
                Debug.Log(timingEntry);
            }
        }
    }
}
