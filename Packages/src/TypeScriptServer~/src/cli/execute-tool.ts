/**
 * Tool execution logic for CLI.
 * Handles dynamic tool execution by connecting to Unity and sending requests.
 */

import { DirectUnityClient } from './direct-unity-client.js';
import { resolveUnityPort } from './port-resolver.js';
import { saveToolsCache, getCacheFilePath, ToolsCache, ToolDefinition } from './tool-cache.js';
import { VERSION } from '../version.js';

export interface GlobalOptions {
  port?: string;
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

    // Always output JSON to match MCP response format
    console.log(JSON.stringify(result, null, 2));
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
      Tools: Array<{ name: string; description: string }>;
    }>('get-tool-details', { IncludeDevelopmentOnly: false });

    for (const tool of result.Tools) {
      console.log(`  - ${tool.name}`);
    }
  } finally {
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
  const portNumber = globalOptions.port ? parseInt(globalOptions.port, 10) : undefined;
  const port = await resolveUnityPort(portNumber);

  const client = new DirectUnityClient(port);

  try {
    await client.connect();

    const result = await client.sendRequest<{
      Tools: UnityToolInfo[];
    }>('get-tool-details', { IncludeDevelopmentOnly: false });

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
    client.disconnect();
  }
}
