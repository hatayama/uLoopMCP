/**
 * CLI command for getting device logs from an Android device.
 */

/* eslint-disable no-console */

import { Command } from 'commander';
import { getLogcat, assertAdbAvailable } from '../device/adb.js';

interface DeviceLogsOptions {
  serial?: string;
  filter?: string;
  maxLines?: string;
}

export function registerDeviceLogsCommand(program: Command): void {
  program
    .command('device-logs')
    .description('Get logs from an Android device (logcat)')
    .option('--serial <serial>', 'ADB device serial (for multi-device)')
    .option('--filter <tag>', 'Logcat tag filter (e.g., Unity)')
    .option('--max-lines <n>', 'Maximum number of lines', '100')
    .action(async (options: DeviceLogsOptions) => {
      await assertAdbAvailable();

      const maxLines: number = parseInt(options.maxLines ?? '100', 10);
      const logs: string = await getLogcat(options.serial, options.filter, maxLines);
      console.log(logs);
    });
}
