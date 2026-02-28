using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        private static readonly string[] SkillTargetDirs = { ".claude", ".codex", ".cursor", ".gemini", ".windsurf" };

        private static readonly Dictionary<string, string> TargetFlagMap = new()
        {
            { ".claude", "--claude" },
            { ".codex", "--codex" },
            { ".cursor", "--cursor" },
            { ".gemini", "--gemini" },
            { ".windsurf", "--windsurf" }
        };

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

                string[] skillDirs = Directory.GetDirectories(skillsRoot, "uloop-*");
                foreach (string skillDir in skillDirs)
                {
                    if (SkillMatchesTool(skillDir, toolName))
                    {
                        Directory.Delete(skillDir, true);
                    }
                }
            }
        }

        /// <summary>
        /// Re-install skills for all installed targets.
        /// Detects which targets have skill directories and runs `uloop skills install` for each.
        /// </summary>
        public static async Task InstallSkillFiles()
        {
            string projectRoot = UnityMcpPathResolver.GetProjectRoot();

            foreach (KeyValuePair<string, string> entry in TargetFlagMap)
            {
                string skillsDir = Path.Combine(projectRoot, entry.Key, "skills");
                if (!Directory.Exists(skillsDir))
                {
                    continue;
                }

                await RunSkillsInstall(entry.Value);
            }
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
            return dirName == $"uloop-{toolName}";
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

        private static async Task RunSkillsInstall(string targetFlag)
        {
            string uloopPath = NodeEnvironmentResolver.FindExecutablePath(CliConstants.EXECUTABLE_NAME);
            string uloopFileName = uloopPath ?? CliConstants.EXECUTABLE_NAME;

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = uloopFileName,
                Arguments = $"skills install {targetFlag}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            NodeEnvironmentResolver.SetupEnvironmentPath(startInfo, NodeEnvironmentResolver.FindNodePath());

            await Task.Run(() =>
            {
                Process process = Process.Start(startInfo);
                process?.WaitForExit();
                process?.Dispose();
            });
        }
    }
}
