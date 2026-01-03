/**
 * Unity project root detection utility.
 * Searches child directories first (up to 3 levels deep), then parent directories.
 */

// Path traversal is intentional for finding Unity project root
/* eslint-disable security/detect-non-literal-fs-filename */

import { existsSync, readdirSync } from 'fs';
import { join, dirname } from 'path';

const CHILD_SEARCH_MAX_DEPTH = 3;

const EXCLUDED_DIRS = new Set([
  'node_modules',
  '.git',
  'Temp',
  'obj',
  'Build',
  'Builds',
  'Logs',
  'Library',
]);

function isUnityProjectWithUloop(dirPath: string): boolean {
  const hasAssets = existsSync(join(dirPath, 'Assets'));
  const hasProjectSettings = existsSync(join(dirPath, 'ProjectSettings'));
  const hasUloopSettings = existsSync(join(dirPath, 'UserSettings/UnityMcpSettings.json'));
  return hasAssets && hasProjectSettings && hasUloopSettings;
}

function findUnityProjectsInChildren(startPath: string, maxDepth: number): string[] {
  const projects: string[] = [];

  function scan(currentPath: string, depth: number): void {
    if (depth > maxDepth) {
      return;
    }

    if (!existsSync(currentPath)) {
      return;
    }

    if (isUnityProjectWithUloop(currentPath)) {
      projects.push(currentPath);
      return;
    }

    let entries: ReturnType<typeof readdirSync>;
    try {
      entries = readdirSync(currentPath, { withFileTypes: true });
    } catch {
      return;
    }

    for (const entry of entries) {
      if (!entry.isDirectory()) {
        continue;
      }

      if (EXCLUDED_DIRS.has(entry.name)) {
        continue;
      }

      const fullPath = join(currentPath, entry.name);
      scan(fullPath, depth + 1);
    }
  }

  scan(startPath, 0);
  return projects.sort();
}

function findUnityProjectInParents(startPath: string): string | null {
  let currentPath = startPath;

  while (true) {
    if (isUnityProjectWithUloop(currentPath)) {
      return currentPath;
    }

    const isGitRoot = existsSync(join(currentPath, '.git'));
    if (isGitRoot) {
      return null;
    }

    const parentPath = dirname(currentPath);
    if (parentPath === currentPath) {
      return null;
    }
    currentPath = parentPath;
  }
}

/**
 * Find Unity project root by searching child directories first, then parent directories.
 * A Unity project is identified by having both Assets/ and ProjectSettings/ directories.
 *
 * Search order:
 * 1. Child directories (up to 3 levels deep)
 * 2. Parent directories (up to root)
 *
 * If multiple Unity projects are found in child search, a warning is printed
 * and the first one (alphabetically) is used.
 *
 * Returns null if no Unity project is found.
 */
export function findUnityProjectRoot(startPath: string = process.cwd()): string | null {
  const childProjects = findUnityProjectsInChildren(startPath, CHILD_SEARCH_MAX_DEPTH);

  if (childProjects.length > 0) {
    if (childProjects.length > 1) {
      console.error('\x1b[33mWarning: Multiple Unity projects found in child directories:\x1b[0m');
      for (const project of childProjects) {
        console.error(`  - ${project}`);
      }
      console.error(`\x1b[33mUsing: ${childProjects[0]}\x1b[0m`);
      console.error('');
    }
    return childProjects[0];
  }

  return findUnityProjectInParents(startPath);
}
