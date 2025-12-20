/**
 * CLI entry point for uloop command.
 * Provides direct Unity communication without MCP server.
 * Commands are dynamically registered from tools.json cache.
 */

import { existsSync, readFileSync, appendFileSync } from 'fs';
import { join, basename } from 'path';
import { homedir } from 'os';
import { spawn } from 'child_process';
import { Command } from 'commander';
import {
  executeToolCommand,
  listAvailableTools,
  GlobalOptions,
  syncTools,
} from './execute-tool.js';
import { loadToolsCache, ToolDefinition, ToolProperty } from './tool-cache.js';
import { pascalToKebabCase } from './arg-parser.js';
import { registerSkillsCommand } from './skills/skills-command.js';
import { VERSION } from './version.js';

interface CliOptions extends GlobalOptions {
  [key: string]: unknown;
}

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
  .action(async (options: CliOptions) => {
    await runWithErrorHandling(() => listAvailableTools(options));
  });

program
  .command('sync')
  .description('Sync tool definitions from Unity to local cache')
  .option('-p, --port <port>', 'Unity TCP port')
  .action(async (options: CliOptions) => {
    await runWithErrorHandling(() => syncTools(options));
  });

program
  .command('completion')
  .description('Setup shell completion (auto-detects shell from $SHELL)')
  .option('--install', 'Install completion to shell config file')
  .action((options: { install?: boolean }) => {
    handleCompletion(options.install ?? false);
  });

program
  .command('update')
  .description('Update uloop CLI to the latest version')
  .action(() => {
    updateCli();
  });

// Register skills subcommand
registerSkillsCommand(program);

// Load tools from cache and register commands dynamically
const toolsCache = loadToolsCache();
for (const tool of toolsCache.tools) {
  registerToolCommand(tool);
}

/**
 * Register a tool as a CLI command dynamically.
 */
function registerToolCommand(tool: ToolDefinition): void {
  const cmd = program.command(tool.name).description(tool.description);

  // Add options from inputSchema.properties
  const properties = tool.inputSchema.properties;
  for (const [propName, propInfo] of Object.entries(properties)) {
    const optionStr = generateOptionString(propName, propInfo);
    const description = buildOptionDescription(propInfo);
    const defaultValue = propInfo.default as string | boolean | undefined;
    if (defaultValue !== undefined) {
      cmd.option(optionStr, description, defaultValue);
    } else {
      cmd.option(optionStr, description);
    }
  }

  // Add --code-base64 and --stdin options for execute-dynamic-code command
  if (tool.name === 'execute-dynamic-code') {
    cmd.option(
      '--code-base64 <base64>',
      'Base64 encoded code (alternative to --code for shell escaping issues)',
    );
    cmd.option('--stdin', 'Read code from standard input');
  }

  // Add global options
  cmd.option('-p, --port <port>', 'Unity TCP port');

  cmd.action(async (options: CliOptions) => {
    const params = buildParams(options, properties);

    // Handle --stdin for execute-dynamic-code
    if (tool.name === 'execute-dynamic-code' && options['stdin']) {
      const stdinCode = await readStdin();
      params['Code'] = stdinCode;
    }

    // Handle --code-base64 for execute-dynamic-code
    if (tool.name === 'execute-dynamic-code' && options['codeBase64']) {
      const base64Code = options['codeBase64'] as string;
      const decodedCode = Buffer.from(base64Code, 'base64').toString('utf-8');
      params['Code'] = decodedCode;
    }

    await runWithErrorHandling(() =>
      executeToolCommand(tool.name, params, extractGlobalOptions(options)),
    );
  });
}

/**
 * Generate commander.js option string from property info.
 */
