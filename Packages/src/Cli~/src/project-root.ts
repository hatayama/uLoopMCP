/**
 * Unity project root detection utility.
 * Searches upward from current directory to find Unity project markers.
 */

// Path traversal is intentional for finding Unity project root by walking up directory tree
/* eslint-disable security/detect-non-literal-fs-filename */

import { existsSync } from 'fs';
import { join, dirname } from 'path';

/**
 * Find Unity project root by searching upward from start path.
 * A Unity project is identified by having both Assets/ and ProjectSettings/ directories.
 * Returns null if not inside a Unity project.
 */
export function findUnityProjectRoot(startPath: string = process.cwd()): string | null {
  let currentPath = startPath;

  while (true) {
    const hasAssets = existsSync(join(currentPath, 'Assets'));
    const hasProjectSettings = existsSync(join(currentPath, 'ProjectSettings'));

    if (hasAssets && hasProjectSettings) {
      return currentPath;
    }

    const parentPath = dirname(currentPath);
    if (parentPath === currentPath) {
      return null;
    }
    currentPath = parentPath;
  }
}
