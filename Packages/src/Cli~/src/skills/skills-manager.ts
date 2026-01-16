/**
 * Claude Code and other AI tools require skills to be in specific directories.
 * This module bridges the gap between bundled/project skills and target tool
 * configurations, handling path resolution and file synchronization.
 */

// File paths are constructed from home directory and skill names, not from untrusted user input
/* eslint-disable security/detect-non-literal-fs-filename */

import { existsSync, mkdirSync, readFileSync, writeFileSync, rmSync, readdirSync } from 'fs';
import { join, dirname, resolve, isAbsolute, sep } from 'path';
import { homedir } from 'os';
import { TargetConfig } from './target-config.js';
import { findUnityProjectRoot, getUnityProjectStatus } from '../project-root.js';
import { DEPRECATED_SKILLS } from './deprecated-skills.js';

export type SkillStatus = 'installed' | 'not_installed' | 'outdated';

export interface SkillInfo {
  name: string;
  status: SkillStatus;
  path?: string;
  source?: 'bundled' | 'project';
}

export interface SkillDefinition {
  name: string;
  dirName: string;
  content: string;
  sourcePath: string;
  additionalFiles?: Record<string, Buffer>;
  sourceType: 'package' | 'cli-only' | 'project';
}

const EXCLUDED_DIRS = new Set([
  'node_modules',
  '.git',
  'Temp',
  'obj',
  'Build',
  'Builds',
  'Logs',
  'Skill',
]);
const EXCLUDED_FILES = new Set(['.meta', '.DS_Store', '.gitkeep']);
class SkillsPathConstants {
  public static readonly PACKAGES_DIR = 'Packages';
  public static readonly SRC_DIR = 'src';
  public static readonly SKILLS_DIR = 'skills';
  public static readonly EDITOR_DIR = 'Editor';
  public static readonly API_DIR = 'Api';
  public static readonly MCP_TOOLS_DIR = 'McpTools';
  public static readonly SKILL_DIR = 'Skill';
  public static readonly LIBRARY_DIR = 'Library';
  public static readonly PACKAGE_CACHE_DIR = 'PackageCache';
  public static readonly ASSETS_DIR = 'Assets';
  public static readonly MANIFEST_FILE = 'manifest.json';
  public static readonly SKILL_FILE = 'SKILL.md';
  public static readonly CLI_ONLY_DIR = 'skill-definitions';
  public static readonly CLI_ONLY_SUBDIR = 'cli-only';
  public static readonly DIST_PARENT_DIR = '..';
  public static readonly FILE_PROTOCOL = 'file:';
  public static readonly PATH_PROTOCOL = 'path:';
  public static readonly PACKAGE_NAME = 'io.github.hatayama.uloopmcp';
  public static readonly PACKAGE_NAME_ALIAS = 'io.github.hatayama.uLoopMCP';
  public static readonly PACKAGE_NAMES = [
    SkillsPathConstants.PACKAGE_NAME,
    SkillsPathConstants.PACKAGE_NAME_ALIAS,
  ];
}

function getGlobalSkillsDir(target: TargetConfig): string {
  return join(homedir(), target.projectDir, 'skills');
}

function getProjectSkillsDir(target: TargetConfig): string {
  const status = getUnityProjectStatus();
  if (!status.found) {
    throw new Error(
      'Not inside a Unity project. Run this command from within a Unity project directory.',
    );
  }
  if (!status.hasUloop) {
    throw new Error(
      `uLoopMCP is not installed in this Unity project (${status.path}).\n` +
        'Please install uLoopMCP package first, then run this command again.',
    );
  }
  return join(status.path as string, target.projectDir, 'skills');
}

function getSkillPath(skillDirName: string, target: TargetConfig, global: boolean): string {
  const baseDir = global ? getGlobalSkillsDir(target) : getProjectSkillsDir(target);
  return join(baseDir, skillDirName, target.skillFileName);
}

function isSkillInstalled(skill: SkillDefinition, target: TargetConfig, global: boolean): boolean {
  const skillPath = getSkillPath(skill.dirName, target, global);
  return existsSync(skillPath);
}

