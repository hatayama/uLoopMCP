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
import { validateProjectPath } from '../port-resolver.js';

export function registerFocusWindowCommand(program: Command): void {
  program
    .command('focus-window')
    .description('Bring Unity Editor window to front using OS-level commands')
    .option('--project-path <path>', 'Unity project path')
    .action(async (options: { projectPath?: string }) => {
      let projectRoot: string | null;
      if (options.projectPath !== undefined) {
        try {
          projectRoot = validateProjectPath(options.projectPath);
        } catch (error) {
          console.error(
            JSON.stringify({
              Success: false,
              Message: error instanceof Error ? error.message : String(error),
            }),
          );
          process.exit(1);
          return;
        }
      } else {
        projectRoot = findUnityProjectRoot();
      }
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
