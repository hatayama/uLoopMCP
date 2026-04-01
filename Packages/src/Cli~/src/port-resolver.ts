/**
 * Port resolution utility for CLI.
 * Resolves Unity server port from various sources.
 */

// File paths are constructed from Unity project root detection, not from user input
/* eslint-disable security/detect-non-literal-fs-filename */

import { PRODUCT_DISPLAY_NAME } from './cli-constants';
import { readFile } from 'fs/promises';
import { existsSync } from 'fs';
import { join, resolve } from 'path';
import { findUnityProjectRoot, isUnityProject, hasUloopInstalled } from './project-root.js';

export class UnityNotRunningError extends Error {
  constructor(public readonly projectRoot: string) {
    super('UNITY_NOT_RUNNING');
  }
}

interface UnityMcpSettings {
  isServerRunning?: boolean;
  customPort?: number;
}

function normalizePort(port: unknown): number | null {
  if (typeof port !== 'number') {
    return null;
  }

  if (!Number.isInteger(port)) {
    return null;
  }

  if (port < 1 || port > 65535) {
    return null;
  }

  return port;
}

export function resolvePortFromUnitySettings(settings: UnityMcpSettings): number | null {
  const customPort = normalizePort(settings.customPort);

  if (customPort !== null) {
    return customPort;
  }

  return null;
}

export function validateProjectPath(projectPath: string): string {
  const resolved = resolve(projectPath);

  if (!existsSync(resolved)) {
    throw new Error(`Path does not exist: ${resolved}`);
  }

  if (!isUnityProject(resolved)) {
    throw new Error(`Not a Unity project (Assets/ or ProjectSettings/ not found): ${resolved}`);
  }

  if (!hasUloopInstalled(resolved)) {
    throw new Error(
      `${PRODUCT_DISPLAY_NAME} is not installed in this project (UserSettings/UnityMcpSettings.json not found): ${resolved}`,
    );
  }

  return resolved;
}

export async function resolveUnityPort(
  explicitPort?: number,
  projectPath?: string,
): Promise<number> {
  if (explicitPort !== undefined && projectPath !== undefined) {
    throw new Error('Cannot specify both --port and --project-path. Use one or the other.');
  }

  if (explicitPort !== undefined) {
    return explicitPort;
  }

  if (projectPath !== undefined) {
    const resolved = validateProjectPath(projectPath);
    return await readPortFromSettingsOrThrow(resolved);
  }

  const projectRoot = findUnityProjectRoot();
  if (projectRoot === null) {
    throw new Error('Unity project not found. Use --project-path option to specify the target.');
  }

  return await readPortFromSettingsOrThrow(projectRoot);
}

function createSettingsReadError(projectRoot: string): Error {
  const settingsPath = join(projectRoot, 'UserSettings/UnityMcpSettings.json');
  return new Error(
    `Could not read Unity server port from settings.\n\n` +
      `  Settings file: ${settingsPath}\n\n` +
      `Run 'uloop launch -r' to restart Unity.`,
  );
}

// File I/O and JSON parsing can fail for external reasons (permissions, corruption, concurrent writes)
async function readPortFromSettingsOrThrow(projectRoot: string): Promise<number> {
  const settingsPath = join(projectRoot, 'UserSettings/UnityMcpSettings.json');

  if (!existsSync(settingsPath)) {
    throw createSettingsReadError(projectRoot);
  }

  let content: string;
  try {
    content = await readFile(settingsPath, 'utf-8');
  } catch {
    throw createSettingsReadError(projectRoot);
  }

  let parsed: unknown;
  try {
    parsed = JSON.parse(content);
  } catch {
    throw createSettingsReadError(projectRoot);
  }

  if (typeof parsed !== 'object' || parsed === null) {
    throw createSettingsReadError(projectRoot);
  }
  const settings = parsed as UnityMcpSettings;

  // Only block when isServerRunning is explicitly false (Unity clean shutdown).
  // undefined/missing means old settings format — proceed to next validation stage.
  if (settings.isServerRunning === false) {
    throw new UnityNotRunningError(projectRoot);
  }

  const port = resolvePortFromUnitySettings(settings);
  if (port === null) {
    throw createSettingsReadError(projectRoot);
  }

  return port;
}
