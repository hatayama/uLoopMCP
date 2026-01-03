/**
 * Skills manager for installing/uninstalling/listing uloop skills.
 * Supports both bundled skills and project-local skills.
 */

// File paths are constructed from home directory and skill names, not from untrusted user input
/* eslint-disable security/detect-non-literal-fs-filename */

import { existsSync, mkdirSync, readFileSync, writeFileSync, rmSync, readdirSync } from 'fs';
import { join, dirname } from 'path';
import { homedir } from 'os';
import { BUNDLED_SKILLS, BundledSkill } from './bundled-skills.js';
import { TargetConfig } from './target-config.js';

export type SkillStatus = 'installed' | 'not_installed' | 'outdated';

export interface SkillInfo {
  name: string;
  status: SkillStatus;
  path?: string;
  source?: 'bundled' | 'project';
}

export interface ProjectSkill {
  name: string;
  dirName: string;
  content: string;
  sourcePath: string;
}

const EXCLUDED_DIRS = new Set(['node_modules', '.git', 'Temp', 'obj', 'Build', 'Builds', 'Logs']);

function getGlobalSkillsDir(target: TargetConfig): string {
  return join(homedir(), target.projectDir, 'skills');
}

function getProjectSkillsDir(target: TargetConfig): string {
  return join(process.cwd(), target.projectDir, 'skills');
}

function getSkillPath(skillDirName: string, target: TargetConfig, global: boolean): string {
  const baseDir = global ? getGlobalSkillsDir(target) : getProjectSkillsDir(target);
  return join(baseDir, skillDirName, target.skillFileName);
}

function isSkillInstalled(
  skill: BundledSkill | ProjectSkill,
  target: TargetConfig,
  global: boolean,
): boolean {
  const skillPath = getSkillPath(skill.dirName, target, global);
  return existsSync(skillPath);
}

function isSkillOutdated(
  skill: BundledSkill | ProjectSkill,
  target: TargetConfig,
  global: boolean,
): boolean {
  const skillPath = getSkillPath(skill.dirName, target, global);
  if (!existsSync(skillPath)) {
    return false;
  }
  const installedContent = readFileSync(skillPath, 'utf-8');
  return installedContent !== skill.content;
}

export function getSkillStatus(
  skill: BundledSkill | ProjectSkill,
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

  const frontmatter: Record<string, string | boolean> = {};
  const lines = frontmatterMatch[1].split('\n');

  for (const line of lines) {
    const colonIndex = line.indexOf(':');
    if (colonIndex === -1) {
      continue;
    }

    const key = line.slice(0, colonIndex).trim();
    const value = line.slice(colonIndex + 1).trim();

    if (value === 'true') {
      frontmatter[key] = true;
    } else if (value === 'false') {
      frontmatter[key] = false;
    } else {
      frontmatter[key] = value;
    }
  }

  return frontmatter;
}

