/**
 * Tool execution logic for CLI.
 * Handles dynamic tool execution by connecting to Unity and sending requests.
 */

import { DirectUnityClient } from './direct-unity-client.js';
import { resolveUnityPort } from './port-resolver.js';
import { formatOutput } from './output-formatter.js';

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
