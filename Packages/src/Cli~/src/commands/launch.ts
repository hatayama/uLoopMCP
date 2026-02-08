/**
 * CLI command for launching Unity projects.
 * Delegates to launch-unity's orchestrateLaunch() for all orchestration logic.
 */

// CLI commands output to console by design
/* eslint-disable no-console */

import { Command } from 'commander';
import { resolve } from 'path';

import { orchestrateLaunch } from 'launch-unity';

interface LaunchCommandOptions {
  restart?: boolean;
  quit?: boolean;
  platform?: string;
  maxDepth?: string;
  addUnityHub?: boolean;
  favorite?: boolean;
}

export function registerLaunchCommand(program: Command): void {
  program
    .command('launch')
    .description('Launch Unity project with matching Editor version')
    .argument('[project-path]', 'Path to Unity project')
    .option('-r, --restart', 'Kill running Unity and restart')
    .option('-q, --quit', 'Gracefully quit running Unity')
    .option('-p, --platform <platform>', 'Build target (e.g., Android, iOS)')
    .option('--max-depth <n>', 'Search depth when project-path is omitted', '3')
    .option('-a, --add-unity-hub', 'Add to Unity Hub (does not launch)')
    .option('-f, --favorite', 'Add to Unity Hub as favorite (does not launch)')
    .action(async (projectPath: string | undefined, options: LaunchCommandOptions) => {
      await runLaunchCommand(projectPath, options);
    });
}

function parseMaxDepth(value: string | undefined): number {
  if (value === undefined) {
    return 3;
  }
  const parsed: number = parseInt(value, 10);
  if (Number.isNaN(parsed)) {
    console.error(`Error: Invalid --max-depth value: "${value}". Must be an integer.`);
    process.exit(1);
  }
  return parsed;
}

async function runLaunchCommand(
  projectPath: string | undefined,
  options: LaunchCommandOptions,
): Promise<void> {
  const maxDepth: number = parseMaxDepth(options.maxDepth);

  await orchestrateLaunch({
    projectPath: projectPath ? resolve(projectPath) : undefined,
    searchRoot: process.cwd(),
    searchMaxDepth: maxDepth,
    platform: options.platform,
    unityArgs: [],
    restart: options.restart === true,
    quit: options.quit === true,
    addUnityHub: options.addUnityHub === true,
    favoriteUnityHub: options.favorite === true,
  });
}
