using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    internal static class SkillInstallLayout
    {
        internal const string SkillsDirName = "skills";
        internal const string ManagedSkillsDirName = "unity-cli-loop";
        internal const string SkillFileName = "SKILL.md";
        private static readonly HashSet<string> ExcludedFileNames = new()
        {
            ".meta",
            ".DS_Store",
            ".gitkeep"
        };

        private sealed class SkillSourceDefinition
        {
            public readonly string Name;
            public readonly string SkillDirectoryPath;
            public readonly Dictionary<string, byte[]> SkillFiles;

            public SkillSourceDefinition(
                string name,
                string skillDirectoryPath,
                Dictionary<string, byte[]> skillFiles)
            {
                Name = name;
                SkillDirectoryPath = skillDirectoryPath;
                SkillFiles = skillFiles;
            }
        }

        private static string _cachedProjectRoot;
        private static Dictionary<string, SkillSourceDefinition> _cachedSkillSources;

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

        internal static bool HasInstalledSkills(string targetRoot, bool groupSkillsUnderUnityCliLoop)
        {
            IEnumerable<string> skillDirs = groupSkillsUnderUnityCliLoop
                ? EnumerateManagedSkillDirectories(targetRoot)
                : EnumerateLegacyManagedSkillDirectories(targetRoot);
            return skillDirs.Any(skillDir => File.Exists(Path.Combine(skillDir, SkillFileName)));
        }

        internal static SkillInstallState GetInstalledState(
            string projectRoot,
            string targetRoot,
            bool groupSkillsUnderUnityCliLoop)
        {
            Dictionary<string, SkillSourceDefinition> expectedSkills = GetSkillSources(projectRoot);
            bool hasLayoutSkills = HasInstalledSkills(targetRoot, groupSkillsUnderUnityCliLoop);
            if (expectedSkills.Count == 0)
            {
                return hasLayoutSkills ? SkillInstallState.Installed : SkillInstallState.Missing;
            }

            bool hasInstalledExpectedSkill = false;
            bool hasMissingExpectedSkill = false;

            foreach (SkillSourceDefinition expectedSkill in expectedSkills.Values)
            {
                string installedSkillDirectory = GetInstalledSkillDirectoryPath(
                    targetRoot,
                    expectedSkill.Name,
                    groupSkillsUnderUnityCliLoop);
                if (!Directory.Exists(installedSkillDirectory))
                {
                    hasMissingExpectedSkill = true;
                    continue;
                }

                hasInstalledExpectedSkill = true;
                if (IsSkillDirectoryOutdated(expectedSkill.SkillFiles, installedSkillDirectory))
                {
                    return SkillInstallState.Outdated;
                }
            }

            if (HasUnexpectedInstalledSkillDirectories(
                targetRoot,
                expectedSkills.Keys,
                groupSkillsUnderUnityCliLoop))
            {
                return SkillInstallState.Outdated;
            }

            if (!hasInstalledExpectedSkill)
            {
                return hasLayoutSkills ? SkillInstallState.Outdated : SkillInstallState.Missing;
            }

            return hasMissingExpectedSkill ? SkillInstallState.Outdated : SkillInstallState.Installed;
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

        private static string GetInstalledSkillDirectoryPath(
            string targetRoot,
            string skillName,
            bool groupSkillsUnderUnityCliLoop)
        {
            Debug.Assert(IsSafeSkillPathComponent(skillName), "skillName must be a single safe path component");

            string skillsRoot = groupSkillsUnderUnityCliLoop
                ? GetManagedSkillsRoot(targetRoot)
                : GetSkillsRoot(targetRoot);
            return Path.Combine(skillsRoot, skillName);
        }

        private static bool HasUnexpectedInstalledSkillDirectories(
            string targetRoot,
            IEnumerable<string> expectedSkillNames,
            bool groupSkillsUnderUnityCliLoop)
        {
            HashSet<string> expectedSkillNameSet = new(expectedSkillNames, StringComparer.Ordinal);
            IEnumerable<string> installedSkillDirectories = groupSkillsUnderUnityCliLoop
                ? EnumerateManagedSkillDirectories(targetRoot)
                : EnumerateLegacyManagedSkillDirectories(targetRoot);

            foreach (string installedSkillDirectory in installedSkillDirectories)
            {
                string installedSkillName = Path.GetFileName(installedSkillDirectory);
                if (string.IsNullOrEmpty(installedSkillName))
                {
                    continue;
                }

                if (!expectedSkillNameSet.Contains(installedSkillName))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSkillDirectoryOutdated(
            Dictionary<string, byte[]> sourceFiles,
            string installedSkillDirectory)
        {
            Dictionary<string, byte[]> installedFiles = CollectSkillFiles(installedSkillDirectory);
            if (sourceFiles.Count != installedFiles.Count)
            {
                return true;
            }

            foreach (KeyValuePair<string, byte[]> sourceFile in sourceFiles)
            {
                if (!installedFiles.TryGetValue(sourceFile.Key, out byte[] installedContent))
                {
                    return true;
                }

                if (!sourceFile.Value.SequenceEqual(installedContent))
                {
                    return true;
                }
            }

            return false;
        }

        private static Dictionary<string, byte[]> CollectSkillFiles(string skillDirectory)
        {
            Dictionary<string, byte[]> files = new(StringComparer.Ordinal);
            foreach (string filePath in Directory.EnumerateFiles(skillDirectory, "*", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileName(filePath);
                if (IsExcludedFile(fileName))
                {
                    continue;
                }

                string relativePath = Path.GetRelativePath(skillDirectory, filePath);
                files[relativePath] = File.ReadAllBytes(filePath);
            }

            return files;
        }

        private static Dictionary<string, SkillSourceDefinition> GetSkillSources(string projectRoot)
        {
            if (_cachedSkillSources != null && _cachedProjectRoot == projectRoot)
            {
                return _cachedSkillSources;
            }

            Dictionary<string, SkillSourceDefinition> sources = new(StringComparer.Ordinal);
            foreach (string searchRoot in EnumerateSkillSourceRoots(projectRoot))
            {
                if (!Directory.Exists(searchRoot))
                {
                    continue;
                }

                foreach (string skillFilePath in Directory.EnumerateFiles(
                    searchRoot,
                    SkillFileName,
                    SearchOption.AllDirectories))
                {
                    string skillDirectory = Path.GetDirectoryName(skillFilePath);
                    if (skillDirectory == null || Path.GetFileName(skillDirectory) != "Skill")
                    {
                        continue;
                    }

                    string skillContent = File.ReadAllText(skillFilePath);
                    if (IsInternalSkill(skillContent))
                    {
                        continue;
                    }

                    string skillName = ParseNameFromFrontmatter(skillContent);
                    if (string.IsNullOrEmpty(skillName)
                        || !IsSafeSkillPathComponent(skillName)
                        || sources.ContainsKey(skillName))
                    {
                        continue;
                    }

                    sources[skillName] = new SkillSourceDefinition(
                        skillName,
                        skillDirectory,
                        CollectSkillFiles(skillDirectory));
                }
            }

            _cachedProjectRoot = projectRoot;
            _cachedSkillSources = sources;
            return sources;
        }

        private static IEnumerable<string> EnumerateSkillSourceRoots(string projectRoot)
        {
            yield return Path.Combine(projectRoot, "Assets");
            yield return Path.Combine(projectRoot, "Packages");
            yield return Path.Combine(projectRoot, "Library", "PackageCache");
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

        private static string ParseNameFromFrontmatter(string content)
        {
            Match frontmatterMatch = Regex.Match(content, @"^---\r?\n([\s\S]*?)\r?\n---");
            if (!frontmatterMatch.Success)
            {
                return null;
            }

            string frontmatter = frontmatterMatch.Groups[1].Value;
            Match nameMatch = Regex.Match(frontmatter, @"^name:\s*(.+)$", RegexOptions.Multiline);
            if (!nameMatch.Success)
            {
                return null;
            }

            return nameMatch.Groups[1].Value.Trim().Trim('"');
        }

        private static bool IsInternalSkill(string content)
        {
            Match frontmatterMatch = Regex.Match(content, @"^---\r?\n([\s\S]*?)\r?\n---");
            if (!frontmatterMatch.Success)
            {
                return false;
            }

            string frontmatter = frontmatterMatch.Groups[1].Value;
            Match internalMatch = Regex.Match(frontmatter, @"^internal:\s*(.+)$", RegexOptions.Multiline);
            if (!internalMatch.Success)
            {
                return false;
            }

            return string.Equals(
                internalMatch.Groups[1].Value.Trim(),
                "true",
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsExcludedFile(string fileName)
        {
            if (ExcludedFileNames.Contains(fileName))
            {
                return true;
            }

            foreach (string excludedPattern in ExcludedFileNames)
            {
                if (fileName.EndsWith(excludedPattern, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSafeSkillPathComponent(string skillName)
        {
            if (string.IsNullOrEmpty(skillName))
            {
                return false;
            }

            if (skillName == "." || skillName == "..")
            {
                return false;
            }

            if (skillName.Contains('/') || skillName.Contains('\\'))
            {
                return false;
            }

            return string.Equals(Path.GetFileName(skillName), skillName, StringComparison.Ordinal);
        }
    }
}
