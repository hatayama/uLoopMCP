// Dispatcher output and process spawning are the public CLI behavior.
/* eslint-disable security/detect-non-literal-fs-filename */

import assert from 'node:assert';
import { spawn, type SpawnOptions } from 'child_process';
import { existsSync } from 'fs';
import { basename, dirname, join, resolve } from 'path';
import { VERSION } from './version.js';

const PROJECT_LOCAL_CLI_RELATIVE_PATH = join('.uloop', 'bin', 'uloop');
const WINDOWS_PROJECT_LOCAL_CLI_RELATIVE_PATH = `${PROJECT_LOCAL_CLI_RELATIVE_PATH}.cmd`;
const VERSION_ARGS = new Set(['--version', '-v']);

type OutputWriter = {
  write(chunk: string): boolean;
};

export type DispatcherChildProcess = {
  on(event: 'exit', listener: (code: number | null, signal: NodeJS.Signals | null) => void): void;
  on(event: 'error', listener: (error: Error) => void): void;
};

export type DispatcherSpawnFn = (
  command: string,
  args: readonly string[],
  options: SpawnOptions & { cwd: string },
) => DispatcherChildProcess;

export type DispatcherDependencies = {
  readonly args: readonly string[];
  readonly cwd: string;
  readonly platform: NodeJS.Platform;
  readonly stdout: OutputWriter;
  readonly stderr: OutputWriter;
  readonly spawnFn: DispatcherSpawnFn;
};

type ProjectPathArgument =
  | { readonly value: string; readonly error: null }
  | { readonly value: null; readonly error: string | null };

type ProjectRootResolution =
  | { readonly projectRoot: string; readonly error: null }
  | { readonly projectRoot: null; readonly error: string };

function createDefaultDependencies(): DispatcherDependencies {
  return {
    args: process.argv.slice(2),
    cwd: process.cwd(),
    platform: process.platform,
    stdout: process.stdout,
    stderr: process.stderr,
    spawnFn: spawn as DispatcherSpawnFn,
  };
}

function isVersionRequest(args: readonly string[]): boolean {
  return args.length === 1 && VERSION_ARGS.has(args[0]);
}

function findProjectPathArgument(args: readonly string[]): ProjectPathArgument {
  for (let index = 0; index < args.length; index++) {
    const arg = args[index];

    if (arg.startsWith('--project-path=')) {
      const value = arg.slice('--project-path='.length);
      return value.length === 0
        ? { value: null, error: '--project-path requires a value.' }
        : { value, error: null };
    }

    if (arg !== '--project-path') {
      continue;
    }

    const value = args[index + 1];
    if (value === undefined || value.startsWith('-')) {
      return { value: null, error: '--project-path requires a value.' };
    }

    return { value, error: null };
  }

  return { value: null, error: null };
}

function isUnityProject(projectRoot: string): boolean {
  return (
    existsSync(join(projectRoot, 'Assets')) && existsSync(join(projectRoot, 'ProjectSettings'))
  );
}

function findUnityProjectInParents(startPath: string): string | null {
  let currentPath = resolve(startPath);

  while (true) {
    if (isUnityProject(currentPath)) {
      return currentPath;
    }

    if (existsSync(join(currentPath, '.git'))) {
      return null;
    }

    const parentPath = dirname(currentPath);
    if (parentPath === currentPath) {
      return null;
    }

    currentPath = parentPath;
  }
}

function resolveProjectRoot(args: readonly string[], cwd: string): ProjectRootResolution {
  assert(cwd.length > 0, 'cwd must not be empty');

  const projectPathArgument = findProjectPathArgument(args);
  if (projectPathArgument.error !== null) {
    return { projectRoot: null, error: projectPathArgument.error };
  }

  if (projectPathArgument.value !== null) {
    const explicitProjectRoot = resolve(cwd, projectPathArgument.value);
    if (!isUnityProject(explicitProjectRoot)) {
      return {
        projectRoot: null,
        error: `--project-path does not point to a Unity project: ${explicitProjectRoot}`,
      };
    }

    return { projectRoot: explicitProjectRoot, error: null };
  }

  const discoveredProjectRoot = findUnityProjectInParents(cwd);
  if (discoveredProjectRoot === null) {
    return {
      projectRoot: null,
      error:
        'Could not find a Unity project. Run uloop from inside a Unity project or pass --project-path.',
    };
  }

  return { projectRoot: discoveredProjectRoot, error: null };
}

function getProjectLocalCliCandidatePaths(
  projectRoot: string,
  platform: NodeJS.Platform,
): string[] {
  const unixPath = join(projectRoot, PROJECT_LOCAL_CLI_RELATIVE_PATH);
  if (platform !== 'win32') {
    return [unixPath];
  }

  return [join(projectRoot, WINDOWS_PROJECT_LOCAL_CLI_RELATIVE_PATH), unixPath];
}

function findProjectLocalCliPath(projectRoot: string, platform: NodeJS.Platform): string | null {
  const candidatePaths = getProjectLocalCliCandidatePaths(projectRoot, platform);
  return candidatePaths.find((candidatePath) => existsSync(candidatePath)) ?? null;
}

async function runProjectLocalCli(
  localCliPath: string,
  args: readonly string[],
  projectRoot: string,
  dependencies: DispatcherDependencies,
): Promise<number> {
  return new Promise((resolveExitCode) => {
    const child = dependencies.spawnFn(localCliPath, args, {
      cwd: projectRoot,
      stdio: 'inherit',
    });

    child.on('exit', (code) => {
      resolveExitCode(code ?? 1);
    });

    child.on('error', (error) => {
      dependencies.stderr.write(`Failed to start project-local uloop CLI: ${error.message}\n`);
      resolveExitCode(1);
    });
  });
}

export async function runDispatcher(
  dependencies: DispatcherDependencies = createDefaultDependencies(),
): Promise<number> {
  if (isVersionRequest(dependencies.args)) {
    dependencies.stdout.write(`${VERSION}\n`);
    return 0;
  }

  const projectResolution = resolveProjectRoot(dependencies.args, dependencies.cwd);
  if (projectResolution.error !== null) {
    dependencies.stderr.write(`${projectResolution.error}\n`);
    return 1;
  }

  const localCliPath = findProjectLocalCliPath(
    projectResolution.projectRoot,
    dependencies.platform,
  );
  if (localCliPath === null) {
    dependencies.stderr.write(
      `Project-local uloop CLI was not found at ${PROJECT_LOCAL_CLI_RELATIVE_PATH}.\n` +
        'Open Unity CLI Loop setup in this project and refresh the CLI installation.\n',
    );
    return 1;
  }

  return runProjectLocalCli(
    localCliPath,
    dependencies.args,
    projectResolution.projectRoot,
    dependencies,
  );
}

function shouldRunDispatcherEntryPoint(): boolean {
  if (process.env.JEST_WORKER_ID === undefined) {
    return true;
  }

  return basename(process.argv[1] ?? '') === 'dispatcher.bundle.cjs';
}

if (shouldRunDispatcherEntryPoint()) {
  void (async (): Promise<void> => {
    const exitCode = await runDispatcher();
    process.exit(exitCode);
  })();
}
