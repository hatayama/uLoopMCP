/**
 * CLI entry point for uloop command.
 * Provides direct Unity communication without MCP server.
 * Commands are dynamically registered from tools.json cache.
 */

// CLI tools output to console by design, file paths are constructed from trusted sources (project root detection),
// and object keys come from tool definitions which are internal trusted data
/* eslint-disable no-console, security/detect-non-literal-fs-filename, security/detect-object-injection */

import { existsSync, readFileSync, writeFileSync, mkdirSync, unlinkSync } from 'fs';
import { join, basename, dirname } from 'path';
import { homedir } from 'os';
import { spawn } from 'child_process';
import { Command } from 'commander';
import {
  executeToolCommand,
  listAvailableTools,
  GlobalOptions,
  syncTools,
  isVersionOlder,
} from './execute-tool.js';
import {
  loadToolsCache,
  hasCacheFile,
  ToolDefinition,
  ToolProperty,
  getCachedServerVersion,
} from './tool-cache.js';
import { pascalToKebabCase } from './arg-parser.js';
import { registerSkillsCommand } from './skills/skills-command.js';
import { registerLaunchCommand } from './commands/launch.js';
import { registerFocusWindowCommand } from './commands/focus-window.js';
import { VERSION } from './version.js';
import { findUnityProjectRoot } from './project-root.js';
import { validateProjectPath } from './port-resolver.js';

interface CliOptions extends GlobalOptions {
  [key: string]: unknown;
}

const LAUNCH_COMMAND = 'launch' as const;
const UPDATE_COMMAND = 'update' as const;

// commander.js built-in flags that exit immediately without needing Unity
const NO_SYNC_FLAGS = ['-v', '--version', '-h', '--help'] as const;

const BUILTIN_COMMANDS = [
  'list',
  'sync',
  'completion',
  UPDATE_COMMAND,
  'fix',
  'skills',
  LAUNCH_COMMAND,
  'focus-window',
] as const;

const program = new Command();

program
  .name('uloop')
  .description('Unity MCP CLI - Direct communication with Unity Editor')
  .version(VERSION, '-v, --version', 'Output the version number');

// --list-commands: Output command names for shell completion
program.option('--list-commands', 'List all command names (for shell completion)');

// --list-options <cmd>: Output options for a specific command (for shell completion)
program.option('--list-options <cmd>', 'List options for a command (for shell completion)');

// Built-in commands (not from tools.json)
program
  .command('list')
  .description('List all available tools from Unity')
  .option('-p, --port <port>', 'Unity TCP port')
  .option('--project-path <path>', 'Unity project path')
  .action(async (options: CliOptions) => {
    await runWithErrorHandling(() => listAvailableTools(extractGlobalOptions(options)));
  });

program
  .command('sync')
  .description('Sync tool definitions from Unity to local cache')
  .option('-p, --port <port>', 'Unity TCP port')
  .option('--project-path <path>', 'Unity project path')
  .action(async (options: CliOptions) => {
    await runWithErrorHandling(() => syncTools(extractGlobalOptions(options)));
  });

program
  .command('completion')
  .description('Setup shell completion')
  .option('--install', 'Install completion to shell config file')
  .option('--shell <type>', 'Shell type: bash, zsh, or powershell')
  .action((options: { install?: boolean; shell?: string }) => {
    handleCompletion(options.install ?? false, options.shell);
  });

program
  .command('update')
  .description('Update uloop CLI to the latest version')
  .action(() => {
    updateCli();
  });

program
  .command('fix')
  .description('Clean up stale lock files that may prevent CLI from connecting')
  .option('--project-path <path>', 'Unity project path')
  .action(async (options: { projectPath?: string }) => {
    await runWithErrorHandling(() => {
      cleanupLockFiles(options.projectPath);
      return Promise.resolve();
    });
  });

// Register skills subcommand
registerSkillsCommand(program);

// Register launch subcommand
registerLaunchCommand(program);

