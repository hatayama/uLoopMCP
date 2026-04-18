using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace io.github.hatayama.uLoopMCP
{
    internal static class SkillInstallLayout
    {
        internal const string SkillsDirName = "skills";
        internal const string ManagedSkillsDirName = "unity-cli-loop";
        internal const string SkillFileName = "SKILL.md";

        internal static string GetSkillsRoot(string targetRoot)
        {
            return Path.Combine(targetRoot, SkillsDirName);
        }

        internal static string GetManagedSkillsRoot(string targetRoot)
        {
            return Path.Combine(GetSkillsRoot(targetRoot), ManagedSkillsDirName);
        }

        internal static bool HasOptedInSkillsDirectory(string targetRoot)
        {
            return Directory.Exists(GetSkillsRoot(targetRoot));
        }

        internal static IEnumerable<string> EnumerateInstalledSkillDirectories(string targetRoot)
        {
            foreach (string skillDir in EnumerateManagedSkillDirectories(targetRoot))
            {
                yield return skillDir;
            }

            foreach (string skillDir in EnumerateLegacyManagedSkillDirectories(targetRoot))
            {
                yield return skillDir;
            }
        }

        internal static bool HasInstalledSkills(string targetRoot)
        {
            return EnumerateInstalledSkillDirectories(targetRoot)
                .Any(skillDir => File.Exists(Path.Combine(skillDir, SkillFileName)));
        }

        internal static bool SkillMatchesTool(string skillDir, string toolName)
        {
            string skillMdPath = Path.Combine(skillDir, SkillFileName);
            if (File.Exists(skillMdPath))
            {
                string content = File.ReadAllText(skillMdPath);
                string parsed = ParseToolNameFromFrontmatter(content);
                if (!string.IsNullOrEmpty(parsed))
                {
                    return parsed == toolName;
                }
            }

            string dirName = Path.GetFileName(skillDir);
            return dirName == $"{CliConstants.SKILL_DIR_PREFIX}{toolName}";
        }

        private static IEnumerable<string> EnumerateManagedSkillDirectories(string targetRoot)
        {
            string managedSkillsRoot = GetManagedSkillsRoot(targetRoot);
            if (!Directory.Exists(managedSkillsRoot))
            {
                return Enumerable.Empty<string>();
            }

            return Directory.EnumerateDirectories(managedSkillsRoot);
        }

        private static IEnumerable<string> EnumerateLegacyManagedSkillDirectories(string targetRoot)
        {
            string skillsRoot = GetSkillsRoot(targetRoot);
            if (!Directory.Exists(skillsRoot))
            {
                return Enumerable.Empty<string>();
            }

            return Directory.EnumerateDirectories(skillsRoot)
                .Where(skillDir => Path.GetFileName(skillDir) != ManagedSkillsDirName)
                .Where(IsLegacyManagedSkillDirectory);
        }

        private static bool IsLegacyManagedSkillDirectory(string skillDir)
        {
            string skillMdPath = Path.Combine(skillDir, SkillFileName);
            if (!File.Exists(skillMdPath))
            {
                return false;
            }

            string content = File.ReadAllText(skillMdPath);
            if (!string.IsNullOrEmpty(ParseToolNameFromFrontmatter(content)))
            {
                return true;
            }

            string dirName = Path.GetFileName(skillDir);
            return dirName.StartsWith(CliConstants.SKILL_DIR_PREFIX);
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
    }
}