function isSkillOutdated(skill: SkillDefinition, target: TargetConfig, global: boolean): boolean {
  const baseDir = global ? getGlobalSkillsDir(target) : getProjectSkillsDir(target);
  const skillDir = join(baseDir, skill.dirName);
  const skillPath = join(skillDir, target.skillFileName);

  if (!existsSync(skillPath)) {
    return false;
  }

  const installedContent = readFileSync(skillPath, 'utf-8');
  if (installedContent !== skill.content) {
    return true;
  }

  if ('additionalFiles' in skill && skill.additionalFiles) {
    const additionalFiles: Record<string, Buffer> = skill.additionalFiles;
    for (const [relativePath, expectedContent] of Object.entries(additionalFiles)) {
      const filePath = join(skillDir, relativePath);
      if (!existsSync(filePath)) {
        return true;
      }
      const installedFileContent = readFileSync(filePath);
      if (!installedFileContent.equals(expectedContent)) {
        return true;
      }
    }
  }

  return false;
}

export function getSkillStatus(
  skill: SkillDefinition,
  target: TargetConfig,
  global: boolean,
): SkillStatus {
  if (!isSkillInstalled(skill, target, global)) {
    return 'not_installed';
  }
  if (isSkillOutdated(skill, target, global)) {
    return 'outdated';
  }
  return 'installed';
}

export function parseFrontmatter(content: string): Record<string, string | boolean> {
  const frontmatterMatch = content.match(/^---\n([\s\S]*?)\n---/);
  if (!frontmatterMatch) {
    return {};
  }

  const frontmatterMap = new Map<string, string | boolean>();
  const lines = frontmatterMatch[1].split('\n');

  for (const line of lines) {
    const colonIndex = line.indexOf(':');
    if (colonIndex === -1) {
      continue;
    }

    const key = line.slice(0, colonIndex).trim();
    const rawValue = line.slice(colonIndex + 1).trim();

    let parsedValue: string | boolean = rawValue;
    if (rawValue === 'true') {
      parsedValue = true;
    } else if (rawValue === 'false') {
      parsedValue = false;
    }

    frontmatterMap.set(key, parsedValue);
  }

  return Object.fromEntries(frontmatterMap);
}

const warnedLegacyPaths = new Set<string>();

function warnLegacySkillStructure(toolPath: string, legacySkillMdPath: string): void {
  if (warnedLegacyPaths.has(legacySkillMdPath)) {
    return;
  }
  warnedLegacyPaths.add(legacySkillMdPath);

  /* eslint-disable no-console -- CLI user-facing warning output */
  console.error('\x1b[33m' + '='.repeat(70) + '\x1b[0m');
  console.error('\x1b[33mWarning: Legacy skill structure detected\x1b[0m');
  console.error(`  Path: ${legacySkillMdPath}`);
  console.error('');
  console.error('  The skill structure has changed. Please migrate to the new format:');
  console.error('    1. Create a "Skill" folder in the tool directory');
  console.error('    2. Move SKILL.md and any additional files/folders into Skill/');
  console.error('');
  console.error('  Expected structure:');
  console.error('    ToolName/');
  console.error('      └── Skill/');
  console.error('            ├── SKILL.md');
  console.error('            └── (any additional files or directories)');
  console.error('\x1b[33m' + '='.repeat(70) + '\x1b[0m');
  console.error('');
  /* eslint-enable no-console */
}

function scanEditorFolderForSkills(
  editorPath: string,
  skills: SkillDefinition[],
  sourceType: SkillDefinition['sourceType'],
  warnLegacy: boolean = true,
): void {
  if (!existsSync(editorPath)) {
    return;
  }

  const entries = readdirSync(editorPath, { withFileTypes: true });

  for (const entry of entries) {
    if (EXCLUDED_DIRS.has(entry.name)) {
      continue;
    }

    const fullPath = join(editorPath, entry.name);

    if (entry.isDirectory()) {
      const skillDir = join(fullPath, SkillsPathConstants.SKILL_DIR);
      const skillMdPath = join(skillDir, SkillsPathConstants.SKILL_FILE);

      const legacySkillMdPath = join(fullPath, SkillsPathConstants.SKILL_FILE);
      if (warnLegacy && !existsSync(skillMdPath) && existsSync(legacySkillMdPath)) {
        warnLegacySkillStructure(fullPath, legacySkillMdPath);
      }

      if (existsSync(skillMdPath)) {
        const content = readFileSync(skillMdPath, 'utf-8');
        const frontmatter = parseFrontmatter(content);

        if (frontmatter.internal === true) {
          continue;
        }

        const name = typeof frontmatter.name === 'string' ? frontmatter.name : entry.name;
        const additionalFiles = collectSkillFolderFiles(skillDir);

        skills.push({
          name,
          dirName: name,
          content,
          sourcePath: skillMdPath,
          additionalFiles,
          sourceType,
        });
      }

      scanEditorFolderForSkills(fullPath, skills, sourceType, warnLegacy);
    }
  }
}

