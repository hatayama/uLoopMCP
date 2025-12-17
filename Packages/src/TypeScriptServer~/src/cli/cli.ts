/**
 * CLI entry point for uloop command.
 * Provides direct Unity communication without MCP server.
 */

import { Command } from 'commander';
import { executeToolCommand, listAvailableTools, GlobalOptions } from './execute-tool.js';
import { pascalToKebabCase, kebabToPascalCase } from './arg-parser.js';

const VERSION = '0.43.11';

interface CliOptions extends GlobalOptions {
  [key: string]: unknown;
}

const program = new Command();

program
  .name('uloop')
  .description('Unity MCP CLI - Direct communication with Unity Editor')
  .version(VERSION);

program
  .command('list')
  .description('List all available tools')
  .option('-p, --port <port>', 'Unity TCP port')
  .option('--json', 'Output as JSON')
  .action(async (options: CliOptions) => {
    await runWithErrorHandling(() => listAvailableTools(options));
  });

program
  .command('compile')
  .description('Execute Unity project compilation')
  .option('--force-recompile', 'Force recompilation')
  .option('-p, --port <port>', 'Unity TCP port')
  .option('--json', 'Output as JSON')
  .action(async (options: CliOptions) => {
    const params = buildParams(options, ['forceRecompile']);
    await runWithErrorHandling(() =>
      executeToolCommand('compile', params, extractGlobalOptions(options)),
    );
  });

program
  .command('get-logs')
  .description('Retrieve logs from Unity Console')
  .option('--log-type <type>', 'Log type (Error, Warning, Log, All)', 'All')
  .option('--max-count <count>', 'Maximum number of logs', '100')
  .option('--search-text <text>', 'Text to search within logs')
  .option('--include-stack-trace', 'Include stack trace', true)
  .option('--use-regex', 'Use regex for search')
  .option('--search-in-stack-trace', 'Search in stack trace')
  .option('-p, --port <port>', 'Unity TCP port')
  .option('--json', 'Output as JSON')
  .action(async (options: CliOptions) => {
    const params = buildParams(options, [
      'logType',
      'maxCount',
      'searchText',
      'includeStackTrace',
      'useRegex',
      'searchInStackTrace',
    ]);
    await runWithErrorHandling(() =>
      executeToolCommand('get-logs', params, extractGlobalOptions(options)),
    );
  });

program
  .command('run-tests')
  .description('Execute Unity Test Runner')
  .option('--test-mode <mode>', 'Test mode (EditMode, PlayMode)', 'PlayMode')
  .option('--filter-type <type>', 'Filter type (all, exact, regex, assembly)', 'all')
  .option('--filter-value <value>', 'Filter value')
  .option('--save-xml', 'Save test results as XML')
  .option('-p, --port <port>', 'Unity TCP port')
  .option('--json', 'Output as JSON')
  .action(async (options: CliOptions) => {
    const params = buildParams(options, ['testMode', 'filterType', 'filterValue', 'saveXml']);
    await runWithErrorHandling(() =>
      executeToolCommand('run-tests', params, extractGlobalOptions(options)),
    );
  });

program
  .command('clear-console')
  .description('Clear Unity console logs')
  .option('--add-confirmation-message', 'Add confirmation message after clearing')
  .option('-p, --port <port>', 'Unity TCP port')
  .option('--json', 'Output as JSON')
  .action(async (options: CliOptions) => {
    const params = buildParams(options, ['addConfirmationMessage']);
    await runWithErrorHandling(() =>
      executeToolCommand('clear-console', params, extractGlobalOptions(options)),
    );
  });

program
  .command('focus-window')
  .description('Bring Unity Editor window to front')
  .option('-p, --port <port>', 'Unity TCP port')
  .option('--json', 'Output as JSON')
  .action(async (options: CliOptions) => {
    await runWithErrorHandling(() =>
      executeToolCommand('focus-window', {}, extractGlobalOptions(options)),
    );
  });

program
  .command('get-hierarchy')
  .description('Get Unity Hierarchy structure')
  .option('--root-path <path>', 'Root GameObject path')
  .option('--max-depth <depth>', 'Maximum depth (-1 for unlimited)', '-1')
  .option('--include-components', 'Include component information', true)
  .option('--include-inactive', 'Include inactive GameObjects', true)
  .option('--include-paths', 'Include path information')
  .option('-p, --port <port>', 'Unity TCP port')
  .option('--json', 'Output as JSON')
  .action(async (options: CliOptions) => {
    const params = buildParams(options, [
      'rootPath',
      'maxDepth',
      'includeComponents',
      'includeInactive',
      'includePaths',
    ]);
    await runWithErrorHandling(() =>
      executeToolCommand('get-hierarchy', params, extractGlobalOptions(options)),
    );
  });

program
  .command('unity-search')
  .description('Search Unity project')
  .option('--search-query <query>', 'Search query')
  .option('--providers <providers>', 'Search providers (comma-separated)')
  .option('--max-results <count>', 'Maximum results', '50')
  .option('--save-to-file', 'Save results to file')
  .option('-p, --port <port>', 'Unity TCP port')
  .option('--json', 'Output as JSON')
  .action(async (options: CliOptions) => {
    const params = buildParams(options, ['searchQuery', 'providers', 'maxResults', 'saveToFile']);
    const providers = params['Providers'];
    if (typeof providers === 'string') {
      params['Providers'] = providers.split(',').map((s) => s.trim());
    }
    await runWithErrorHandling(() =>
      executeToolCommand('unity-search', params, extractGlobalOptions(options)),
    );
  });

