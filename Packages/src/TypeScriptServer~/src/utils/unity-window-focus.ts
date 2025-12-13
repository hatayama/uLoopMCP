/**
 * Unity Window Focus Utility
 *
 * OS-level utility for focusing Unity Editor windows.
 * Works independently of Unity's TCP server (useful during Domain Reload).
 *
 * Based on: https://github.com/hatayama/LaunchUnityCommand
 *
 * Supported platforms:
 * - macOS: Uses osascript with System Events
 * - Windows: Uses PowerShell with Win32 API (SetForegroundWindow)
 */

import { execFile } from 'node:child_process';
import { resolve } from 'node:path';
import { promisify } from 'node:util';
import { VibeLogger } from './vibe-logger.js';

const execFileAsync = promisify(execFile);

// Unity process detection patterns
const UNITY_EXECUTABLE_PATTERN_MAC = /Unity\.app\/Contents\/MacOS\/Unity/i;
const UNITY_EXECUTABLE_PATTERN_WINDOWS = /Unity\.exe/i;
const PROJECT_PATH_PATTERN = /-(?:projectPath|projectpath)(?:=|\s+)("[^"]+"|'[^']+'|[^\s"']+)/i;

// Process listing commands
const PROCESS_LIST_COMMAND_MAC = 'ps';
const PROCESS_LIST_ARGS_MAC = ['-axo', 'pid=,command=', '-ww'];
const WINDOWS_POWERSHELL = 'powershell';

/**
 * Information about a running Unity process
 */
export type UnityProcessInfo = {
  pid: number;
  projectPath: string;
};

/**
 * Result of a focus operation
 */
export type FocusResult = {
  success: boolean;
  message: string;
};

/**
 * Normalize path for consistent comparison
 */
const normalizePath = (target: string): string => {
  const resolvedPath = resolve(target);
  let trimmed = resolvedPath;
  while (trimmed.length > 1 && (trimmed.endsWith('/') || trimmed.endsWith('\\'))) {
    trimmed = trimmed.slice(0, -1);
  }
  return trimmed;
};

/**
 * Convert path to comparable format (lowercase, forward slashes)
 */
const toComparablePath = (value: string): string => {
  return value.replace(/\\/g, '/').toLowerCase();
};

/**
 * Check if two paths are equal (case-insensitive, normalized)
 */
const pathsEqual = (left: string, right: string): boolean => {
  return toComparablePath(normalizePath(left)) === toComparablePath(normalizePath(right));
};

/**
 * Extract project path from Unity command line arguments
 */
const extractProjectPath = (command: string): string | undefined => {
  const match = command.match(PROJECT_PATH_PATTERN);
  if (!match) {
    return undefined;
  }

  const raw = match[1];
  if (!raw) {
    return undefined;
  }

  const trimmed = raw.trim();
  if (trimmed.startsWith('"') && trimmed.endsWith('"')) {
    return trimmed.slice(1, -1);
  }
  if (trimmed.startsWith("'") && trimmed.endsWith("'")) {
    return trimmed.slice(1, -1);
  }
  return trimmed;
};

/**
 * Check if command is an auxiliary Unity process (batchmode, asset import worker)
 */
const isUnityAuxiliaryProcess = (command: string): boolean => {
  const normalizedCommand = command.toLowerCase();
  if (normalizedCommand.includes('-batchmode')) {
    return true;
  }
  return normalizedCommand.includes('assetimportworker');
};

/**
 * List Unity processes on macOS
 */
async function listUnityProcessesMac(): Promise<UnityProcessInfo[]> {
  let stdout = '';
  try {
    const result = await execFileAsync(PROCESS_LIST_COMMAND_MAC, PROCESS_LIST_ARGS_MAC);
    stdout = result.stdout;
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    VibeLogger.logDebug(
      'unity_process_list_failed',
      `Failed to retrieve Unity process list: ${message}`,
      { platform: 'darwin' },
    );
    return [];
  }

  const lines = stdout
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => line.length > 0);

  const processes: UnityProcessInfo[] = [];

  for (const line of lines) {
    const match = line.match(/^(\d+)\s+(.*)$/);
    if (!match) {
      continue;
    }

    const pidValue = Number.parseInt(match[1] ?? '', 10);
    if (!Number.isFinite(pidValue)) {
      continue;
    }

    const command = match[2] ?? '';
    if (!UNITY_EXECUTABLE_PATTERN_MAC.test(command)) {
      continue;
    }
    if (isUnityAuxiliaryProcess(command)) {
      continue;
    }

    const projectArgument = extractProjectPath(command);
    if (!projectArgument) {
      continue;
    }

    processes.push({
      pid: pidValue,
      projectPath: normalizePath(projectArgument),
    });
  }

  return processes;
}

/**
 * List Unity processes on Windows
 */