function findEditorFolders(basePath: string, maxDepth: number = 2): string[] {
  const editorFolders: string[] = [];

  function scan(currentPath: string, depth: number): void {
    if (depth > maxDepth || !existsSync(currentPath)) {
      return;
    }

    const entries = readdirSync(currentPath, { withFileTypes: true });

    for (const entry of entries) {
      if (!entry.isDirectory() || EXCLUDED_DIRS.has(entry.name)) {
        continue;
      }

      const fullPath = join(currentPath, entry.name);

      if (entry.name === 'Editor') {
        editorFolders.push(fullPath);
      } else {
        scan(fullPath, depth + 1);
      }
    }
  }

  scan(basePath, 0);
  return editorFolders;
}

export function collectProjectSkills(excludedRoots: string[] = []): SkillDefinition[] {
  const projectRoot = findUnityProjectRoot();
  if (!projectRoot) {
    return [];
  }
  const skills: SkillDefinition[] = [];
  const seenNames = new Set<string>();

  const searchPaths = [
    join(projectRoot, SkillsPathConstants.ASSETS_DIR),
    join(projectRoot, SkillsPathConstants.PACKAGES_DIR),
    join(projectRoot, SkillsPathConstants.LIBRARY_DIR, SkillsPathConstants.PACKAGE_CACHE_DIR),
  ];

  for (const searchPath of searchPaths) {
    if (!existsSync(searchPath)) {
      continue;
    }

    const editorFolders = findEditorFolders(searchPath, 3);

    for (const editorFolder of editorFolders) {
      scanEditorFolderForSkills(editorFolder, skills, 'project');
    }
  }

  const uniqueSkills: SkillDefinition[] = [];
  for (const skill of skills) {
    if (isUnderExcludedRoots(skill.sourcePath, excludedRoots)) {
      continue;
    }
    if (!seenNames.has(skill.name)) {
      seenNames.add(skill.name);
      uniqueSkills.push(skill);
    }
  }

  return uniqueSkills;
}

export function getAllSkillStatuses(target: TargetConfig, global: boolean): SkillInfo[] {
  const allSkills = collectAllSkills();
  return allSkills.map((skill) => ({
    name: skill.name,
    status: getSkillStatus(skill, target, global),
    path: isSkillInstalled(skill, target, global)
      ? getSkillPath(skill.dirName, target, global)
      : undefined,
    source: skill.sourceType === 'project' ? 'project' : 'bundled',
  }));
}

export function installSkill(skill: SkillDefinition, target: TargetConfig, global: boolean): void {
  const baseDir = global ? getGlobalSkillsDir(target) : getProjectSkillsDir(target);
  const skillDir = join(baseDir, skill.dirName);
  const skillPath = join(skillDir, target.skillFileName);

  mkdirSync(skillDir, { recursive: true });
  writeFileSync(skillPath, skill.content, 'utf-8');

  if ('additionalFiles' in skill && skill.additionalFiles) {
    const additionalFiles: Record<string, Buffer> = skill.additionalFiles;
    for (const [relativePath, content] of Object.entries(additionalFiles)) {
      const fullPath = join(skillDir, relativePath);
      mkdirSync(dirname(fullPath), { recursive: true });
      writeFileSync(fullPath, content);
    }
  }
}

export function uninstallSkill(
  skill: SkillDefinition,
  target: TargetConfig,
  global: boolean,
): boolean {
  const baseDir = global ? getGlobalSkillsDir(target) : getProjectSkillsDir(target);
  const skillDir = join(baseDir, skill.dirName);

  if (!existsSync(skillDir)) {
    return false;
  }

  rmSync(skillDir, { recursive: true, force: true });
  return true;
}