// Register focus-window subcommand
registerFocusWindowCommand(program);

/**
 * Register a tool as a CLI command dynamically.
 */
function registerToolCommand(tool: ToolDefinition): void {
  // Skip if already registered as a built-in command
  if (BUILTIN_COMMANDS.includes(tool.name as (typeof BUILTIN_COMMANDS)[number])) {
    return;
  }
  const cmd = program.command(tool.name).description(tool.description);

  // Add options from inputSchema.properties
  const properties = tool.inputSchema.properties;
  for (const [propName, propInfo] of Object.entries(properties)) {
    const optionStr = generateOptionString(propName, propInfo);
    const description = buildOptionDescription(propInfo);
    const defaultValue = propInfo.default;
    if (defaultValue !== undefined && defaultValue !== null) {
      // Convert default values to strings for consistent CLI handling
      const defaultStr = convertDefaultToString(defaultValue);
      cmd.option(optionStr, description, defaultStr);
    } else {
      cmd.option(optionStr, description);
    }
  }

  // Add global options
  cmd.option('-p, --port <port>', 'Unity TCP port');
  cmd.option('--project-path <path>', 'Unity project path');

  cmd.action(async (options: CliOptions) => {
    const params = buildParams(options, properties);

    // Unescape \! to ! for execute-dynamic-code
    // Some shells (e.g., Claude Code's bash wrapper) escape ! as \!
    if (tool.name === 'execute-dynamic-code' && params['Code']) {
      const code = params['Code'] as string;
      params['Code'] = code.replace(/\\!/g, '!');
    }

    await runWithErrorHandling(() =>
      executeToolCommand(tool.name, params, extractGlobalOptions(options)),
    );
  });
}

/**
 * Convert default value to string for CLI option registration.
 */
function convertDefaultToString(value: unknown): string {
  if (typeof value === 'string') {
    return value;
  }
  if (typeof value === 'boolean' || typeof value === 'number') {
    return String(value);
  }
  return JSON.stringify(value);
}

/**
 * Generate commander.js option string from property info.
 * All types use value format (--option <value>) for consistency with MCP.
 */
function generateOptionString(propName: string, propInfo: ToolProperty): string {
  const kebabName = pascalToKebabCase(propName);
  void propInfo; // All types now use value format
  return `--${kebabName} <value>`;
}

/**
 * Build option description with enum values if present.
 */
function buildOptionDescription(propInfo: ToolProperty): string {
  let desc = propInfo.description || '';
  if (propInfo.enum && propInfo.enum.length > 0) {
    desc += ` (${propInfo.enum.join(', ')})`;
  }
  return desc;
}

/**
 * Build parameters from CLI options.
 */
function buildParams(
  options: Record<string, unknown>,
  properties: Record<string, ToolProperty>,
): Record<string, unknown> {
  const params: Record<string, unknown> = {};

  for (const propName of Object.keys(properties)) {
    const camelName = propName.charAt(0).toLowerCase() + propName.slice(1);
    const value = options[camelName];

    if (value !== undefined) {
      const propInfo = properties[propName];
      params[propName] = convertValue(value, propInfo);
    }
  }

  return params;
}

/**
 * Convert CLI value to appropriate type based on property info.
 */
