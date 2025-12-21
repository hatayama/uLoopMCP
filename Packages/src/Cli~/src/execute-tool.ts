/**
 * Tool execution logic for CLI.
 * Handles dynamic tool execution by connecting to Unity and sending requests.
 */

// CLI tools output to console by design, and object keys come from Unity tool responses which are trusted
/* eslint-disable no-console, security/detect-object-injection */

import * as readline from 'readline';
import { DirectUnityClient } from './direct-unity-client.js';
import { resolveUnityPort } from './port-resolver.js';
import { saveToolsCache, getCacheFilePath, ToolsCache, ToolDefinition } from './tool-cache.js';
import { VERSION } from './version.js';
import { createSpinner } from './spinner.js';

/**
 * Suppress stdin echo during async operation to prevent escape sequences from being displayed.
 * Returns a cleanup function to restore stdin state.
 */
function suppressStdinEcho(): () => void {
  if (!process.stdin.isTTY) {
    return () => {};
  }

  const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout,
    terminal: false,
  });

  process.stdin.setRawMode(true);
  process.stdin.resume();
  process.stdin.on('data', () => {});

  return () => {
    process.stdin.setRawMode(false);
    process.stdin.pause();
    rl.close();
  };
}

export interface GlobalOptions {
  port?: string;
}

export async function executeToolCommand(
  toolName: string,
  params: Record<string, unknown>,
  globalOptions: GlobalOptions,
): Promise<void> {
  let portNumber: number | undefined;
  if (globalOptions.port) {
    const parsed = parseInt(globalOptions.port, 10);
    if (isNaN(parsed)) {
      throw new Error(`Invalid port number: ${globalOptions.port}`);
    }
    portNumber = parsed;
  }
  const port = await resolveUnityPort(portNumber);

  const client = new DirectUnityClient(port);
  const restoreStdin = suppressStdinEcho();
  const spinner = createSpinner('Connecting to Unity...');

  try {
    await client.connect();

    spinner.update(`Executing ${toolName}...`);
    const result = await client.sendRequest(toolName, params);

    spinner.stop();
    // Always output JSON to match MCP response format
    console.log(JSON.stringify(result, null, 2));
  } finally {
    spinner.stop();
    restoreStdin();
    client.disconnect();
  }
}

export async function listAvailableTools(globalOptions: GlobalOptions): Promise<void> {
  let portNumber: number | undefined;
  if (globalOptions.port) {
    const parsed = parseInt(globalOptions.port, 10);
    if (isNaN(parsed)) {
      throw new Error(`Invalid port number: ${globalOptions.port}`);
    }
    portNumber = parsed;
  }
  const port = await resolveUnityPort(portNumber);

  const client = new DirectUnityClient(port);
  const restoreStdin = suppressStdinEcho();
  const spinner = createSpinner('Connecting to Unity...');

  try {
    await client.connect();

    spinner.update('Fetching tool list...');
    const result = await client.sendRequest<{
      Tools: Array<{ name: string; description: string }>;
    }>('get-tool-details', { IncludeDevelopmentOnly: false });

    spinner.stop();
    if (!result.Tools || !Array.isArray(result.Tools)) {
      throw new Error('Unexpected response from Unity: missing Tools array');
    }

    for (const tool of result.Tools) {
      console.log(`  - ${tool.name}`);
    }
  } finally {
    spinner.stop();
    restoreStdin();
    client.disconnect();
  }
}

interface UnityToolInfo {
  name: string;
  description: string;
  parameterSchema: {
    Properties: Record<string, UnityPropertyInfo>;
    Required?: string[];
  };
}

interface UnityPropertyInfo {
  Type: string;
  Description?: string;
  DefaultValue?: unknown;
  Enum?: string[] | null;
}

function convertProperties(
  unityProps: Record<string, UnityPropertyInfo>,
): Record<string, ToolDefinition['inputSchema']['properties'][string]> {
  const result: Record<string, ToolDefinition['inputSchema']['properties'][string]> = {};
  for (const [key, prop] of Object.entries(unityProps)) {
    result[key] = {
      type: prop.Type?.toLowerCase() ?? 'string',
      description: prop.Description,
      default: prop.DefaultValue,
      enum: prop.Enum ?? undefined,
    };
  }
  return result;
}

export async function syncTools(globalOptions: GlobalOptions): Promise<void> {
  let portNumber: number | undefined;
  if (globalOptions.port) {
    const parsed = parseInt(globalOptions.port, 10);
    if (isNaN(parsed)) {
      throw new Error(`Invalid port number: ${globalOptions.port}`);
    }
    portNumber = parsed;
  }
  const port = await resolveUnityPort(portNumber);

  const client = new DirectUnityClient(port);
  const restoreStdin = suppressStdinEcho();
  const spinner = createSpinner('Connecting to Unity...');

  try {
    await client.connect();

    spinner.update('Syncing tools...');
    const result = await client.sendRequest<{
      Tools: UnityToolInfo[];
    }>('get-tool-details', { IncludeDevelopmentOnly: false });

    spinner.stop();
    if (!result.Tools || !Array.isArray(result.Tools)) {
      throw new Error('Unexpected response from Unity: missing Tools array');
    }

    const cache: ToolsCache = {
      version: VERSION,
      updatedAt: new Date().toISOString(),
      tools: result.Tools.map((tool) => ({
        name: tool.name,
        description: tool.description,
        inputSchema: {
          type: 'object',
          properties: convertProperties(tool.parameterSchema.Properties),
          required: tool.parameterSchema.Required,
        },
      })),
    };

    saveToolsCache(cache);

    console.log(`Synced ${cache.tools.length} tools to ${getCacheFilePath()}`);
    console.log('\nTools:');
    for (const tool of cache.tools) {
      console.log(`  - ${tool.name}`);
    }
  } finally {
    spinner.stop();
    restoreStdin();
    client.disconnect();
  }
}
