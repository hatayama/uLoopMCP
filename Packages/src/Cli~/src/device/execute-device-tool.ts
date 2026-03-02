/**
 * Generic Device Agent tool executor.
 * Connects to device, authenticates, executes tool, and prints result.
 */

/* eslint-disable no-console */

import { DeviceClient, resolveDeviceToken } from './device-client.js';
import { DEVICE_AGENT_PORT } from './device-constants.js';

export interface DeviceToolGlobalOptions {
  token?: string;
  port?: string;
}

export async function executeDeviceTool(
  toolName: string,
  params: Record<string, unknown>,
  globalOptions: DeviceToolGlobalOptions,
): Promise<void> {
  const port: number = parseInt(globalOptions.port ?? String(DEVICE_AGENT_PORT), 10);
  const token: string = globalOptions.token ?? resolveDeviceToken();

  const client: DeviceClient = new DeviceClient(port);

  await client.connect(token);
  try {
    const result: Record<string, unknown> = await client.sendToolRequest<Record<string, unknown>>(
      toolName,
      params,
    );
    console.log(JSON.stringify(result, null, 2));
  } finally {
    client.disconnect();
  }
}