function convertValue(value: unknown, propInfo: ToolProperty): unknown {
  const lowerType = propInfo.type.toLowerCase();

  if (lowerType === 'boolean' && typeof value === 'string') {
    const lower = value.toLowerCase();
    if (lower === 'true') {
      return true;
    }
    if (lower === 'false') {
      return false;
    }
    throw new Error(`Invalid boolean value: ${value}. Use 'true' or 'false'.`);
  }

  if (lowerType === 'array' && typeof value === 'string') {
    // Handle JSON array format (e.g., "[]" or "[\"item1\",\"item2\"]")
    if (value.startsWith('[') && value.endsWith(']')) {
      try {
        const parsed: unknown = JSON.parse(value);
        if (Array.isArray(parsed)) {
          return parsed;
        }
      } catch {
        // Fall through to comma-separated handling
      }
    }
    // Handle comma-separated format (e.g., "item1,item2")
    return value.split(',').map((s) => s.trim());
  }

  if (lowerType === 'integer' && typeof value === 'string') {
    const parsed = parseInt(value, 10);
    if (isNaN(parsed)) {
      throw new Error(`Invalid integer value: ${value}`);
    }
    return parsed;
  }

  if (lowerType === 'number' && typeof value === 'string') {
    const parsed = parseFloat(value);
    if (isNaN(parsed)) {
      throw new Error(`Invalid number value: ${value}`);
    }
    return parsed;
  }

  if (lowerType === 'object') {
    if (typeof value === 'string') {
      const trimmed = value.trim();
      if (!trimmed.startsWith('{') || !trimmed.endsWith('}')) {
        throw new Error(`Invalid object value: ${value}. Use JSON object syntax.`);
      }
      try {
        const parsed: unknown = JSON.parse(trimmed);
        if (typeof parsed === 'object' && parsed !== null && !Array.isArray(parsed)) {
          return parsed;
        }
      } catch {
        // fall through to error below
      }
      throw new Error(`Invalid object value: ${value}. Use JSON object syntax.`);
    }
    if (typeof value === 'object' && value !== null && !Array.isArray(value)) {
      return value;
    }
    throw new Error(`Invalid object value: ${String(value)}. Use JSON object syntax.`);
  }

  return value;
}

function extractGlobalOptions(options: Record<string, unknown>): GlobalOptions {
  return {
    port: options['port'] as string | undefined,
    projectPath: options['projectPath'] as string | undefined,
  };
}

function isConnectionError(message: string): boolean {
  return message.includes('ECONNREFUSED') || message.includes('EADDRNOTAVAIL');
}

function printConnectionError(): void {
  console.error('\x1b[31mError: Cannot connect to Unity.\x1b[0m');
  console.error('Make sure Unity Editor is open and uLoopMCP server is running.');
  console.error('');
  console.error('[For AI] Please report the above to the user.');
}

/**
 * Print version mismatch diagnostic information when errors occur.
 * Only prints if versions are mismatched.
 */
function printVersionMismatchDiagnostic(): void {
  const serverVersion = getCachedServerVersion();
  if (serverVersion === undefined) {
    return;
  }

  if (serverVersion === VERSION) {
    return;
  }

  const isCliOlder = isVersionOlder(VERSION, serverVersion);
  const action = isCliOlder ? 'Update' : 'Downgrade';
  const updateCommand = `npm install -g uloop-cli@${serverVersion}`;

  console.error('');
  console.error('\x1b[33m━━━ Version Mismatch Diagnostic ━━━\x1b[0m');
  console.error('');
  console.error(`  CLI version:     ${VERSION}`);
  console.error(`  Unity package:   ${serverVersion}`);
  console.error('  Version mismatch may cause communication errors.');
  console.error('');
  console.error(`  To fix: ${action} CLI to match Unity package version`);
  console.error(`    ${updateCommand}`);
  console.error('');
  console.error('[For AI] Please ask the user if they would like to run this command.');
  console.error('\x1b[33m━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\x1b[0m');
}

