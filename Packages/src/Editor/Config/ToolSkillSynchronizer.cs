using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using UnityEngine;

using Debug = UnityEngine.Debug;

namespace io.github.hatayama.uLoopMCP
{
    public enum SkillInstallState
    {
        Missing,
        Checking,
        Installed,
        Outdated
    }

    /// <summary>
    /// Synchronizes skill files when tools are enabled/disabled.
    /// Removes skill directories on disable, re-installs on enable.
    /// </summary>
    public static class ToolSkillSynchronizer
    {
        public readonly struct SkillTargetDefinition
        {
            public readonly string DirName;
            public readonly string Flag;
            public readonly string DisplayName;

            public SkillTargetDefinition(string dirName, string flag, string displayName)
            {
                DirName = dirName;
                Flag = flag;
                DisplayName = displayName;
            }
        }

        public readonly struct SkillInstallResult
        {
            public readonly int AttemptedTargets;
            public readonly int SucceededTargets;

            public SkillInstallResult(int attemptedTargets, int succeededTargets)
            {
                AttemptedTargets = attemptedTargets;
                SucceededTargets = succeededTargets;
            }

            public int FailedTargets => AttemptedTargets - SucceededTargets;
            public bool IsSuccessful => FailedTargets == 0;
        }

        public readonly struct SkillTargetInfo
        {
            public readonly string DisplayName;
            public readonly string DirName;
            public readonly string InstallFlag;
            public readonly bool HasSkillsDirectory;
            public readonly bool HasExistingSkills;
            public readonly bool HasDifferentLayoutSkills;
            public readonly SkillInstallState InstallState;

            public SkillTargetInfo(
                string displayName,
                string dirName,
                string installFlag,
                bool hasSkillsDirectory,
                bool hasExistingSkills,
                bool hasDifferentLayoutSkills = false,
                SkillInstallState installState = SkillInstallState.Missing)
            {
                DisplayName = displayName;
                DirName = dirName;
                InstallFlag = installFlag;
                HasSkillsDirectory = hasSkillsDirectory;
                HasExistingSkills = hasExistingSkills;
                HasDifferentLayoutSkills = hasDifferentLayoutSkills;
                InstallState = installState;
            }
        }

        private static readonly SkillTargetDefinition[] SkillTargets =
        {
            new(".claude", "--claude", "Claude Code"),
            new(".cursor", "--cursor", "Cursor"),
            new(".gemini", "--gemini", "Gemini CLI"),
            new(".codex", "--codex", "Codex CLI"),
            new(".agents", "--agents", "Other (.agents)"),
            new(".agent", "--antigravity", "Antigravity")
        };

        internal static readonly string[] SkillTargetDirs = SkillTargets.Select(t => t.DirName).ToArray();

        public static void RemoveSkillFiles(string toolName)
        {
            Debug.Assert(!string.IsNullOrEmpty(toolName), "toolName must not be null or empty");

            string projectRoot = UnityMcpPathResolver.GetProjectRoot();

            foreach (string targetDir in SkillTargetDirs)
            {
                string targetRoot = Path.Combine(projectRoot, targetDir);
                if (!Directory.Exists(targetRoot))
                {
                    continue;
                }

                foreach (string skillDir in SkillInstallLayout.EnumerateInstalledSkillDirectories(targetRoot))
                {
                    if (SkillInstallLayout.SkillMatchesTool(skillDir, toolName))
                    {
                        Directory.Delete(skillDir, true);
                    }
                }
            }
        }

