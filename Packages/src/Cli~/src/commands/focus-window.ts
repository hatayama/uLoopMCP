/**
 * CLI command for focusing Unity Editor window.
 * Uses OS-level commands via launch-unity library.
 * Works even when Unity is busy (compiling, domain reload).
 */

// CLI commands output to console by design
/* eslint-disable no-console */

import { Command } from 'commander';
import { findRunningUnityProcess, focusUnityProcess } from 'launch-unity';
import { findUnityProjectRoot } from '../project-root.js';

export function registerFocusWindowCommand(program: Command): void {
  program
    .command('focus-window')
    .description('Bring Unity Editor window to front using OS-level commands')
    .action(async () => {
      const projectRoot = findUnityProjectRoot();
      if (projectRoot === null) {
        console.error(
          JSON.stringify({
            Success: false,
            Message: 'Unity project not found',
          }),
        );
        process.exit(1);
      }

      const runningProcess = await findRunningUnityProcess(projectRoot);
      if (!runningProcess) {
        console.error(
          JSON.stringify({
            Success: false,
            Message: 'No running Unity process found for this project',
          }),
        );
        process.exit(1);
      }

      try {
        await focusUnityProcess(runningProcess.pid);
        console.log(
          JSON.stringify({
            Success: true,
            Message: `Unity Editor window focused (PID: ${runningProcess.pid})`,
          }),
        );
      } catch (error) {
        console.error(
          JSON.stringify({
            Success: false,
            Message: `Failed to focus Unity window: ${error instanceof Error ? error.message : String(error)}`,
          }),
        );
        process.exit(1);
      }
    });
}