async function runWithErrorHandling(fn: () => Promise<void>): Promise<void> {
  try {
    await fn();
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);

    // Unity busy states have clear causes - no version diagnostic needed
    if (message === 'UNITY_COMPILING') {
      console.error('\x1b[33m⏳ Unity is compiling scripts.\x1b[0m');
      console.error('Please wait for compilation to finish and try again.');
      console.error('');
      console.error('If the issue persists, run: uloop fix');
      process.exit(1);
    }

    if (message === 'UNITY_DOMAIN_RELOAD') {
      console.error('\x1b[33m⏳ Unity is reloading (Domain Reload in progress).\x1b[0m');
      console.error('Please wait a moment and try again.');
      console.error('');
      console.error('If the issue persists, run: uloop fix');
      process.exit(1);
    }

    if (message === 'UNITY_SERVER_STARTING') {
      console.error('\x1b[33m⏳ Unity server is starting.\x1b[0m');
      console.error('Please wait a moment and try again.');
      console.error('');
      console.error('If the issue persists, run: uloop fix');
      process.exit(1);
    }

    // Errors that may be caused by version mismatch - show diagnostic
    if (message === 'UNITY_NO_RESPONSE') {
      console.error('\x1b[33m⏳ Unity is busy (no response received).\x1b[0m');
      console.error('Unity may be compiling, reloading, or starting. Please wait and try again.');
      printVersionMismatchDiagnostic();
      process.exit(1);
    }

    if (isConnectionError(message)) {
      printConnectionError();
      printVersionMismatchDiagnostic();
      process.exit(1);
    }

    // Timeout errors
    if (message.includes('Request timed out')) {
      console.error(`\x1b[31mError: ${message}\x1b[0m`);
      printVersionMismatchDiagnostic();
      process.exit(1);
    }

    console.error(`\x1b[31mError: ${message}\x1b[0m`);
    process.exit(1);
  }
}

/**
 * Detect shell type from environment.
 */
function detectShell(): 'bash' | 'zsh' | 'powershell' | null {
  // Check $SHELL first (works for bash/zsh including MINGW64)
  const shell = process.env['SHELL'] || '';
  const shellName = basename(shell).replace(/\.exe$/i, ''); // Remove .exe for Windows
  if (shellName === 'zsh') {
    return 'zsh';
  }
  if (shellName === 'bash') {
    return 'bash';
  }

  // Check for PowerShell (only if $SHELL is not set)
  if (process.env['PSModulePath']) {
    return 'powershell';
  }

  return null;
}

/**
 * Get shell config file path.
 */
function getShellConfigPath(shell: 'bash' | 'zsh' | 'powershell'): string {
  const home = homedir();
  if (shell === 'zsh') {
    return join(home, '.zshrc');
  }
  if (shell === 'powershell') {
    // PowerShell profile path
    return join(home, 'Documents', 'WindowsPowerShell', 'Microsoft.PowerShell_profile.ps1');
  }
  return join(home, '.bashrc');
}

/**
 * Get completion script for a shell.
 */
function getCompletionScript(shell: 'bash' | 'zsh' | 'powershell'): string {
  if (shell === 'bash') {
    return `# uloop bash completion
_uloop_completions() {
  local cur="\${COMP_WORDS[COMP_CWORD]}"
  local cmd="\${COMP_WORDS[1]}"

  if [[ \${COMP_CWORD} -eq 1 ]]; then
    COMPREPLY=($(compgen -W "$(uloop --list-commands 2>/dev/null)" -- "\${cur}"))
  elif [[ \${COMP_CWORD} -ge 2 ]]; then
    COMPREPLY=($(compgen -W "$(uloop --list-options \${cmd} 2>/dev/null)" -- "\${cur}"))
  fi
}
complete -F _uloop_completions uloop`;
  }

  if (shell === 'powershell') {
    return `# uloop PowerShell completion
Register-ArgumentCompleter -Native -CommandName uloop -ScriptBlock {
  param($wordToComplete, $commandAst, $cursorPosition)
  $commands = $commandAst.CommandElements
  if ($commands.Count -eq 1) {
    uloop --list-commands 2>$null | Where-Object { $_ -like "$wordToComplete*" } | ForEach-Object {
      [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
    }
  } elseif ($commands.Count -ge 2) {
    $cmd = $commands[1].ToString()
    uloop --list-options $cmd 2>$null | Where-Object { $_ -like "$wordToComplete*" } | ForEach-Object {
      [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
    }
  }
}`;
  }

  /* eslint-disable no-useless-escape */
  return `# uloop zsh completion
_uloop() {
  local -a commands
  local -a options
  local -a used_options

  if (( CURRENT == 2 )); then
    commands=(\${(f)"$(uloop --list-commands 2>/dev/null)"})
    _describe 'command' commands
  elif (( CURRENT >= 3 )); then
    options=(\${(f)"$(uloop --list-options \${words[2]} 2>/dev/null)"})
    used_options=(\${words:2})
    for opt in \${used_options}; do
      options=(\${options:#\$opt})
    done
    _describe 'option' options
  fi
}
compdef _uloop uloop`;
  /* eslint-enable no-useless-escape */
}