        public static bool IsSkillInstalled(string toolName)
        {
            Debug.Assert(!string.IsNullOrEmpty(toolName), "toolName must not be null or empty");

            string projectRoot = UnityMcpPathResolver.GetProjectRoot();

            foreach (string targetDir in SkillTargetDirs)
            {
                string targetRoot = Path.Combine(projectRoot, targetDir);
                if (!Directory.Exists(targetRoot))
                {
                    continue;
                }

                foreach (string skillDir in SkillInstallLayout.EnumerateInstalledSkillDirectories(targetRoot))
                {
                    if (SkillInstallLayout.SkillMatchesTool(skillDir, toolName))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static List<SkillTargetInfo> DetectTargets()
        {
            return DetectTargets(requireSkillsDirectory: false);
        }

        public static List<SkillTargetInfo> DetectTargetsForLayout(bool groupSkillsUnderUnityCliLoop)
        {
            return DetectTargets(requireSkillsDirectory: false, groupSkillsUnderUnityCliLoop);
        }

        public static List<SkillTargetInfo> DetectTargetsForLayoutFast(bool groupSkillsUnderUnityCliLoop)
        {
            return DetectTargets(
                requireSkillsDirectory: false,
                groupSkillsUnderUnityCliLoop,
                includeFreshnessCheck: false);
        }

        internal static List<SkillTargetInfo> DetectTargets(bool requireSkillsDirectory)
        {
            string projectRoot = UnityMcpPathResolver.GetProjectRoot();
            Debug.Assert(!string.IsNullOrEmpty(projectRoot), "projectRoot must not be null or empty");

            return DetectTargets(projectRoot, requireSkillsDirectory);
        }

        internal static List<SkillTargetInfo> DetectTargets(
            bool requireSkillsDirectory,
            bool groupSkillsUnderUnityCliLoop)
        {
            string projectRoot = UnityMcpPathResolver.GetProjectRoot();
            Debug.Assert(!string.IsNullOrEmpty(projectRoot), "projectRoot must not be null or empty");

            return DetectTargets(
                projectRoot,
                requireSkillsDirectory,
                groupSkillsUnderUnityCliLoop,
                includeFreshnessCheck: true);
        }

        internal static List<SkillTargetInfo> DetectTargets(
            bool requireSkillsDirectory,
            bool groupSkillsUnderUnityCliLoop,
            bool includeFreshnessCheck)
        {
            string projectRoot = UnityMcpPathResolver.GetProjectRoot();
            Debug.Assert(!string.IsNullOrEmpty(projectRoot), "projectRoot must not be null or empty");

            return DetectTargets(
                projectRoot,
                requireSkillsDirectory,
                groupSkillsUnderUnityCliLoop,
                includeFreshnessCheck);
        }

        internal static List<SkillTargetInfo> DetectTargets(string projectRoot, bool requireSkillsDirectory)
        {
            Debug.Assert(!string.IsNullOrEmpty(projectRoot), "projectRoot must not be null or empty");

            List<SkillTargetInfo> targets = new();

            foreach (SkillTargetDefinition target in SkillTargets)
            {
                string targetRoot = Path.Combine(projectRoot, target.DirName);
                if (!Directory.Exists(targetRoot))
                {
                    continue;
                }

                bool hasSkillsDirectory = SkillInstallLayout.HasOptedInSkillsDirectory(targetRoot);
                if (requireSkillsDirectory && !hasSkillsDirectory)
                {
                    continue;
                }

                bool hasULoopSkills = hasSkillsDirectory && SkillInstallLayout.HasInstalledSkills(targetRoot);
                targets.Add(new SkillTargetInfo(
                    target.DisplayName,
                    target.DirName,
                    target.Flag,
                    hasSkillsDirectory,
                    hasULoopSkills,
                    installState: hasULoopSkills
                        ? SkillInstallState.Installed
                        : SkillInstallState.Missing));
            }

            return targets;
        }

        internal static List<SkillTargetInfo> DetectTargets(
            string projectRoot,
            bool requireSkillsDirectory,
            bool groupSkillsUnderUnityCliLoop)
        {
            return DetectTargets(
                projectRoot,
                requireSkillsDirectory,
                groupSkillsUnderUnityCliLoop,
                includeFreshnessCheck: true);
        }

        internal static List<SkillTargetInfo> DetectTargets(
            string projectRoot,
            bool requireSkillsDirectory,
            bool groupSkillsUnderUnityCliLoop,
            bool includeFreshnessCheck)
        {
            Debug.Assert(!string.IsNullOrEmpty(projectRoot), "projectRoot must not be null or empty");

            List<SkillTargetInfo> targets = new();

            foreach (SkillTargetDefinition target in SkillTargets)
            {
                string targetRoot = Path.Combine(projectRoot, target.DirName);
                if (!Directory.Exists(targetRoot))
                {
                    continue;
                }

                bool hasSkillsDirectory = SkillInstallLayout.HasOptedInSkillsDirectory(targetRoot);
                if (requireSkillsDirectory && !hasSkillsDirectory)
                {
                    continue;
                }

                SkillInstallState installState = ResolveInstallState(
                    projectRoot,
                    targetRoot,
                    hasSkillsDirectory,
                    groupSkillsUnderUnityCliLoop,
                    includeFreshnessCheck);
                bool hasULoopSkills = installState == SkillInstallState.Installed
                    || installState == SkillInstallState.Checking;
                bool hasDifferentLayoutSkills = hasSkillsDirectory
                    && SkillInstallLayout.HasInstalledSkills(targetRoot, !groupSkillsUnderUnityCliLoop);
                targets.Add(new SkillTargetInfo(
                    target.DisplayName,
                    target.DirName,
                    target.Flag,
                    hasSkillsDirectory,
                    hasULoopSkills,
                    hasDifferentLayoutSkills,
                    installState));
            }

            return targets;
        }

        private static SkillInstallState ResolveInstallState(
            string projectRoot,
            string targetRoot,
            bool hasSkillsDirectory,
            bool groupSkillsUnderUnityCliLoop,
            bool includeFreshnessCheck)
        {
            if (!hasSkillsDirectory)
            {
                return SkillInstallState.Missing;
            }

            if (!includeFreshnessCheck)
            {
                return SkillInstallLayout.HasInstalledSkills(targetRoot, groupSkillsUnderUnityCliLoop)
                    ? SkillInstallState.Checking
                    : SkillInstallState.Missing;
            }

            return SkillInstallLayout.GetInstalledState(projectRoot, targetRoot, groupSkillsUnderUnityCliLoop);
        }

        /// <summary>
        /// Re-installs skills only for targets that already opted in via an existing skills directory.
        /// </summary>
        public static async Task<SkillInstallResult> InstallSkillFiles()
        {
            return await InstallSkillFiles(groupSkillsUnderUnityCliLoop: true);
        }

        public static async Task<SkillInstallResult> InstallSkillFiles(bool groupSkillsUnderUnityCliLoop)
        {
            List<SkillTargetInfo> targets = DetectTargets(requireSkillsDirectory: true);
            return await InstallSkillFiles(targets, groupSkillsUnderUnityCliLoop);
        }

        public static async Task<SkillInstallResult> InstallSkillFiles(List<SkillTargetInfo> targets)
        {
            return await InstallSkillFiles(targets, groupSkillsUnderUnityCliLoop: true);
        }

        public static async Task<SkillInstallResult> InstallSkillFiles(
            List<SkillTargetInfo> targets,
            bool groupSkillsUnderUnityCliLoop)
        {
            Debug.Assert(targets != null, "targets must not be null");

            string uloopPath = NodeEnvironmentResolver.FindExecutablePath(CliConstants.EXECUTABLE_NAME);
            string uloopFileName = uloopPath ?? CliConstants.EXECUTABLE_NAME;
            string nodePath = NodeEnvironmentResolver.FindNodePath();

            int succeeded = 0;

            foreach (SkillTargetInfo target in targets)
            {
                bool success = await RunSkillsInstall(
                    target.InstallFlag,
                    groupSkillsUnderUnityCliLoop,
                    uloopFileName,
                    nodePath);
                if (success)
                {
                    succeeded++;
                }
            }

            return new SkillInstallResult(targets.Count, succeeded);
        }

        private static async Task<bool> RunSkillsInstall(
            string targetFlag,
            bool groupSkillsUnderUnityCliLoop,
            string uloopFileName,
            string nodePath)
        {
            string arguments = groupSkillsUnderUnityCliLoop
                ? $"skills install {targetFlag}"
                : $"skills install {targetFlag} --flat";
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = uloopFileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            NodeEnvironmentResolver.SetupEnvironmentPath(startInfo, nodePath);

            return await Task.Run(() =>
            {
                Process process = ProcessStartHelper.TryStart(startInfo);
                if (process == null)
                {
                    Debug.LogWarning($"[uLoopMCP] Failed to start uloop process for: {arguments}");
                    return false;
                }

                using (process)
                {
                    // Read stderr asynchronously to prevent buffer deadlock
                    // when stdout and stderr are both redirected
                    Task<string> stderrTask = process.StandardError.ReadToEndAsync();
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = stderrTask.Result;
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        Debug.LogWarning($"[uLoopMCP] uloop {arguments} exited with code {process.ExitCode}: {stderr}");
                        return false;
                    }

                    return true;
                }
            });
        }
    }
}
