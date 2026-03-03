/**
 * CLI argument parser for tool parameters.
 * Converts CLI options to Unity tool parameters.
 */

/**
 * Converts PascalCase parameter name to kebab-case CLI option name.
 * e.g., "ForceRecompile" -> "force-recompile"
 */
export function pascalToKebabCase(pascal: string): string {
  const kebab = pascal.replace(/([A-Z])/g, '-$1').toLowerCase();
  return kebab.startsWith('-') ? kebab.slice(1) : kebab;
}
