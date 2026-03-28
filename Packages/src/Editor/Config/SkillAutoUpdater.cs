using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using UnityEditor;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Prompts user to update skill files when uLoopMCP package version changes.
    /// </summary>
    [InitializeOnLoad]
    internal static class SkillAutoUpdater
    {
        static SkillAutoUpdater()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess()) return;
            if (Application.isBatchMode) return;

            string lastVersion = McpEditorSettings.GetSettings().lastSkillPromptVersion;
            if (lastVersion == McpVersion.VERSION) return;

            EditorApplication.delayCall += OnDelayedSkillUpdate;
        }

        private static async void OnDelayedSkillUpdate()
        {
            // async void can crash the AppDomain on unhandled exceptions;
            // finally guarantees SavePromptVersion runs on every exit path
            try
            {
                await RunSkillUpdateFlow();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[uLoopMCP] Skill auto-update failed: {ex.Message}");
            }
            finally
            {
                SavePromptVersion();
            }
        }

        private static async System.Threading.Tasks.Task RunSkillUpdateFlow()
        {
            List<ToolSkillSynchronizer.SkillTargetInfo> targets = ToolSkillSynchronizer.DetectTargets();

            if (targets.Count == 0)
            {
                return;
            }

            // ForceRefresh to get a definitive answer; RefreshCliVersionAsync early-returns when _isRefreshing
            await CliInstallationDetector.ForceRefreshCliVersionAsync(CancellationToken.None);

            if (!CliInstallationDetector.IsCliInstalled())
            {
                Debug.LogWarning(
                    "[uLoopMCP] uloop-cli was not found. Skill auto-update was skipped. " +
                    "Run 'uloop skills install' after installing the CLI.");
                return;
            }

            string message = BuildDialogMessage(targets);

            bool userAccepted = EditorUtility.DisplayDialog(
                "uLoopMCP - Skill Update",
                message,
                "Yes",
                "No");

            if (!userAccepted)
            {
                return;
            }

            ToolSkillSynchronizer.SkillInstallResult result = await ToolSkillSynchronizer.InstallSkillFiles(targets);

            if (result.IsSuccessful)
            {
                Debug.Log($"[uLoopMCP] Skills updated successfully for v{McpVersion.VERSION}");
            }
            else
            {
                Debug.LogWarning(
                    $"[uLoopMCP] Skill update partially failed: {result.SucceededTargets}/{result.AttemptedTargets} targets succeeded. " +
                    "Run 'uloop skills install' to retry.");
            }
        }

        private static void SavePromptVersion()
        {
            McpEditorSettings.UpdateSettings(s => s with
            {
                lastSkillPromptVersion = McpVersion.VERSION
            });
        }

        private static string BuildDialogMessage(List<ToolSkillSynchronizer.SkillTargetInfo> targets)
        {
            bool isFirstInstall = string.IsNullOrEmpty(
                McpEditorSettings.GetSettings().lastSkillPromptVersion);

            StringBuilder sb = new();

            if (isFirstInstall)
            {
                sb.AppendLine($"uLoopMCP v{McpVersion.VERSION} has been installed.");
            }
            else
            {
                sb.AppendLine($"uLoopMCP has been updated to v{McpVersion.VERSION}.");
            }

            sb.AppendLine();
            sb.AppendLine("Skills will be installed to:");

            foreach (ToolSkillSynchronizer.SkillTargetInfo target in targets)
            {
                string action = target.HasExistingSkills ? "update" : "new";
                sb.AppendLine($"  \u2022 {target.DisplayName} ({target.DirName}/skills/) [{action}]");
            }

            sb.AppendLine();
            sb.Append("You can also run 'uloop skills install' manually later.");

            return sb.ToString();
        }
    }
}
