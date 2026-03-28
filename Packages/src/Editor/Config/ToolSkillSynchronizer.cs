using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using UnityEngine;

using Debug = UnityEngine.Debug;

namespace io.github.hatayama.uLoopMCP
{
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
            public readonly bool HasExistingSkills;

            public SkillTargetInfo(string displayName, string dirName, bool hasExistingSkills)
            {
                DisplayName = displayName;
                DirName = dirName;
                HasExistingSkills = hasExistingSkills;
            }
        }

        private static readonly SkillTargetDefinition[] SkillTargets =
        {
            new(".claude", "--claude", "Claude Code"),
            new(".agents", "--codex", "Codex CLI / Gemini CLI"),
            new(".cursor", "--cursor", "Cursor"),
            new(".agent", "--antigravity", "Antigravity")
        };

        internal static readonly string[] SkillTargetDirs = SkillTargets.Select(t => t.DirName).ToArray();

        public static void RemoveSkillFiles(string toolName)
        {
            Debug.Assert(!string.IsNullOrEmpty(toolName), "toolName must not be null or empty");

            string projectRoot = UnityMcpPathResolver.GetProjectRoot();

            foreach (string targetDir in SkillTargetDirs)
            {
                string skillsRoot = Path.Combine(projectRoot, targetDir, "skills");
                if (!Directory.Exists(skillsRoot))
                {
                    continue;
                }

                string[] skillDirs = Directory.GetDirectories(skillsRoot, CliConstants.SKILL_DIR_GLOB);
                foreach (string skillDir in skillDirs)
                {
                    if (SkillMatchesTool(skillDir, toolName))
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
                string skillsRoot = Path.Combine(projectRoot, targetDir, "skills");
                if (!Directory.Exists(skillsRoot))
                {
                    continue;
                }

                string[] skillDirs = Directory.GetDirectories(skillsRoot, CliConstants.SKILL_DIR_GLOB);
                foreach (string skillDir in skillDirs)
                {
                    if (SkillMatchesTool(skillDir, toolName))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks parent directories (.claude/, .agents/, etc.), not skills/ subdirectories.
        /// </summary>
        public static List<SkillTargetInfo> DetectTargets()
        {
            string projectRoot = UnityMcpPathResolver.GetProjectRoot();
            Debug.Assert(!string.IsNullOrEmpty(projectRoot), "projectRoot must not be null or empty");

            List<SkillTargetInfo> targets = new();

            foreach (SkillTargetDefinition target in SkillTargets)
            {
                string parentDir = Path.Combine(projectRoot, target.DirName);
                if (!Directory.Exists(parentDir))
                {
                    continue;
                }

                string skillsRoot = Path.Combine(parentDir, "skills");
                bool hasULoopSkills = Directory.Exists(skillsRoot)
                    && Directory.EnumerateDirectories(skillsRoot, CliConstants.SKILL_DIR_GLOB).Any();
                targets.Add(new SkillTargetInfo(target.DisplayName, target.DirName, hasULoopSkills));
            }

            return targets;
        }

        /// <summary>
        /// Re-install skills for all detected targets.
        /// Checks parent directories (.claude/, .agents/, etc.) to determine targets.
        /// </summary>
        public static async Task<SkillInstallResult> InstallSkillFiles()
        {
            List<SkillTargetInfo> targets = DetectTargets();
            return await InstallSkillFiles(targets);
        }

        public static async Task<SkillInstallResult> InstallSkillFiles(List<SkillTargetInfo> targets)
        {
            Debug.Assert(targets != null, "targets must not be null");

            string uloopPath = NodeEnvironmentResolver.FindExecutablePath(CliConstants.EXECUTABLE_NAME);
            string uloopFileName = uloopPath ?? CliConstants.EXECUTABLE_NAME;
            string nodePath = NodeEnvironmentResolver.FindNodePath();

            int succeeded = 0;

            // Map DirName -> Flag for the targets we need to install
            Dictionary<string, string> targetFlagLookup = new();
            foreach (SkillTargetDefinition def in SkillTargets)
            {
                targetFlagLookup[def.DirName] = def.Flag;
            }

            foreach (SkillTargetInfo target in targets)
            {
                if (!targetFlagLookup.TryGetValue(target.DirName, out string flag))
                {
                    continue;
                }

                bool success = await RunSkillsInstall(flag, uloopFileName, nodePath);
                if (success)
                {
                    succeeded++;
                }
            }

            return new SkillInstallResult(targets.Count, succeeded);
        }

        private static bool SkillMatchesTool(string skillDir, string toolName)
        {
            string skillMdPath = Path.Combine(skillDir, "SKILL.md");
            if (File.Exists(skillMdPath))
            {
                string content = File.ReadAllText(skillMdPath);
                string parsed = ParseToolNameFromFrontmatter(content);
                if (!string.IsNullOrEmpty(parsed))
                {
                    return parsed == toolName;
                }
            }
            // Fallback: directory name "uloop-{toolName}"
            string dirName = Path.GetFileName(skillDir);
            return dirName == $"{CliConstants.SKILL_DIR_PREFIX}{toolName}";
        }

        private static string ParseToolNameFromFrontmatter(string content)
        {
            Match frontmatterMatch = Regex.Match(content, @"^---\r?\n([\s\S]*?)\r?\n---");
            if (!frontmatterMatch.Success)
            {
                return null;
            }

            string frontmatter = frontmatterMatch.Groups[1].Value;
            Match toolNameMatch = Regex.Match(frontmatter, @"^toolName:\s*(.+)$", RegexOptions.Multiline);
            if (!toolNameMatch.Success)
            {
                return null;
            }

            return toolNameMatch.Groups[1].Value.Trim();
        }

        private static async Task<bool> RunSkillsInstall(string targetFlag, string uloopFileName, string nodePath)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = uloopFileName,
                Arguments = $"skills install {targetFlag}",
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
                    Debug.LogWarning($"[uLoopMCP] Failed to start uloop process for: skills install {targetFlag}");
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
                        Debug.LogWarning($"[uLoopMCP] uloop skills install {targetFlag} exited with code {process.ExitCode}: {stderr}");
                        return false;
                    }

                    return true;
                }
            });
        }
    }
}
