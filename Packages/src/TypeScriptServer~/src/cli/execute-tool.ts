/**
 * Tool execution logic for CLI.
 * Handles dynamic tool execution by connecting to Unity and sending requests.
 */

import { DirectUnityClient } from './direct-unity-client.js';
import { resolveUnityPort } from './port-resolver.js';
import { formatOutput } from './output-formatter.js';
import { saveToolsCache, getCacheFilePath, ToolsCache, ToolDefinition } from './tool-cache.js';

export interface GlobalOptions {
  port?: string;
  json?: boolean;
}

export async function executeToolCommand(
  toolName: string,
  params: Record<string, unknown>,
  globalOptions: GlobalOptions,
): Promise<void> {
  const portNumber = globalOptions.port ? parseInt(globalOptions.port, 10) : undefined;
  const port = await resolveUnityPort(portNumber);

  const client = new DirectUnityClient(port);

  try {
    await client.connect();

    const result = await client.sendRequest(toolName, params);

    if (globalOptions.json) {
      console.log(JSON.stringify(result, null, 2));
    } else {
      formatOutput(toolName, result);
    }
  } finally {
    client.disconnect();
  }
}

export async function listAvailableTools(globalOptions: GlobalOptions): Promise<void> {
  const portNumber = globalOptions.port ? parseInt(globalOptions.port, 10) : undefined;
  const port = await resolveUnityPort(portNumber);

  const client = new DirectUnityClient(port);

  try {
    await client.connect();

    const result = await client.sendRequest<{
      tools: Array<{ name: string; description: string }>;
    }>('tools/list', {});

    if (globalOptions.json) {
      console.log(JSON.stringify(result, null, 2));
      return;
    }

    console.log('Available tools:\n');
    for (const tool of result.tools) {
      const name = tool.name.padEnd(25);
      console.log(`  ${name} ${tool.description}`);
    }
  } finally {
    client.disconnect();
  }
}

interface UnityToolInfo {
  name: string;
  description: string;
  inputSchema: {
    type: string;
    properties: Record<string, unknown>;
    required?: string[];
  };
}

export async function syncTools(globalOptions: GlobalOptions): Promise<void> {
  const portNumber = globalOptions.port ? parseInt(globalOptions.port, 10) : undefined;
  const port = await resolveUnityPort(portNumber);

  const client = new DirectUnityClient(port);

  try {
    await client.connect();

    const result = await client.sendRequest<{
      tools: UnityToolInfo[];
    }>('tools/list', {});

    const cache: ToolsCache = {
      version: '0.43.11',
      updatedAt: new Date().toISOString(),
      tools: result.tools.map((tool) => ({
        name: tool.name,
        description: tool.description,
        inputSchema: {
          type: tool.inputSchema.type || 'object',
          properties: tool.inputSchema.properties as Record<
            string,
            ToolDefinition['inputSchema']['properties'][string]
          >,
          required: tool.inputSchema.required,
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
    client.disconnect();
  }
}
