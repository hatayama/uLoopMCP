/**
 * CLI command for launching an app on an Android device.
 */

/* eslint-disable no-console */

import { Command } from 'commander';
import { launchApp, assertAdbAvailable } from '../device/adb.js';

interface DeviceLaunchOptions {
  activity?: string;
  serial?: string;
}

export function registerDeviceLaunchCommand(program: Command): void {
  program
    .command('device-launch')
    .description('Launch an app on an Android device')
    .argument('<package-name>', 'Android package name (e.g., com.example.myapp)')
    .option('--activity <activity>', 'Activity name (default: UnityPlayerActivity)')
    .option('--serial <serial>', 'ADB device serial (for multi-device)')
    .action(async (packageName: string, options: DeviceLaunchOptions) => {
      await assertAdbAvailable();

      console.log(`Launching ${packageName}...`);
      const result: string = await launchApp(packageName, options.activity, options.serial);
      console.log(result);
    });
}