/**
 * Get the currently installed version of uloop-cli from npm.
 */
function getInstalledVersion(callback: (version: string | null) => void): void {
  const npmCommand = process.platform === 'win32' ? 'npm.cmd' : 'npm';
  const child = spawn(npmCommand, ['list', '-g', 'uloop-cli', '--json'], {
    shell: true,
  });

  let stdout = '';
  child.stdout.on('data', (data: Buffer) => {
    stdout += data.toString();
  });

  child.on('close', (code) => {
    if (code !== 0) {
      callback(null);
      return;
    }

    let parsed: unknown;
    try {
      parsed = JSON.parse(stdout);
    } catch {
      callback(null);
      return;
    }

    if (typeof parsed !== 'object' || parsed === null) {
      callback(null);
      return;
    }

    const deps = (parsed as Record<string, unknown>)['dependencies'];
    if (typeof deps !== 'object' || deps === null) {
      callback(null);
      return;
    }

    const uloopCli = (deps as Record<string, unknown>)['uloop-cli'];
    if (typeof uloopCli !== 'object' || uloopCli === null) {
      callback(null);
      return;
    }

    const version = (uloopCli as Record<string, unknown>)['version'];
    if (typeof version !== 'string') {
      callback(null);
      return;
    }

    callback(version);
  });

  child.on('error', () => {
    callback(null);
  });
}

/**
 * Update uloop CLI to the latest version using npm.
 */
function updateCli(): void {
  const previousVersion = VERSION;
  console.log('Updating uloop-cli to the latest version...');

  const npmCommand = process.platform === 'win32' ? 'npm.cmd' : 'npm';
  const child = spawn(npmCommand, ['install', '-g', 'uloop-cli@latest'], {
    stdio: 'inherit',
    shell: true,
  });

  child.on('close', (code) => {
    if (code === 0) {
      getInstalledVersion((newVersion) => {
        if (newVersion && newVersion !== previousVersion) {
          console.log(`\n✅ uloop-cli updated: v${previousVersion} -> v${newVersion}`);
        } else {
          console.log(`\n✅ Already up to date (v${previousVersion})`);
        }
      });
    } else {
      console.error(`\n❌ Update failed with exit code ${code}`);
      process.exit(1);
    }
  });

  child.on('error', (err) => {
    console.error(`❌ Failed to run npm: ${err.message}`);
    process.exit(1);
  });
}

const LOCK_FILES = ['compiling.lock', 'domainreload.lock', 'serverstarting.lock'] as const;

/**
 * Clean up stale lock files that may prevent CLI from connecting to Unity.
 */
function cleanupLockFiles(projectPath?: string): void {
  const projectRoot =
    projectPath !== undefined ? validateProjectPath(projectPath) : findUnityProjectRoot();
  if (projectRoot === null) {
    console.error('Could not find Unity project root.');
    process.exit(1);
  }

  const tempDir = join(projectRoot, 'Temp');
  let cleaned = 0;

  for (const lockFile of LOCK_FILES) {
    const lockPath = join(tempDir, lockFile);
    if (existsSync(lockPath)) {
      unlinkSync(lockPath);
      console.log(`Removed: ${lockFile}`);
      cleaned++;
    }
  }

  if (cleaned === 0) {
    console.log('No lock files found.');
  } else {
    console.log(`\n✅ Cleaned up ${cleaned} lock file(s).`);
  }
}