function generateOptionString(propName: string, propInfo: ToolProperty): string {
  const kebabName = pascalToKebabCase(propName);
  const lowerType = propInfo.type.toLowerCase();

  if (lowerType === 'boolean') {
    return `--${kebabName}`;
  }

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

  if (lowerType === 'array' && typeof value === 'string') {
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

  return value;
}

function extractGlobalOptions(options: Record<string, unknown>): GlobalOptions {
  return {
    port: options['port'] as string | undefined,
  };
}

/**
 * Read code from standard input.
 */
function readStdin(): Promise<string> {
  return new Promise((resolve, reject) => {
    let data = '';
    process.stdin.setEncoding('utf-8');
    process.stdin.on('data', (chunk) => {
      data += chunk;
    });
    process.stdin.on('end', () => {
      resolve(data);
    });
    process.stdin.on('error', (err) => {
      reject(err);
    });
  });
}

function isDomainReloadLockFilePresent(): boolean {
  const lockPath = join(process.cwd(), 'Temp', 'domainreload.lock');
  return existsSync(lockPath);
}

async function runWithErrorHandling(fn: () => Promise<void>): Promise<void> {
  try {
    await fn();
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);

    if (message.includes('ECONNREFUSED')) {
      if (isDomainReloadLockFilePresent()) {
        console.error('\x1b[33m⏳ Unity is reloading (Domain Reload in progress).\x1b[0m');
        console.error('Please wait a moment and try again.');
      } else {
        console.error('\x1b[31mError: Cannot connect to Unity.\x1b[0m');
        console.error('Make sure Unity is running with uLoopMCP installed.');
      }
      process.exit(1);
    }

    console.error(`\x1b[31mError: ${message}\x1b[0m`);
    process.exit(1);
  }
}

/**
 * Detect shell type from $SHELL environment variable.
 */
function detectShell(): 'bash' | 'zsh' | null {
  const shell = process.env['SHELL'] || '';
  const shellName = basename(shell);
  if (shellName === 'zsh') {
    return 'zsh';
  }
  if (shellName === 'bash') {
    return 'bash';
  }
  return null;
}

/**
 * Get shell config file path.
 */
function getShellConfigPath(shell: 'bash' | 'zsh'): string {
  const home = homedir();
  return shell === 'zsh' ? join(home, '.zshrc') : join(home, '.bashrc');
}

/**
 * Get completion script for a shell.
 */
function getCompletionScript(shell: 'bash' | 'zsh'): string {
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
 * Update uloop CLI to the latest version using npm.
 */
function updateCli(): void {
  // eslint-disable-next-line no-console
  console.log('Updating uloop-cli to the latest version...');

  const npmCommand = process.platform === 'win32' ? 'npm.cmd' : 'npm';
  const child = spawn(npmCommand, ['install', '-g', 'uloop-cli@latest'], {
    stdio: 'inherit',
    shell: true,
  });

  child.on('close', (code) => {
    if (code === 0) {
      // eslint-disable-next-line no-console
      console.log('\n✅ uloop-cli has been updated successfully!');
      // eslint-disable-next-line no-console
      console.log('Run "uloop --version" to check the new version.');
    } else {
      // eslint-disable-next-line no-console
      console.error(`\n❌ Update failed with exit code ${code}`);
      process.exit(1);
    }
  });

  child.on('error', (err) => {
    // eslint-disable-next-line no-console
    console.error(`❌ Failed to run npm: ${err.message}`);
    process.exit(1);
  });
}

/**
 * Handle completion command.
 */
function handleCompletion(install: boolean): void {
  const shell = detectShell();
  if (!shell) {
    console.error('Could not detect shell from $SHELL. Supported: bash, zsh');
    process.exit(1);
  }

  const script = getCompletionScript(shell);

  if (!install) {
    console.log(script);
    return;
  }

  // Install to shell config file
  const configPath = getShellConfigPath(shell);
  const evalLine = 'eval "$(uloop completion)"';

  // Check if already installed
  if (existsSync(configPath)) {
    const content = readFileSync(configPath, 'utf-8');
    if (content.includes('uloop completion')) {
      console.log(`Completion already installed in ${configPath}`);
      return;
    }
  }

  // Append to config file
  const lineToAdd = `\n# uloop CLI completion\n${evalLine}\n`;
  appendFileSync(configPath, lineToAdd, 'utf-8');
  console.log(`Completion installed to ${configPath}`);
  console.log(`Run 'source ${configPath}' or restart your shell to enable completion.`);
}

/**
 * Handle --list-commands and --list-options before parsing.
 */
function handleCompletionOptions(): boolean {
  const args = process.argv.slice(2);

  if (args.includes('--list-commands')) {
    const tools = loadToolsCache();
    const builtinCommands = ['list', 'sync', 'completion', 'update', 'skills'];
    const allCommands = [...builtinCommands, ...tools.tools.map((t) => t.name)];
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
  if (
    cmdName === 'list' ||
    cmdName === 'sync' ||
    cmdName === 'completion' ||
    cmdName === 'update'
  ) {
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

// Handle completion options first (before commander parsing)
if (!handleCompletionOptions()) {
  program.parse();
}