export interface InstallResult {
  installed: number;
  updated: number;
  skipped: number;
  bundledCount: number;
  projectCount: number;
  deprecatedRemoved: number;
}

export function installAllSkills(target: TargetConfig, global: boolean): InstallResult {
  const result: InstallResult = {
    installed: 0,
    updated: 0,
    skipped: 0,
    bundledCount: 0,
    projectCount: 0,
    deprecatedRemoved: 0,
  };

  const baseDir = global ? getGlobalSkillsDir(target) : getProjectSkillsDir(target);
  for (const deprecatedName of DEPRECATED_SKILLS) {
    const deprecatedDir = join(baseDir, deprecatedName);
    if (existsSync(deprecatedDir)) {
      rmSync(deprecatedDir, { recursive: true, force: true });
      result.deprecatedRemoved++;
    }
  }

  const allSkills = collectAllSkills();
  const projectSkills = allSkills.filter((skill) => skill.sourceType === 'project');
  const nonProjectSkills = allSkills.filter((skill) => skill.sourceType !== 'project');

  for (const skill of allSkills) {
    const status = getSkillStatus(skill, target, global);

    if (status === 'not_installed') {
      installSkill(skill, target, global);
      result.installed++;
    } else if (status === 'outdated') {
      installSkill(skill, target, global);
      result.updated++;
    } else {
      result.skipped++;
    }
  }
  result.bundledCount = nonProjectSkills.length;
  result.projectCount = projectSkills.length;

  return result;
}

export interface UninstallResult {
  removed: number;
  notFound: number;
}

export function uninstallAllSkills(target: TargetConfig, global: boolean): UninstallResult {
  const result: UninstallResult = { removed: 0, notFound: 0 };

  const baseDir = global ? getGlobalSkillsDir(target) : getProjectSkillsDir(target);
  for (const deprecatedName of DEPRECATED_SKILLS) {
    const deprecatedDir = join(baseDir, deprecatedName);
    if (existsSync(deprecatedDir)) {
      rmSync(deprecatedDir, { recursive: true, force: true });
      result.removed++;
    }
  }

  const allSkills = collectAllSkills();
  for (const skill of allSkills) {
    if (uninstallSkill(skill, target, global)) {
      result.removed++;
    } else {
      result.notFound++;
    }
  }

  return result;
}

export function getInstallDir(target: TargetConfig, global: boolean): string {
  return global ? getGlobalSkillsDir(target) : getProjectSkillsDir(target);
}

export function getTotalSkillCount(): number {
  return collectAllSkills().length;
}

function collectAllSkills(): SkillDefinition[] {
  const projectRoot = findUnityProjectRoot();
  const packageRoot = projectRoot ? resolvePackageRoot(projectRoot) : null;
  const packageSkills = packageRoot ? collectPackageSkillsFromRoot(packageRoot) : [];
  const cliOnlySkills = collectCliOnlySkills();
  const projectSkills = collectProjectSkills(packageRoot ? [packageRoot] : []);

  return dedupeSkillsByName([packageSkills, cliOnlySkills, projectSkills]);
}

function collectPackageSkillsFromRoot(packageRoot: string): SkillDefinition[] {
  const mcpToolsRoot = join(
    packageRoot,
    SkillsPathConstants.EDITOR_DIR,
    SkillsPathConstants.API_DIR,
    SkillsPathConstants.MCP_TOOLS_DIR,
  );
  if (!existsSync(mcpToolsRoot)) {
    return [];
  }
  const skills: SkillDefinition[] = [];
  scanEditorFolderForSkills(mcpToolsRoot, skills, 'package');
  return skills;
}

function collectCliOnlySkills(): SkillDefinition[] {
  const cliOnlyRoot = resolve(
    __dirname,
    SkillsPathConstants.DIST_PARENT_DIR,
    SkillsPathConstants.SRC_DIR,
    SkillsPathConstants.SKILLS_DIR,
    SkillsPathConstants.CLI_ONLY_DIR,
    SkillsPathConstants.CLI_ONLY_SUBDIR,
  );
  if (!existsSync(cliOnlyRoot)) {
    return [];
  }
  const skills: SkillDefinition[] = [];
  scanEditorFolderForSkills(cliOnlyRoot, skills, 'cli-only', false);
  return skills;
}