/**
 * Handle completion command.
 */
function handleCompletion(install: boolean, shellOverride?: string): void {
  let shell: 'bash' | 'zsh' | 'powershell' | null;

  if (shellOverride) {
    const normalized = shellOverride.toLowerCase();
    if (normalized === 'bash' || normalized === 'zsh' || normalized === 'powershell') {
      shell = normalized;
    } else {
      console.error(`Unknown shell: ${shellOverride}. Supported: bash, zsh, powershell`);
      process.exit(1);
    }
  } else {
    shell = detectShell();
  }

  if (!shell) {
    console.error('Could not detect shell. Use --shell option: bash, zsh, or powershell');
    process.exit(1);
  }

  const script = getCompletionScript(shell);

  if (!install) {
    console.log(script);
    return;
  }

  // Install to shell config file
  const configPath = getShellConfigPath(shell);

  // PowerShell profile directory may not exist on fresh installations
  const configDir = dirname(configPath);
  if (!existsSync(configDir)) {
    mkdirSync(configDir, { recursive: true });
  }

  // Remove existing uloop completion and add new one
  let content = '';
  if (existsSync(configPath)) {
    content = readFileSync(configPath, 'utf-8');
    // Remove existing uloop completion block using markers
    content = content.replace(
      /\n?# >>> uloop completion >>>[\s\S]*?# <<< uloop completion <<<\n?/g,
      '',
    );
  }

  // Add new completion with markers
  const startMarker = '# >>> uloop completion >>>';
  const endMarker = '# <<< uloop completion <<<';

  if (shell === 'powershell') {
    const lineToAdd = `\n${startMarker}\n${script}\n${endMarker}\n`;
    writeFileSync(configPath, content + lineToAdd, 'utf-8');
  } else {
    // Include --shell option to ensure correct shell detection
    const evalLine = `eval "$(uloop completion --shell ${shell})"`;
    const lineToAdd = `\n${startMarker}\n${evalLine}\n${endMarker}\n`;
    writeFileSync(configPath, content + lineToAdd, 'utf-8');
  }

  console.log(`Completion installed to ${configPath}`);
  if (shell === 'powershell') {
    console.log('Restart PowerShell to enable completion.');
  } else {
    console.log(`Run 'source ${configPath}' or restart your shell to enable completion.`);
  }
}

/**
 * Handle --list-commands and --list-options before parsing.
 */
function handleCompletionOptions(): boolean {
  const args = process.argv.slice(2);

  if (args.includes('--list-commands')) {
    const tools = loadToolsCache();
    const allCommands = [...BUILTIN_COMMANDS, ...tools.tools.map((t) => t.name)];
    console.log(allCommands.join('\n'));
    return true;
  }

  const listOptionsIdx = args.indexOf('--list-options');
  if (listOptionsIdx !== -1 && args[listOptionsIdx + 1]) {
    const cmdName = args[listOptionsIdx + 1];
    listOptionsForCommand(cmdName);
    return true;
  }

  return false;
}

/**
 * List options for a specific command.
 */
function listOptionsForCommand(cmdName: string): void {
  // Built-in commands have no tool-specific options
  if (BUILTIN_COMMANDS.includes(cmdName as (typeof BUILTIN_COMMANDS)[number])) {
    return;
  }

  // Tool commands - only output tool-specific options
  const tools = loadToolsCache();
  const tool = tools.tools.find((t) => t.name === cmdName);
  if (!tool) {
    return;
  }

  const options: string[] = [];
  for (const propName of Object.keys(tool.inputSchema.properties)) {
    const kebabName = pascalToKebabCase(propName);
    options.push(`--${kebabName}`);
  }

  console.log(options.join('\n'));
}

/**
 * Check if a command exists in the current program.
 */
