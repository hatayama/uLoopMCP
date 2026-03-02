/**
 * CLI command for listing connected physical devices.
 * Queries ADB for Android devices and idevice_id for iOS devices (future).
 */

// CLI commands output to console by design
/* eslint-disable no-console */

import { execSync } from 'child_process';
import { Command } from 'commander';

interface DeviceInfo {
  serial: string;
  model: string;
  status: string;
  platform: string;
}

export function registerDeviceListCommand(program: Command): void {
  program
    .command('device-list')
    .description('List connected physical devices (Android via ADB, iOS planned)')
    .action(() => {
      runDeviceList();
    });
}

function runDeviceList(): void {
  const devices: DeviceInfo[] = listAndroidDevices();

  if (devices.length === 0) {
    console.log('No devices found.');
    console.log('Make sure a device is connected and USB debugging is enabled.');
    return;
  }

  console.log(`Found ${devices.length} device(s):\n`);

  const serialWidth: number = Math.max('SERIAL'.length, ...devices.map((d) => d.serial.length));
  const modelWidth: number = Math.max('MODEL'.length, ...devices.map((d) => d.model.length));
  const statusWidth: number = Math.max('STATUS'.length, ...devices.map((d) => d.status.length));
  const platformWidth: number = Math.max(
    'PLATFORM'.length,
    ...devices.map((d) => d.platform.length),
  );

  const header: string = [
    'SERIAL'.padEnd(serialWidth),
    'MODEL'.padEnd(modelWidth),
    'STATUS'.padEnd(statusWidth),
    'PLATFORM'.padEnd(platformWidth),
  ].join('  ');

  const separator: string = [
    '-'.repeat(serialWidth),
    '-'.repeat(modelWidth),
    '-'.repeat(statusWidth),
    '-'.repeat(platformWidth),
  ].join('  ');

  console.log(header);
  console.log(separator);

  for (const device of devices) {
    const row: string = [
      device.serial.padEnd(serialWidth),
      device.model.padEnd(modelWidth),
      device.status.padEnd(statusWidth),
      device.platform.padEnd(platformWidth),
    ].join('  ');
    console.log(row);
  }
}

function listAndroidDevices(): DeviceInfo[] {
  let output: string;
  try {
    output = execSync('adb devices -l', { encoding: 'utf-8', timeout: 10000 });
  } catch {
    return [];
  }

  const lines: string[] = output.split('\n');
  const devices: DeviceInfo[] = [];

  for (const line of lines) {
    // Skip the header line ("List of devices attached") and empty lines
    if (line.startsWith('List of') || line.trim().length === 0) {
      continue;
    }

    const parsed: DeviceInfo | null = parseAdbDeviceLine(line);
    if (parsed !== null) {
      devices.push(parsed);
    }
  }

  return devices;
}

function parseAdbDeviceLine(line: string): DeviceInfo | null {
  // Format: "<serial> <status> <properties...>"
  // Example: "ABCDEF123456 device usb:1-1 product:oriole model:Pixel_6 device:oriole transport_id:1"
  const parts: string[] = line.trim().split(/\s+/);
  if (parts.length < 2) {
    return null;
  }

  const serial: string = parts[0];
  const status: string = parts[1];

  let model: string = 'unknown';
  for (const part of parts.slice(2)) {
    if (part.startsWith('model:')) {
      model = part.substring('model:'.length);
      break;
    }
  }

  return {
    serial,
    model,
    status,
    platform: 'android',
  };
}