program
  .command('get-menu-items')
  .description('Retrieve Unity MenuItems')
  .option('--filter-text <text>', 'Filter text')
  .option('--filter-type <type>', 'Filter type (contains, exact, startswith)', 'contains')
  .option('--max-count <count>', 'Maximum count', '200')
  .option('--include-validation', 'Include validation functions')
  .option('-p, --port <port>', 'Unity TCP port')
  .option('--json', 'Output as JSON')
  .action(async (options: CliOptions) => {
    const params = buildParams(options, [
      'filterText',
      'filterType',
      'maxCount',
      'includeValidation',
    ]);
    await runWithErrorHandling(() =>
      executeToolCommand('get-menu-items', params, extractGlobalOptions(options)),
    );
  });

program
  .command('execute-menu-item')
  .description('Execute Unity MenuItem')
  .option('--menu-item-path <path>', 'Menu item path (e.g., "GameObject/Create Empty")')
  .option('--use-reflection-fallback', 'Use reflection fallback', true)
  .option('-p, --port <port>', 'Unity TCP port')
  .option('--json', 'Output as JSON')
  .action(async (options: CliOptions) => {
    const params = buildParams(options, ['menuItemPath', 'useReflectionFallback']);
    await runWithErrorHandling(() =>
      executeToolCommand('execute-menu-item', params, extractGlobalOptions(options)),
    );
  });

program
  .command('find-game-objects')
  .description('Find GameObjects with search criteria')
  .option('--name-pattern <pattern>', 'Name pattern')
  .option('--search-mode <mode>', 'Search mode (Exact, Path, Regex, Contains)', 'Contains')
  .option('--required-components <components>', 'Required components (comma-separated)')
  .option('--tag <tag>', 'Tag filter')
  .option('--layer <layer>', 'Layer filter')
  .option('--max-results <count>', 'Maximum results', '20')
  .option('--include-inactive', 'Include inactive GameObjects')
  .option('-p, --port <port>', 'Unity TCP port')
  .option('--json', 'Output as JSON')
  .action(async (options: CliOptions) => {
    const params = buildParams(options, [
      'namePattern',
      'searchMode',
      'requiredComponents',
      'tag',
      'layer',
      'maxResults',
      'includeInactive',
    ]);
    const requiredComponents = params['RequiredComponents'];
    if (typeof requiredComponents === 'string') {
      params['RequiredComponents'] = requiredComponents.split(',').map((s) => s.trim());
    }
    await runWithErrorHandling(() =>
      executeToolCommand('find-game-objects', params, extractGlobalOptions(options)),
    );
  });

program
  .command('capture-gameview')
  .description('Capture Unity Game View as PNG')
  .option('--resolution-scale <scale>', 'Resolution scale (0.1 to 1.0)', '1')
  .option('-p, --port <port>', 'Unity TCP port')
  .option('--json', 'Output as JSON')
  .action(async (options: CliOptions) => {
    const params = buildParams(options, ['resolutionScale']);
    await runWithErrorHandling(() =>
      executeToolCommand('capture-gameview', params, extractGlobalOptions(options)),
    );
  });

program
  .command('execute-dynamic-code')
  .description('Execute C# code in Unity Editor')
  .option('--code <code>', 'C# code to execute')
  .option('--compile-only', 'Compile only without execution')
  .option('--auto-qualify-unity-types-once', 'Auto-qualify Unity types')
  .option('-p, --port <port>', 'Unity TCP port')
  .option('--json', 'Output as JSON')
  .action(async (options: CliOptions) => {
    const params = buildParams(options, ['code', 'compileOnly', 'autoQualifyUnityTypesOnce']);
    await runWithErrorHandling(() =>
      executeToolCommand('execute-dynamic-code', params, extractGlobalOptions(options)),
    );
  });

program
  .command('get-provider-details')
  .description('Get Unity Search provider details')
  .option('--provider-id <id>', 'Specific provider ID')
  .option('--active-only', 'Only active providers')
  .option('--include-descriptions', 'Include descriptions', true)
  .option('--sort-by-priority', 'Sort by priority', true)
  .option('-p, --port <port>', 'Unity TCP port')
  .option('--json', 'Output as JSON')
  .action(async (options: CliOptions) => {
    const params = buildParams(options, [
      'providerId',
      'activeOnly',
      'includeDescriptions',
      'sortByPriority',
    ]);
    await runWithErrorHandling(() =>
      executeToolCommand('get-provider-details', params, extractGlobalOptions(options)),
    );
  });

function buildParams(
  options: Record<string, unknown>,
  paramNames: string[],
): Record<string, unknown> {
  const params: Record<string, unknown> = {};

  for (const camelName of paramNames) {
    const value = options[camelName];
    if (value !== undefined) {
      const pascalName = kebabToPascalCase(pascalToKebabCase(camelName));
      params[pascalName] = value;
    }
  }

  return params;
}

function extractGlobalOptions(options: Record<string, unknown>): GlobalOptions {
  return {
    port: options['port'] as string | undefined,
    json: options['json'] as boolean | undefined,
  };
}

async function runWithErrorHandling(fn: () => Promise<void>): Promise<void> {
  try {
    await fn();
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);

    if (message.includes('ECONNREFUSED')) {
      console.error('\x1b[31mError: Cannot connect to Unity.\x1b[0m');
      console.error('Make sure Unity is running with uLoopMCP installed.');
      process.exit(1);
    }

    console.error(`\x1b[31mError: ${message}\x1b[0m`);
    process.exit(1);
  }
}

program.parse();