async function listUnityProcessesWindows(): Promise<UnityProcessInfo[]> {
  // prettier-ignore
  const scriptLines: string[] = [
    "$ErrorActionPreference = 'Stop'",
    "$processes = Get-CimInstance Win32_Process -Filter \"Name = 'Unity.exe'\" | Where-Object { $_.CommandLine }",
    'foreach ($process in $processes) {',
    "  $commandLine = $process.CommandLine -replace \"`r\", ' ' -replace \"`n\", ' '",
    '  Write-Output ("{0}|{1}" -f $process.ProcessId, $commandLine)',
    '}',
  ];

  let stdout = '';
  try {
    const result = await execFileAsync(WINDOWS_POWERSHELL, [
      '-NoProfile',
      '-Command',
      scriptLines.join('\n'),
    ]);
    stdout = result.stdout ?? '';
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    VibeLogger.logDebug(
      'unity_process_list_failed',
      `Failed to retrieve Unity process list on Windows: ${message}`,
      { platform: 'win32' },
    );
    return [];
  }

  const lines = stdout
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => line.length > 0);

  const processes: UnityProcessInfo[] = [];

  for (const line of lines) {
    const delimiterIndex = line.indexOf('|');
    if (delimiterIndex < 0) {
      continue;
    }

    const pidText = line.slice(0, delimiterIndex).trim();
    const command = line.slice(delimiterIndex + 1).trim();

    const pidValue = Number.parseInt(pidText, 10);
    if (!Number.isFinite(pidValue)) {
      continue;
    }

    if (!UNITY_EXECUTABLE_PATTERN_WINDOWS.test(command)) {
      continue;
    }
    if (isUnityAuxiliaryProcess(command)) {
      continue;
    }

    const projectArgument = extractProjectPath(command);
    if (!projectArgument) {
      continue;
    }

    processes.push({
      pid: pidValue,
      projectPath: normalizePath(projectArgument),
    });
  }

  return processes;
}

/**
 * List all running Unity Editor processes (cross-platform)
 */
export async function listUnityProcesses(): Promise<UnityProcessInfo[]> {
  if (process.platform === 'darwin') {
    return await listUnityProcessesMac();
  }
  if (process.platform === 'win32') {
    return await listUnityProcessesWindows();
  }
  return [];
}

/**
 * Find Unity process for a specific project path
 */
export async function findUnityProcessByProjectPath(
  projectPath: string,
): Promise<UnityProcessInfo | undefined> {
  const normalizedTarget = normalizePath(projectPath);
  const processes = await listUnityProcesses();
  return processes.find((candidate) => pathsEqual(candidate.projectPath, normalizedTarget));
}

/**
 * Focus Unity window by PID on macOS
 */
async function focusUnityWindowMac(pid: number): Promise<FocusResult> {
  const script = `tell application "System Events" to set frontmost of (first process whose unix id is ${pid}) to true`;
  try {
    await execFileAsync('osascript', ['-e', script]);
    return { success: true, message: `Focused Unity window (PID: ${pid})` };
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    return { success: false, message: `Failed to focus Unity window: ${message}` };
  }
}

/**
 * Focus Unity window by PID on Windows
 */
async function focusUnityWindowWindows(pid: number): Promise<FocusResult> {
  const addTypeLines: string[] = [
    'Add-Type -TypeDefinition @"',
    'using System;',
    'using System.Runtime.InteropServices;',
    'public static class Win32Interop {',
    '  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);',
    '  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);',
    '}',
    '"@',
  ];

  const scriptLines: string[] = [
    "$ErrorActionPreference = 'Stop'",
    ...addTypeLines,
    `try { $process = Get-Process -Id ${pid} -ErrorAction Stop } catch { exit 1 }`,
    '$handle = $process.MainWindowHandle',
    'if ($handle -eq 0) { exit 1 }',
    '[Win32Interop]::ShowWindowAsync($handle, 9) | Out-Null',
    '[Win32Interop]::SetForegroundWindow($handle) | Out-Null',
  ];

  try {
    await execFileAsync(WINDOWS_POWERSHELL, ['-NoProfile', '-Command', scriptLines.join('\n')]);
    return { success: true, message: `Focused Unity window (PID: ${pid})` };
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    return { success: false, message: `Failed to focus Unity window on Windows: ${message}` };
  }
}

/**
 * Focus Unity window by PID (cross-platform)
 *
 * Uses OS-level commands to bring Unity to foreground.
 * Works even when Unity's TCP server is unavailable (e.g., during Domain Reload).
 */
export async function focusUnityWindowByPid(pid: number): Promise<FocusResult> {
  if (process.platform === 'darwin') {
    return await focusUnityWindowMac(pid);
  }
  if (process.platform === 'win32') {
    return await focusUnityWindowWindows(pid);
  }
  return { success: false, message: `Unsupported platform: ${process.platform}` };
}

/**
 * Focus Unity window by project path (cross-platform)
 *
 * Finds the Unity process for the given project and brings it to foreground.
 * Returns success:false if no matching process is found.
 */
export async function focusUnityWindowByProjectPath(projectPath: string): Promise<FocusResult> {
  const processInfo = await findUnityProcessByProjectPath(projectPath);
  if (!processInfo) {
    return {
      success: false,
      message: `No Unity process found for project: ${projectPath}`,
    };
  }
  return await focusUnityWindowByPid(processInfo.pid);
}

/**
 * Focus any running Unity window (first found)
 *
 * Useful when project path is unknown but we want to bring Unity to foreground.
 */
export async function focusAnyUnityWindow(): Promise<FocusResult> {
  const processes = await listUnityProcesses();
  if (processes.length === 0) {
    return { success: false, message: 'No running Unity processes found' };
  }

  const firstProcess = processes[0];
  if (!firstProcess) {
    return { success: false, message: 'No running Unity processes found' };
  }

  return await focusUnityWindowByPid(firstProcess.pid);
}
