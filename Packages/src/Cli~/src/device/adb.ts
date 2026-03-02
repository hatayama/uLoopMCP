/**
 * ADB (Android Debug Bridge) utility functions.
 * Provides device discovery, port forwarding, and app management for Android.
 */

/* eslint-disable no-console */

import { execFile } from 'child_process';
import { promisify } from 'util';

const execFileAsync = promisify(execFile);

export interface AdbDevice {
  serial: string;
  state: string;
  model?: string;
}

export async function assertAdbAvailable(): Promise<void> {
  const available: boolean = await isAdbAvailable();
  if (!available) {
    console.error('\x1b[31mError: adb not found. Install Android SDK Platform Tools.\x1b[0m');
    process.exit(1);
  }
}

export async function listDevices(): Promise<AdbDevice[]> {
  const { stdout } = await execFileAsync('adb', ['devices', '-l']);
  const lines: string[] = stdout.split('\n').slice(1);
  const devices: AdbDevice[] = [];

  for (const line of lines) {
    const trimmed: string = line.trim();
    if (trimmed.length === 0) {
      continue;
    }

    const parts: string[] = trimmed.split(/\s+/);
    if (parts.length < 2) {
      continue;
    }

    const serial: string = parts[0];
    const state: string = parts[1];

    let model: string | undefined;
    for (const part of parts) {
      if (part.startsWith('model:')) {
        model = part.substring('model:'.length);
        break;
      }
    }

    devices.push({ serial, state, model });
  }

  return devices;
}

export async function forwardPort(
  localPort: number,
  remotePort: number,
  serial?: string,
): Promise<void> {
  const args: string[] = serial
    ? ['-s', serial, 'forward', `tcp:${localPort}`, `tcp:${remotePort}`]
    : ['forward', `tcp:${localPort}`, `tcp:${remotePort}`];

  await execFileAsync('adb', args);
}

export async function installApk(apkPath: string, serial?: string): Promise<string> {
  const args: string[] = serial
    ? ['-s', serial, 'install', '-r', apkPath]
    : ['install', '-r', apkPath];

  const { stdout } = await execFileAsync('adb', args, { timeout: 120000 });
  return stdout.trim();
}

export async function launchApp(
  packageName: string,
  activityName?: string,
  serial?: string,
): Promise<string> {
  const component: string = activityName
    ? `${packageName}/${activityName}`
    : `${packageName}/com.unity3d.player.UnityPlayerActivity`;

  const args: string[] = serial
    ? ['-s', serial, 'shell', 'am', 'start', '-n', component]
    : ['shell', 'am', 'start', '-n', component];

  const { stdout } = await execFileAsync('adb', args);
  return stdout.trim();
}

export async function getLogcat(
  serial?: string,
  filter?: string,
  maxLines: number = 100,
): Promise<string> {
  const args: string[] = serial
    ? ['-s', serial, 'logcat', '-d', '-t', String(maxLines)]
    : ['logcat', '-d', '-t', String(maxLines)];

  if (filter) {
    args.push('-s', filter);
  }

  const { stdout } = await execFileAsync('adb', args, { timeout: 10000 });
  return stdout;
}

export async function isAdbAvailable(): Promise<boolean> {
  try {
    await execFileAsync('adb', ['version']);
    return true;
  } catch {
    return false;
  }
}
