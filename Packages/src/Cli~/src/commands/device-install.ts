/**
 * CLI command for installing APK to an Android device.
 */

/* eslint-disable no-console */

import { Command } from 'commander';
import { installApk, assertAdbAvailable } from '../device/adb.js';

interface DeviceInstallOptions {
  serial?: string;
}

export function registerDeviceInstallCommand(program: Command): void {
  program
    .command('device-install')
    .description('Install APK to an Android device')
    .argument('<apk-path>', 'Path to APK file')
    .option('--serial <serial>', 'ADB device serial (for multi-device)')
    .action(async (apkPath: string, options: DeviceInstallOptions) => {
      await assertAdbAvailable();

      console.log(`Installing ${apkPath}...`);
      const result: string = await installApk(apkPath, options.serial);
      console.log(result);
    });
}