function commandExists(cmdName: string): boolean {
  if (BUILTIN_COMMANDS.includes(cmdName as (typeof BUILTIN_COMMANDS)[number])) {
    return true;
  }
  const tools = loadToolsCache();
  return tools.tools.some((t) => t.name === cmdName);
}

function shouldSkipAutoSync(cmdName: string | undefined, args: string[]): boolean {
  if (cmdName === LAUNCH_COMMAND || cmdName === UPDATE_COMMAND) {
    return true;
  }
  return args.some((arg) => (NO_SYNC_FLAGS as readonly string[]).includes(arg));
}

function extractSyncGlobalOptions(args: string[]): GlobalOptions {
  const options: GlobalOptions = {};

  for (let i = 0; i < args.length; i++) {
    const arg = args[i];
    if (arg === '--port' || arg === '-p') {
      const nextArg = args[i + 1];
      if (nextArg !== undefined && !nextArg.startsWith('-')) {
        options.port = nextArg;
      }
      continue;
    }

    if (arg.startsWith('--port=')) {
      options.port = arg.slice('--port='.length);
      continue;
    }

    if (arg === '--project-path') {
      const nextArg = args[i + 1];
      if (nextArg !== undefined && !nextArg.startsWith('-')) {
        options.projectPath = nextArg;
      }
      continue;
    }

    if (arg.startsWith('--project-path=')) {
      options.projectPath = arg.slice('--project-path='.length);
      continue;
    }
  }

  return options;
}

/**
 * Main entry point with auto-sync for unknown commands.
 */
async function main(): Promise<void> {
  if (handleCompletionOptions()) {
    return;
  }

  const args = process.argv.slice(2);
  const cmdName = args.find((arg) => !arg.startsWith('-'));
  const syncGlobalOptions = extractSyncGlobalOptions(args);

  if (!shouldSkipAutoSync(cmdName, args)) {
    // Check if cache version is outdated and auto-sync if needed
    const cachedVersion = loadToolsCache().version;
    if (hasCacheFile() && cachedVersion !== VERSION) {
      console.log(
        `\x1b[33mCache outdated (${cachedVersion} → ${VERSION}). Syncing tools from Unity...\x1b[0m`,
      );
      try {
        await syncTools(syncGlobalOptions);
        console.log('\x1b[32m✓ Tools synced successfully.\x1b[0m\n');
      } catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        if (isConnectionError(message)) {
          console.error('\x1b[33mWarning: Failed to sync tools. Using cached definitions.\x1b[0m');
          console.error("\x1b[33mRun 'uloop sync' manually when Unity is available.\x1b[0m\n");
        } else {
          console.error('\x1b[33mWarning: Failed to sync tools. Using cached definitions.\x1b[0m');
          console.error(`\x1b[33mError: ${message}\x1b[0m`);
          console.error("\x1b[33mRun 'uloop sync' manually when Unity is available.\x1b[0m\n");
        }
      }
    }
  }

  // Register tool commands from cache (after potential auto-sync)
  const toolsCache = loadToolsCache();
  for (const tool of toolsCache.tools) {
    registerToolCommand(tool);
  }

  if (cmdName && !commandExists(cmdName)) {
    console.log(`\x1b[33mUnknown command '${cmdName}'. Syncing tools from Unity...\x1b[0m`);
    try {
      await syncTools(syncGlobalOptions);
      const newCache = loadToolsCache();
      const tool = newCache.tools.find((t) => t.name === cmdName);
      if (tool) {
        registerToolCommand(tool);
        console.log(`\x1b[32m✓ Found '${cmdName}' after sync.\x1b[0m\n`);
      } else {
        console.error(`\x1b[31mError: Command '${cmdName}' not found even after sync.\x1b[0m`);
        process.exit(1);
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      if (isConnectionError(message)) {
        printConnectionError();
      } else {
        console.error(`\x1b[31mError: Failed to sync tools: ${message}\x1b[0m`);
      }
      process.exit(1);
    }
  }

  program.parse();
}

void main();
