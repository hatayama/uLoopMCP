/**
 * CLI command for launching Unity projects.
 * Integrates launch-unity library into uloop CLI.
 */

// CLI commands output to console by design
/* eslint-disable no-console */

import { Command } from 'commander';
import { resolve } from 'path';

import {
  findUnityProjectBfs,
  getUnityVersion,
  launch,
  findRunningUnityProcess,
  focusUnityProcess,
  killRunningUnity,
  handleStaleLockfile,
  ensureProjectEntryAndUpdate,
  updateLastModifiedIfExists,
  LaunchResolvedOptions,
} from 'launch-unity';

interface LaunchCommandOptions {
  restart?: boolean;
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
  const parsed = parseInt(value, 10);
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
  const maxDepth = parseMaxDepth(options.maxDepth);

  let resolvedProjectPath: string | undefined = projectPath ? resolve(projectPath) : undefined;

  if (!resolvedProjectPath) {
    const searchRoot = process.cwd();
    const depthInfo = maxDepth === -1 ? 'unlimited' : String(maxDepth);
    console.log(
      `No project-path provided. Searching under ${searchRoot} (max-depth: ${depthInfo})...`,
    );
    const found = findUnityProjectBfs(searchRoot, maxDepth);
    if (!found) {
      console.error(`Error: Unity project not found under ${searchRoot}.`);
      process.exit(1);
    }
    console.log(`Selected project: ${found}`);
    resolvedProjectPath = found;
  }

  const unityVersion = getUnityVersion(resolvedProjectPath);

  const unityHubOnlyMode = options.addUnityHub === true || options.favorite === true;
  if (unityHubOnlyMode) {
    console.log(`Detected Unity version: ${unityVersion}`);
    console.log(`Project Path: ${resolvedProjectPath}`);
    const now = new Date();
    await ensureProjectEntryAndUpdate(
      resolvedProjectPath,
      unityVersion,
      now,
      options.favorite === true,
    );
    console.log('Unity Hub entry updated.');
    return;
  }

  if (options.restart === true) {
    await killRunningUnity(resolvedProjectPath);
  } else {
    const runningProcess = await findRunningUnityProcess(resolvedProjectPath);
    if (runningProcess) {
      console.log(
        `Unity process already running for project: ${resolvedProjectPath} (PID: ${runningProcess.pid})`,
      );
      await focusUnityProcess(runningProcess.pid);
      return;
    }
  }

  await handleStaleLockfile(resolvedProjectPath);

  const resolved: LaunchResolvedOptions = {
    projectPath: resolvedProjectPath,
    platform: options.platform,
    unityArgs: [],
    unityVersion,
  };
  launch(resolved);

  const now = new Date();
  await updateLastModifiedIfExists(resolvedProjectPath, now);
}
