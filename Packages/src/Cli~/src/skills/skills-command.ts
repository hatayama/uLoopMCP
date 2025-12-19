/**
 * CLI command definitions for skills management.
 */

import { Command } from 'commander';
import {
  getAllSkillStatuses,
  installAllSkills,
  uninstallAllSkills,
  getInstallDir,
  getTotalSkillCount,
} from './skills-manager.js';

export function registerSkillsCommand(program: Command): void {
  const skillsCmd = program.command('skills').description('Manage uloop skills for Claude Code');

  skillsCmd
    .command('list')
    .description('List all uloop skills and their installation status')
    .option('-g, --global', 'Check global installation (~/.claude/skills/)')
    .action((options: { global?: boolean }) => {
      listSkills(options.global ?? false);
    });

  skillsCmd
    .command('install')
    .description('Install all uloop skills')
    .option('-g, --global', 'Install to global location (~/.claude/skills/)')
    .action((options: { global?: boolean }) => {
      installSkills(options.global ?? false);
    });

  skillsCmd
    .command('uninstall')
    .description('Uninstall all uloop skills')
    .option('-g, --global', 'Uninstall from global location (~/.claude/skills/)')
    .action((options: { global?: boolean }) => {
      uninstallSkills(options.global ?? false);
    });
}

function listSkills(global: boolean): void {
  const location = global ? 'Global' : 'Project';
  const dir = getInstallDir(global);

  console.log(`\nuloop Skills Status (${location}):`);
  console.log(`Location: ${dir}`);
  console.log('='.repeat(50));
  console.log('');

  const statuses = getAllSkillStatuses(global);

  for (const skill of statuses) {
    const icon = getStatusIcon(skill.status);
    const statusText = getStatusText(skill.status);
    console.log(`  ${icon} ${skill.name} (${statusText})`);
  }

  console.log('');
  console.log(`Total: ${getTotalSkillCount()} bundled skills`);
}

function getStatusIcon(status: string): string {
  switch (status) {
    case 'installed':
      return '\x1b[32m✓\x1b[0m';
    case 'outdated':
      return '\x1b[33m↑\x1b[0m';
    case 'not_installed':
      return '\x1b[31m✗\x1b[0m';
    default:
      return '?';
  }
}

function getStatusText(status: string): string {
  switch (status) {
    case 'installed':
      return 'installed';
    case 'outdated':
      return 'outdated';
    case 'not_installed':
      return 'not installed';
    default:
      return 'unknown';
  }
}

function installSkills(global: boolean): void {
  const location = global ? 'global' : 'project';
  const dir = getInstallDir(global);

  console.log(`\nInstalling uloop skills (${location})...`);
  console.log('');

  const result = installAllSkills(global);

  console.log(`\x1b[32m✓\x1b[0m Installed: ${result.installed}`);
  console.log(`\x1b[33m↑\x1b[0m Updated: ${result.updated}`);
  console.log(`\x1b[90m-\x1b[0m Skipped (up-to-date): ${result.skipped}`);
  console.log('');
  console.log(`Skills installed to ${dir}`);
}

function uninstallSkills(global: boolean): void {
  const location = global ? 'global' : 'project';
  const dir = getInstallDir(global);

  console.log(`\nUninstalling uloop skills (${location})...`);
  console.log('');

  const result = uninstallAllSkills(global);

  console.log(`\x1b[31m✗\x1b[0m Removed: ${result.removed}`);
  console.log(`\x1b[90m-\x1b[0m Not found: ${result.notFound}`);
  console.log('');
  console.log(`Skills removed from ${dir}`);
}
