/**
 * CLI command for connecting to a Device Agent on a physical device.
 * Sets up USB port forwarding and establishes an authenticated connection.
 */

// CLI commands output to console by design
/* eslint-disable no-console */

import assert from 'node:assert';
import { execSync } from 'child_process';
import { Command } from 'commander';
import { DeviceClient, DEVICE_DEFAULT_PORT } from '../device-client.js';

type Platform = 'android' | 'ios';

interface DeviceConnectOptions {
  port?: string;
  token?: string;
  platform?: string;
}

export function registerDeviceConnectCommand(program: Command): void {
  program
    .command('device-connect')
    .description('Connect to a Device Agent running on a physical device over USB')
    .option('--port <port>', `Device Agent port (default: ${DEVICE_DEFAULT_PORT})`)
    .option('--token <token>', 'Authentication token for Device Agent')
    .option('--platform <platform>', 'Device platform: android or ios (default: auto-detect)')
    .action(async (options: DeviceConnectOptions) => {
      await runDeviceConnect(options);
    });
}

function parsePort(portStr: string | undefined): number {
  if (portStr === undefined) {
    return DEVICE_DEFAULT_PORT;
  }
  const parsed: number = parseInt(portStr, 10);
  if (Number.isNaN(parsed) || parsed < 1 || parsed > 65535) {
    console.error(`Error: Invalid port "${portStr}". Must be an integer between 1 and 65535.`);
    process.exit(1);
  }
  return parsed;
}

function detectPlatform(): Platform {
  const adbAvailable: boolean = isCommandAvailable('adb');
  if (adbAvailable) {
    return 'android';
  }

  const iproxyAvailable: boolean = isCommandAvailable('iproxy');
  if (iproxyAvailable) {
    return 'ios';
  }

  console.error('Error: Could not detect device platform.');
  console.error('Make sure either "adb" (Android) or "iproxy" (iOS) is available on your PATH.');
  process.exit(1);
}

function resolvePlatform(platformStr: string | undefined): Platform {
  if (platformStr === undefined) {
    return detectPlatform();
  }

  const normalized: string = platformStr.toLowerCase();
  if (normalized === 'android' || normalized === 'ios') {
    return normalized;
  }

  console.error(`Error: Invalid platform "${platformStr}". Must be "android" or "ios".`);
  process.exit(1);
}

function isCommandAvailable(command: string): boolean {
  const whichCmd: string = process.platform === 'win32' ? 'where' : 'which';
  try {
    execSync(`${whichCmd} ${command}`, { stdio: 'ignore' });
    return true;
  } catch {
    return false;
  }
}

function setupAndroidPortForward(port: number): void {
  console.log(`Setting up ADB port forwarding: tcp:${port} -> tcp:${port}`);
  try {
    execSync(`adb forward tcp:${port} tcp:${port}`, { stdio: 'inherit' });
  } catch {
    console.error('Error: Failed to set up ADB port forwarding.');
    console.error('Make sure a device is connected and ADB is working: adb devices');
    process.exit(1);
  }
}

function setupIosPortForward(port: number): void {
  // iproxy runs as a background process; full implementation deferred to future work
  console.log(`iOS port forwarding via iproxy is not yet fully supported.`);
  console.log(`To manually forward: iproxy ${port} ${port}`);
  console.log('Attempting to connect assuming port forwarding is already set up...');
}

async function runDeviceConnect(options: DeviceConnectOptions): Promise<void> {
  const port: number = parsePort(options.port);
  const platform: Platform = resolvePlatform(options.platform);
  const token: string = options.token ?? '';

  assert(port >= 1 && port <= 65535, 'port must be in valid range');

  if (platform === 'android') {
    setupAndroidPortForward(port);
  } else {
    setupIosPortForward(port);
  }

  console.log(`\nConnecting to Device Agent at 127.0.0.1:${port}...`);

  const client = new DeviceClient(port);

  await client.connect();
  console.log('TCP connection established.');

  if (token.length > 0) {
    console.log('Authenticating...');
    const authResult = await client.authenticate(token);
    console.log('Authentication successful.');

    if (authResult.capabilities && Object.keys(authResult.capabilities).length > 0) {
      console.log('\nDevice capabilities:');
      for (const [key, value] of Object.entries(authResult.capabilities)) {
        console.log(`  ${key}: ${JSON.stringify(value)}`);
      }
    }
  } else {
    console.log('No token provided, skipping authentication.');
  }

  console.log(`\nConnected to ${platform} Device Agent on port ${port}.`);

  client.disconnect();
}
