/**
 * Port resolution utility for CLI.
 * Resolves Unity server port from various sources.
 */

import { readFile } from 'fs/promises';
import { join } from 'path';
import { findUnityProjectRoot } from './project-root.js';

const DEFAULT_PORT = 8700;

interface UnityMcpSettings {
  serverPort?: number;
  customPort?: number;
}

export async function resolveUnityPort(explicitPort?: number): Promise<number> {
  if (explicitPort !== undefined) {
    return explicitPort;
  }

  const settingsPort = await readPortFromSettings();
  if (settingsPort !== null) {
    return settingsPort;
  }

  return DEFAULT_PORT;
}

async function readPortFromSettings(): Promise<number | null> {
  const projectRoot = findUnityProjectRoot();
  if (projectRoot === null) {
    return null;
  }
  const settingsPath = join(projectRoot, 'UserSettings/UnityMcpSettings.json');

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

  if (typeof settings.serverPort === 'number') {
    return settings.serverPort;
  }

  if (typeof settings.customPort === 'number') {
    return settings.customPort;
  }

  return null;
}
