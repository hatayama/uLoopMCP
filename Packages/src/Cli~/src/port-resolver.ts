/**
 * Port resolution utility for CLI.
 * Resolves Unity server port from various sources.
 */

// File paths are constructed from Unity project root detection, not from user input
/* eslint-disable security/detect-non-literal-fs-filename */

import { readFile } from 'fs/promises';
import { existsSync } from 'fs';
import { join, resolve } from 'path';
import { findUnityProjectRoot, isUnityProject, hasUloopInstalled } from './project-root.js';

const DEFAULT_PORT = 8700;

interface UnityMcpSettings {
  isServerRunning?: boolean;
  serverPort?: number;
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
  const serverPort = normalizePort(settings.serverPort);
  const customPort = normalizePort(settings.customPort);

  if (settings.isServerRunning === true && serverPort !== null) {
    return serverPort;
  }

  if (customPort !== null) {
    return customPort;
  }

  if (serverPort !== null) {
    return serverPort;
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
      `uLoopMCP is not installed in this project (UserSettings/UnityMcpSettings.json not found): ${resolved}`,
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
    const settingsPort = await readPortFromSettings(resolved);
    if (settingsPort !== null) {
      return settingsPort;
    }
    return DEFAULT_PORT;
  }

  const projectRoot = findUnityProjectRoot();
  if (projectRoot === null) {
    throw new Error(
      'Unity project not found. Use --port or --project-path option to specify the target.',
    );
  }

  const settingsPort = await readPortFromSettings(projectRoot);
  if (settingsPort !== null) {
    return settingsPort;
  }

  return DEFAULT_PORT;
}

async function readPortFromSettings(projectRoot: string): Promise<number | null> {
  const settingsPath = join(projectRoot, 'UserSettings/UnityMcpSettings.json');

  if (!existsSync(settingsPath)) {
    return null;
  }

  let content: string;
  try {
    content = await readFile(settingsPath, 'utf-8');
  } catch {
    return null;
  }

  let settings: UnityMcpSettings;
  try {
    settings = JSON.parse(content) as UnityMcpSettings;
  } catch {
    return null;
  }

  return resolvePortFromUnitySettings(settings);
}
