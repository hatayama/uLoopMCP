/**
 * Skills manager for installing/uninstalling/listing uloop skills.
 */

import { existsSync, mkdirSync, readFileSync, writeFileSync, rmSync } from 'fs';
import { join } from 'path';
import { homedir } from 'os';
import { BUNDLED_SKILLS, BundledSkill } from './bundled-skills.js';

export type SkillStatus = 'installed' | 'not_installed' | 'outdated';

export interface SkillInfo {
  name: string;
  status: SkillStatus;
  path?: string;
}

function getGlobalSkillsDir(): string {
  return join(homedir(), '.claude', 'skills');
}

function getProjectSkillsDir(): string {
  return join(process.cwd(), '.claude', 'skills');
}

function getSkillPath(skillDirName: string, global: boolean): string {
  const baseDir = global ? getGlobalSkillsDir() : getProjectSkillsDir();
  return join(baseDir, skillDirName, 'SKILL.md');
}

function isSkillInstalled(skill: BundledSkill, global: boolean): boolean {
  const skillPath = getSkillPath(skill.dirName, global);
  return existsSync(skillPath);
}

function isSkillOutdated(skill: BundledSkill, global: boolean): boolean {
  const skillPath = getSkillPath(skill.dirName, global);
  if (!existsSync(skillPath)) {
    return false;
  }
  const installedContent = readFileSync(skillPath, 'utf-8');
  return installedContent !== skill.content;
}

export function getSkillStatus(skill: BundledSkill, global: boolean): SkillStatus {
  if (!isSkillInstalled(skill, global)) {
    return 'not_installed';
  }
  if (isSkillOutdated(skill, global)) {
    return 'outdated';
  }
  return 'installed';
}

export function getAllSkillStatuses(global: boolean): SkillInfo[] {
  return BUNDLED_SKILLS.map((skill) => ({
    name: skill.name,
    status: getSkillStatus(skill, global),
    path: isSkillInstalled(skill, global) ? getSkillPath(skill.dirName, global) : undefined,
  }));
}

export function installSkill(skill: BundledSkill, global: boolean): void {
  const baseDir = global ? getGlobalSkillsDir() : getProjectSkillsDir();
  const skillDir = join(baseDir, skill.dirName);
  const skillPath = join(skillDir, 'SKILL.md');

  mkdirSync(skillDir, { recursive: true });
  writeFileSync(skillPath, skill.content, 'utf-8');
}

export function uninstallSkill(skill: BundledSkill, global: boolean): boolean {
  const baseDir = global ? getGlobalSkillsDir() : getProjectSkillsDir();
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
}

export function installAllSkills(global: boolean): InstallResult {
  const result: InstallResult = { installed: 0, updated: 0, skipped: 0 };

  for (const skill of BUNDLED_SKILLS) {
    const status = getSkillStatus(skill, global);

    if (status === 'not_installed') {
      installSkill(skill, global);
      result.installed++;
    } else if (status === 'outdated') {
      installSkill(skill, global);
      result.updated++;
    } else {
      result.skipped++;
    }
  }

  return result;
}

export interface UninstallResult {
  removed: number;
  notFound: number;
}

export function uninstallAllSkills(global: boolean): UninstallResult {
  const result: UninstallResult = { removed: 0, notFound: 0 };

  for (const skill of BUNDLED_SKILLS) {
    if (uninstallSkill(skill, global)) {
      result.removed++;
    } else {
      result.notFound++;
    }
  }

  return result;
}

export function getInstallDir(global: boolean): string {
  return global ? getGlobalSkillsDir() : getProjectSkillsDir();
}

export function getTotalSkillCount(): number {
  return BUNDLED_SKILLS.length;
}
