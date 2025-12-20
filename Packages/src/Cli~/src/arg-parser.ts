/**
 * CLI argument parser for tool parameters.
 * Converts CLI options to Unity tool parameters.
 */

export interface ToolParameter {
  Type: string;
  Description: string;
  DefaultValue?: unknown;
  Enum?: string[];
}

export interface ToolSchema {
  properties: Record<string, ToolParameter>;
}

/**
 * Converts kebab-case CLI option name to PascalCase parameter name.
 * e.g., "force-recompile" -> "ForceRecompile"
 */
export function kebabToPascalCase(kebab: string): string {
  return kebab
    .split('-')
    .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
    .join('');
}

/**
 * Converts PascalCase parameter name to kebab-case CLI option name.
 * e.g., "ForceRecompile" -> "force-recompile"
 */
export function pascalToKebabCase(pascal: string): string {
  const kebab = pascal.replace(/([A-Z])/g, '-$1').toLowerCase();
  return kebab.startsWith('-') ? kebab.slice(1) : kebab;
}

/**
 * Parses CLI arguments into tool parameters based on the tool schema.
 */
export function parseToolArgs(
  args: string[],
  schema: ToolSchema,
  cliOptions: Record<string, unknown>,
): Record<string, unknown> {
  const params: Record<string, unknown> = {};

  for (const [paramName, paramInfo] of Object.entries(schema.properties)) {
    const kebabName = pascalToKebabCase(paramName);
    const cliValue = cliOptions[kebabName];

    if (cliValue === undefined) {
      if (paramInfo.DefaultValue !== undefined) {
        params[paramName] = paramInfo.DefaultValue;
      }
      continue;
    }

    params[paramName] = convertValue(cliValue, paramInfo.Type);
  }

  return params;
}

function convertValue(value: unknown, type: string): unknown {
  if (value === undefined || value === null) {
    return value;
  }

  const lowerType = type.toLowerCase();

  switch (lowerType) {
    case 'boolean':
      if (typeof value === 'boolean') {
        return value;
      }
      if (typeof value === 'string') {
        return value.toLowerCase() === 'true';
      }
      return Boolean(value);

    case 'number':
    case 'integer':
      if (typeof value === 'number') {
        return value;
      }
      if (typeof value === 'string') {
        const parsed = parseFloat(value);
        return isNaN(parsed) ? 0 : parsed;
      }
      return Number(value);

    case 'string':
      if (typeof value === 'string') {
        return value;
      }
      if (typeof value === 'number' || typeof value === 'boolean') {
        return String(value);
      }
      return JSON.stringify(value);

    case 'array':
      if (Array.isArray(value)) {
        return value;
      }
      if (typeof value === 'string') {
        return value.split(',').map((s) => s.trim());
      }
      return [value];

    default:
      return value;
  }
}

/**
 * Generates commander.js option string from parameter info.
 * e.g., "--force-recompile" for boolean, "--max-count <value>" for others
 */
export function generateOptionString(paramName: string, paramInfo: ToolParameter): string {
  const kebabName = pascalToKebabCase(paramName);
  const lowerType = paramInfo.Type.toLowerCase();

  if (lowerType === 'boolean') {
    return `--${kebabName}`;
  }

  return `--${kebabName} <value>`;
}
