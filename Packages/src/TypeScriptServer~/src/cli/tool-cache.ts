/**
 * Tool cache management for CLI.
 * Handles loading/saving tool definitions from .uloop/tools.json cache.
 */

import { existsSync, readFileSync, writeFileSync, mkdirSync } from 'fs';
import { join } from 'path';
import defaultToolsData from './default-tools.json';

export interface ToolProperty {
  type: string;
  description?: string;
  default?: unknown;
  enum?: string[];
  items?: { type: string };
}

export interface ToolInputSchema {
  type: string;
  properties: Record<string, ToolProperty>;
  required?: string[];
}

export interface ToolDefinition {
  name: string;
  description: string;
  inputSchema: ToolInputSchema;
}

export interface ToolsCache {
  version: string;
  updatedAt?: string;
  tools: ToolDefinition[];
}

const CACHE_DIR = '.uloop';
const CACHE_FILE = 'tools.json';

function getCacheDir(): string {
  return join(process.cwd(), CACHE_DIR);
}

function getCachePath(): string {
  return join(getCacheDir(), CACHE_FILE);
}

/**
 * Load default tools bundled with npm package.
 */
export function getDefaultTools(): ToolsCache {
  return defaultToolsData as ToolsCache;
}

/**
 * Load tools from cache file, falling back to default tools if cache doesn't exist.
 */
export function loadToolsCache(): ToolsCache {
  const cachePath = getCachePath();

  if (existsSync(cachePath)) {
    try {
      const content = readFileSync(cachePath, 'utf-8');
      return JSON.parse(content) as ToolsCache;
    } catch {
      return getDefaultTools();
    }
  }

  return getDefaultTools();
}

/**
 * Save tools to cache file.
 */
export function saveToolsCache(cache: ToolsCache): void {
  const cacheDir = getCacheDir();
  const cachePath = getCachePath();

  if (!existsSync(cacheDir)) {
    mkdirSync(cacheDir, { recursive: true });
  }

  const content = JSON.stringify(cache, null, 2);
  writeFileSync(cachePath, content, 'utf-8');
}

/**
 * Check if cache file exists.
 */
export function hasCacheFile(): boolean {
  return existsSync(getCachePath());
}

/**
 * Get the cache file path for display purposes.
 */
export function getCacheFilePath(): string {
  return getCachePath();
}
