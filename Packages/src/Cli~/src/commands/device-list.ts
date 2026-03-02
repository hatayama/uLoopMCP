/**
 * CLI command for listing connected Android devices.
 */

/* eslint-disable no-console */

import { Command } from 'commander';
import { listDevices, assertAdbAvailable } from '../device/adb.js';

export function registerDeviceListCommand(program: Command): void {
  program
    .command('device-list')
    .description('List connected Android devices')
    .action(async () => {
      await assertAdbAvailable();

      const devices = await listDevices();

      if (devices.length === 0) {
        console.log('No devices connected.');
        return;
      }

      console.log(`Found ${devices.length} device(s):\n`);
      for (const device of devices) {
        const model: string = device.model ? ` (${device.model})` : '';
        console.log(`  ${device.serial}  ${device.state}${model}`);
      }
    });
}
