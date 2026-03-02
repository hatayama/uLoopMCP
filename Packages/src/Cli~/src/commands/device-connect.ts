/**
 * CLI command for connecting to a Device Agent on an Android device.
 * Sets up ADB port forwarding and authenticates with the Device Agent.
 */

/* eslint-disable no-console */

import { Command } from 'commander';
import { forwardPort, assertAdbAvailable } from '../device/adb.js';
import { DeviceClient, resolveDeviceToken } from '../device/device-client.js';
import { DEVICE_AGENT_PORT } from '../device/device-constants.js';

interface DeviceConnectOptions {
  token?: string;
  serial?: string;
  port?: string;
  skipForward?: boolean;
}

export function registerDeviceConnectCommand(program: Command): void {
  program
    .command('device-connect')
    .description('Connect to Device Agent on an Android device via ADB')
    .option('--token <token>', 'Auth token (or set ULOOP_DEVICE_TOKEN env var)')
    .option('--serial <serial>', 'ADB device serial (for multi-device)')
    .option('-p, --port <port>', 'Device Agent port', String(DEVICE_AGENT_PORT))
    .option('--skip-forward', 'Skip ADB port forwarding (already set up)')
    .action(async (options: DeviceConnectOptions) => {
      const port: number = parseInt(options.port ?? String(DEVICE_AGENT_PORT), 10);
      const token: string = options.token ?? resolveDeviceToken();

      if (!options.skipForward) {
        await assertAdbAvailable();

        console.log(`Setting up ADB port forward: tcp:${port} -> tcp:${port}`);
        await forwardPort(port, port, options.serial);
      }

      console.log(`Connecting to Device Agent on port ${port}...`);
      const client: DeviceClient = new DeviceClient(port);

      const authResponse = await client.connect(token);
      client.disconnect();

      console.log('\x1b[32mConnected successfully!\x1b[0m');
      console.log(JSON.stringify(authResponse, null, 2));
    });
}