function isExcludedFile(fileName: string): boolean {
  if (EXCLUDED_FILES.has(fileName)) {
    return true;
  }
  for (const pattern of EXCLUDED_FILES) {
    if (fileName.endsWith(pattern)) {
      return true;
    }
  }
  return false;
}

function collectSkillFolderFilesRecursive(
  baseDir: string,
  currentDir: string,
  additionalFiles: Record<string, Buffer>,
): void {
  const entries = readdirSync(currentDir, { withFileTypes: true });
  for (const entry of entries) {
    if (isExcludedFile(entry.name)) {
      continue;
    }
    const fullPath = join(currentDir, entry.name);
    const relativePath = fullPath.slice(baseDir.length + 1);

    if (entry.isDirectory()) {
      if (EXCLUDED_DIRS.has(entry.name)) {
        continue;
      }
      collectSkillFolderFilesRecursive(baseDir, fullPath, additionalFiles);
    } else if (entry.isFile()) {
      if (entry.name === SkillsPathConstants.SKILL_FILE) {
        continue;
      }
      // eslint-disable-next-line security/detect-object-injection -- Paths are controlled by package files, not user input.
      additionalFiles[relativePath] = readFileSync(fullPath);
    }
  }
}

function collectSkillFolderFiles(skillDir: string): Record<string, Buffer> | undefined {
  if (!existsSync(skillDir)) {
    return undefined;
  }
  const additionalFiles: Record<string, Buffer> = {};
  collectSkillFolderFilesRecursive(skillDir, skillDir, additionalFiles);
  return Object.keys(additionalFiles).length > 0 ? additionalFiles : undefined;
}

function dedupeSkillsByName(skillGroups: SkillDefinition[][]): SkillDefinition[] {
  const seenNames = new Set<string>();
  const merged: SkillDefinition[] = [];
  for (const group of skillGroups) {
    for (const skill of group) {
      if (seenNames.has(skill.name)) {
        continue;
      }
      seenNames.add(skill.name);
      merged.push(skill);
    }
  }
  return merged;
}

function resolvePackageRoot(projectRoot: string): string | null {
  const candidates: string[] = [];
  candidates.push(join(projectRoot, SkillsPathConstants.PACKAGES_DIR, SkillsPathConstants.SRC_DIR));

  const manifestPaths = resolveManifestPackagePaths(projectRoot);
  for (const manifestPath of manifestPaths) {
    candidates.push(manifestPath);
  }

  for (const packageName of SkillsPathConstants.PACKAGE_NAMES) {
    candidates.push(join(projectRoot, SkillsPathConstants.PACKAGES_DIR, packageName));
  }

  const directRoot = resolveFirstPackageRoot(candidates);
  if (directRoot) {
    return directRoot;
  }

  return resolvePackageCacheRoot(projectRoot);
}

function resolveManifestPackagePaths(projectRoot: string): string[] {
  const manifestPath = join(
    projectRoot,
    SkillsPathConstants.PACKAGES_DIR,
    SkillsPathConstants.MANIFEST_FILE,
  );
  if (!existsSync(manifestPath)) {
    return [];
  }
  const manifestContent = readFileSync(manifestPath, 'utf-8');
  let manifestJson: { dependencies?: Record<string, string> };
  try {
    manifestJson = JSON.parse(manifestContent) as { dependencies?: Record<string, string> };
  } catch (error) {
    // Manifest is user-editable; fail-soft to keep skill installation usable.
    // eslint-disable-next-line no-console -- Warning is required; silent failure would hide manifest issues.
    console.warn('Failed to parse manifest.json; skipping manifest-based path resolution.', error);
    return [];
  }
  const dependencies = manifestJson.dependencies;
  if (!dependencies) {
    return [];
  }
  const resolvedPaths: string[] = [];
  for (const [dependencyName, dependencyValue] of Object.entries(dependencies)) {
    if (!isTargetPackageName(dependencyName)) {
      continue;
    }
    const localPath = resolveLocalDependencyPath(dependencyValue, projectRoot);
    if (localPath) {
      resolvedPaths.push(localPath);
    }
  }
  return resolvedPaths;
}

