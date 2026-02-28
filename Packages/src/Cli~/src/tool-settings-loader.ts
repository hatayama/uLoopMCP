/**
 * Loads tool toggle settings from .uloop/settings.tools.json.
 * Used by CLI to filter out disabled tools from help and command registration.
 */

// File paths are constructed from Unity project root, not from untrusted user input
/* eslint-disable security/detect-non-literal-fs-filename */

import { existsSync, readFileSync } from 'fs';
import { join } from 'path';
import { findUnityProjectRoot } from './project-root.js';
import type { ToolDefinition } from './tool-cache.js';

const ULOOP_DIR = '.uloop';
const TOOL_SETTINGS_FILE = 'settings.tools.json';

interface ToolSettingsData {
  disabledTools: string[];
}

export function loadDisabledTools(): string[] {
  const projectRoot: string | null = findUnityProjectRoot();
  if (projectRoot === null) {
    return [];
  }

  const settingsPath: string = join(projectRoot, ULOOP_DIR, TOOL_SETTINGS_FILE);
  if (!existsSync(settingsPath)) {
    return [];
  }

  const content: string = readFileSync(settingsPath, 'utf-8');
  if (!content.trim()) {
    return [];
  }

  let parsed: unknown;
  try {
    parsed = JSON.parse(content);
  } catch {
    return [];
  }

  if (typeof parsed !== 'object' || parsed === null) {
    return [];
  }

  const data = parsed as ToolSettingsData;
  if (!Array.isArray(data.disabledTools)) {
    return [];
  }

  return data.disabledTools;
}

export function isToolEnabled(toolName: string): boolean {
  const disabledTools: string[] = loadDisabledTools();
  return !disabledTools.includes(toolName);
}

export function filterEnabledTools(tools: ToolDefinition[]): ToolDefinition[] {
  const disabledTools: string[] = loadDisabledTools();
  if (disabledTools.length === 0) {
    return tools;
  }
  return tools.filter((tool) => !disabledTools.includes(tool.name));
}