function scanEditorFolderForSkills(editorPath: string, skills: ProjectSkill[]): void {
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
      const skillMdPath = join(fullPath, 'SKILL.md');
      if (existsSync(skillMdPath)) {
        const content = readFileSync(skillMdPath, 'utf-8');
        const frontmatter = parseFrontmatter(content);

        if (frontmatter.internal === true) {
          continue;
        }

        const name = typeof frontmatter.name === 'string' ? frontmatter.name : entry.name;

        skills.push({
          name,
          dirName: name,
          content,
          sourcePath: skillMdPath,
        });
      }

      scanEditorFolderForSkills(fullPath, skills);
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

export function collectProjectSkills(): ProjectSkill[] {
  const projectRoot = process.cwd();
  const skills: ProjectSkill[] = [];
  const seenNames = new Set<string>();

  const searchPaths = [
    join(projectRoot, 'Assets'),
    join(projectRoot, 'Packages'),
    join(projectRoot, 'Library', 'PackageCache'),
  ];

  for (const searchPath of searchPaths) {
    if (!existsSync(searchPath)) {
      continue;
    }

    const editorFolders = findEditorFolders(searchPath, 3);

    for (const editorFolder of editorFolders) {
      scanEditorFolderForSkills(editorFolder, skills);
    }
  }

  const uniqueSkills: ProjectSkill[] = [];
  for (const skill of skills) {
    if (!seenNames.has(skill.name)) {
      seenNames.add(skill.name);
      uniqueSkills.push(skill);
    }
  }

  return uniqueSkills;
}

export function getAllSkillStatuses(target: TargetConfig, global: boolean): SkillInfo[] {
  const bundledStatuses: SkillInfo[] = BUNDLED_SKILLS.map((skill) => ({
    name: skill.name,
    status: getSkillStatus(skill, target, global),
    path: isSkillInstalled(skill, target, global)
      ? getSkillPath(skill.dirName, target, global)
      : undefined,
    source: 'bundled' as const,
  }));

  const projectSkills = collectProjectSkills();
  const bundledNames = new Set(BUNDLED_SKILLS.map((s) => s.name));

  const projectStatuses: SkillInfo[] = projectSkills
    .filter((skill) => !bundledNames.has(skill.name))
    .map((skill) => ({
      name: skill.name,
      status: getSkillStatus(skill, target, global),
      path: isSkillInstalled(skill, target, global)
        ? getSkillPath(skill.dirName, target, global)
        : undefined,
      source: 'project' as const,
    }));

  return [...bundledStatuses, ...projectStatuses];
}

export function installSkill(
  skill: BundledSkill | ProjectSkill,
  target: TargetConfig,
  global: boolean,
): void {
  const baseDir = global ? getGlobalSkillsDir(target) : getProjectSkillsDir(target);
  const skillDir = join(baseDir, skill.dirName);
  const skillPath = join(skillDir, target.skillFileName);

  mkdirSync(skillDir, { recursive: true });
  writeFileSync(skillPath, skill.content, 'utf-8');

  if ('additionalFiles' in skill && skill.additionalFiles) {
    const additionalFiles: Record<string, string> = skill.additionalFiles;
    for (const [relativePath, content] of Object.entries(additionalFiles)) {
      const fullPath = join(skillDir, relativePath);
      mkdirSync(dirname(fullPath), { recursive: true });
      writeFileSync(fullPath, content, 'utf-8');
    }
  }
}

export function uninstallSkill(
  skill: BundledSkill | ProjectSkill,
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
}

export function installAllSkills(target: TargetConfig, global: boolean): InstallResult {
  const result: InstallResult = {
    installed: 0,
    updated: 0,
    skipped: 0,
    bundledCount: 0,
    projectCount: 0,
  };

  for (const skill of BUNDLED_SKILLS) {
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
  result.bundledCount = BUNDLED_SKILLS.length;

  const projectSkills = collectProjectSkills();
  const bundledNames = new Set(BUNDLED_SKILLS.map((s) => s.name));

  for (const skill of projectSkills) {
    if (bundledNames.has(skill.name)) {
      continue;
    }

    const status = getSkillStatus(skill, target, global);

    if (status === 'not_installed') {
      installSkill(skill, target, global);
      result.installed++;
      result.projectCount++;
    } else if (status === 'outdated') {
      installSkill(skill, target, global);
      result.updated++;
      result.projectCount++;
    } else {
      result.skipped++;
      result.projectCount++;
    }
  }

  return result;
}

export interface UninstallResult {
  removed: number;
  notFound: number;
}

export function uninstallAllSkills(target: TargetConfig, global: boolean): UninstallResult {
  const result: UninstallResult = { removed: 0, notFound: 0 };

  for (const skill of BUNDLED_SKILLS) {
    if (uninstallSkill(skill, target, global)) {
      result.removed++;
    } else {
      result.notFound++;
    }
  }

  const projectSkills = collectProjectSkills();
  const bundledNames = new Set(BUNDLED_SKILLS.map((s) => s.name));

  for (const skill of projectSkills) {
    if (bundledNames.has(skill.name)) {
      continue;
    }

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
  const projectSkills = collectProjectSkills();
  const bundledNames = new Set(BUNDLED_SKILLS.map((s) => s.name));
  const uniqueProjectCount = projectSkills.filter((s) => !bundledNames.has(s.name)).length;
  return BUNDLED_SKILLS.length + uniqueProjectCount;
}