function resolveLocalDependencyPath(dependencyValue: string, projectRoot: string): string | null {
  if (dependencyValue.startsWith(SkillsPathConstants.FILE_PROTOCOL)) {
    const rawPath = dependencyValue.slice(SkillsPathConstants.FILE_PROTOCOL.length);
    return resolveDependencyPath(rawPath, projectRoot);
  }
  if (dependencyValue.startsWith(SkillsPathConstants.PATH_PROTOCOL)) {
    const rawPath = dependencyValue.slice(SkillsPathConstants.PATH_PROTOCOL.length);
    return resolveDependencyPath(rawPath, projectRoot);
  }
  return null;
}

function resolveDependencyPath(rawPath: string, projectRoot: string): string | null {
  const trimmed = rawPath.trim();
  if (!trimmed) {
    return null;
  }
  let normalizedPath = trimmed;
  if (normalizedPath.startsWith('//')) {
    normalizedPath = normalizedPath.slice(2);
  }
  if (isAbsolute(normalizedPath)) {
    return normalizedPath;
  }
  return resolve(projectRoot, normalizedPath);
}

function resolveFirstPackageRoot(candidates: string[]): string | null {
  for (const candidate of candidates) {
    const resolvedRoot = resolvePackageRootCandidate(candidate);
    if (resolvedRoot) {
      return resolvedRoot;
    }
  }
  return null;
}

function resolvePackageCacheRoot(projectRoot: string): string | null {
  const packageCacheDir = join(
    projectRoot,
    SkillsPathConstants.LIBRARY_DIR,
    SkillsPathConstants.PACKAGE_CACHE_DIR,
  );
  if (!existsSync(packageCacheDir)) {
    return null;
  }
  const entries = readdirSync(packageCacheDir, { withFileTypes: true });
  for (const entry of entries) {
    if (!entry.isDirectory()) {
      continue;
    }
    if (!isTargetPackageCacheDir(entry.name)) {
      continue;
    }
    const candidate = join(packageCacheDir, entry.name);
    const resolvedRoot = resolvePackageRootCandidate(candidate);
    if (resolvedRoot) {
      return resolvedRoot;
    }
  }
  return null;
}

function resolvePackageRootCandidate(candidate: string): string | null {
  if (!existsSync(candidate)) {
    return null;
  }
  const directToolsPath = join(
    candidate,
    SkillsPathConstants.EDITOR_DIR,
    SkillsPathConstants.API_DIR,
    SkillsPathConstants.MCP_TOOLS_DIR,
  );
  if (existsSync(directToolsPath)) {
    return candidate;
  }

  const nestedRoot = join(candidate, SkillsPathConstants.PACKAGES_DIR, SkillsPathConstants.SRC_DIR);
  const nestedToolsPath = join(
    nestedRoot,
    SkillsPathConstants.EDITOR_DIR,
    SkillsPathConstants.API_DIR,
    SkillsPathConstants.MCP_TOOLS_DIR,
  );
  if (existsSync(nestedToolsPath)) {
    return nestedRoot;
  }
  return null;
}

function isTargetPackageName(name: string): boolean {
  const normalized = name.toLowerCase();
  return SkillsPathConstants.PACKAGE_NAMES.some(
    (packageName) => packageName.toLowerCase() === normalized,
  );
}

function isTargetPackageCacheDir(dirName: string): boolean {
  const normalized = dirName.toLowerCase();
  return SkillsPathConstants.PACKAGE_NAMES.some((packageName) =>
    normalized.startsWith(`${packageName.toLowerCase()}@`),
  );
}

function isUnderExcludedRoots(targetPath: string, excludedRoots: string[]): boolean {
  for (const root of excludedRoots) {
    if (isPathUnder(targetPath, root)) {
      return true;
    }
  }
  return false;
}

function isPathUnder(childPath: string, parentPath: string): boolean {
  const resolvedChild = resolve(childPath);
  const resolvedParent = resolve(parentPath);
  if (resolvedChild === resolvedParent) {
    return true;
  }
  return resolvedChild.startsWith(resolvedParent + sep);
}
