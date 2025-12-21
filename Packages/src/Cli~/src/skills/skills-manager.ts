/**
 * Skills manager for installing/uninstalling/listing uloop skills.
 */

// File paths are constructed from home directory and skill names, not from untrusted user input
/* eslint-disable security/detect-non-literal-fs-filename */

import { existsSync, mkdirSync, readFileSync, writeFileSync, rmSync } from 'fs';
import { join } from 'path';
import { homedir } from 'os';
import { BUNDLED_SKILLS, BundledSkill } from './bundled-skills.js';
import { TargetConfig } from './target-config.js';

export type SkillStatus = 'installed' | 'not_installed' | 'outdated';

export interface SkillInfo {
  name: string;
  status: SkillStatus;
  path?: string;
}

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

function isSkillInstalled(skill: BundledSkill, target: TargetConfig, global: boolean): boolean {
  const skillPath = getSkillPath(skill.dirName, target, global);
  return existsSync(skillPath);
}

function isSkillOutdated(skill: BundledSkill, target: TargetConfig, global: boolean): boolean {
  const skillPath = getSkillPath(skill.dirName, target, global);
  if (!existsSync(skillPath)) {
    return false;
  }
  const installedContent = readFileSync(skillPath, 'utf-8');
  return installedContent !== skill.content;
}

export function getSkillStatus(
  skill: BundledSkill,
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

export function getAllSkillStatuses(target: TargetConfig, global: boolean): SkillInfo[] {
  return BUNDLED_SKILLS.map((skill) => ({
    name: skill.name,
    status: getSkillStatus(skill, target, global),
    path: isSkillInstalled(skill, target, global)
      ? getSkillPath(skill.dirName, target, global)
      : undefined,
  }));
}

export function installSkill(skill: BundledSkill, target: TargetConfig, global: boolean): void {
  const baseDir = global ? getGlobalSkillsDir(target) : getProjectSkillsDir(target);
  const skillDir = join(baseDir, skill.dirName);
  const skillPath = join(skillDir, target.skillFileName);

  mkdirSync(skillDir, { recursive: true });
  writeFileSync(skillPath, skill.content, 'utf-8');
}

export function uninstallSkill(
  skill: BundledSkill,
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
}

export function installAllSkills(target: TargetConfig, global: boolean): InstallResult {
  const result: InstallResult = { installed: 0, updated: 0, skipped: 0 };

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

  return result;
}

export function getInstallDir(target: TargetConfig, global: boolean): string {
  return global ? getGlobalSkillsDir(target) : getProjectSkillsDir(target);
}

export function getTotalSkillCount(): number {
  return BUNDLED_SKILLS.length;
}
