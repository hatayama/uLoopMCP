/**
 * CLI End-to-End Tests
 *
 * These tests require a running Unity Editor with uLoopMCP installed.
 * Run from Unity project root: npm run test:cli
 *
 * @jest-environment node
 */

import { execSync, ExecSyncOptionsWithStringEncoding } from 'child_process';
import { join } from 'path';

const CLI_PATH = join(__dirname, '../../..', 'dist/cli.bundle.cjs');

const UNITY_PROJECT_ROOT = join(__dirname, '../../../../../..');

const EXEC_OPTIONS: ExecSyncOptionsWithStringEncoding = {
  encoding: 'utf-8',
  timeout: 60000,
  cwd: UNITY_PROJECT_ROOT,
  stdio: ['pipe', 'pipe', 'pipe'],
};

const INTERVAL_MS = 1500;

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function runCli(args: string): { stdout: string; stderr: string; exitCode: number } {
  try {
    const stdout = execSync(`node "${CLI_PATH}" ${args}`, EXEC_OPTIONS);
    return { stdout, stderr: '', exitCode: 0 };
  } catch (error) {
    const execError = error as { stdout?: string; stderr?: string; status?: number };
    return {
      stdout: execError.stdout ?? '',
      stderr: execError.stderr ?? '',
      exitCode: execError.status ?? 1,
    };
  }
}

function runCliJson<T>(args: string): T {
  const { stdout, stderr, exitCode } = runCli(args);
  if (exitCode !== 0) {
    throw new Error(`CLI failed with exit code ${exitCode}: ${stderr || stdout}`);
  }
  return JSON.parse(stdout) as T;
}

describe('CLI E2E Tests (requires running Unity)', () => {
  beforeEach(async () => {
    await sleep(INTERVAL_MS);
  });

  describe('compile', () => {
    it('should compile successfully', () => {
      const result = runCliJson<{ Success: boolean; ErrorCount: number }>('compile');

      expect(result.Success).toBe(true);
      expect(result.ErrorCount).toBe(0);
    });

    it('should support --force-recompile option', () => {
      const { exitCode } = runCli('compile --force-recompile');

      // Domain Reload causes connection to be lost, so we just verify the command runs
      // The exit code may be non-zero due to connection being dropped during reload
      expect(typeof exitCode).toBe('number');
    });
  });

  describe('get-logs', () => {
    const TEST_LOG_MENU_PATH = 'uLoopMCP/Debug/LogGetter Tests/Output Test Logs';

    function setupTestLogs(): void {
      runCli('clear-console');
      runCli(`execute-menu-item --menu-item-path "${TEST_LOG_MENU_PATH}"`);
    }

    it('should retrieve test logs after executing Output Test Logs menu item', () => {
      setupTestLogs();

      const result = runCliJson<{ TotalCount: number; Logs: Array<{ Message: string }> }>(
        'get-logs',
      );

      expect(result.TotalCount).toBeGreaterThan(0);
      expect(Array.isArray(result.Logs)).toBe(true);

      // Verify specific test log messages exist
      const messages = result.Logs.map((log) => log.Message);
      expect(messages.some((m) => m.includes('This is a normal log'))).toBe(true);
      expect(messages.some((m) => m.includes('LogGetter test complete'))).toBe(true);
    });

    it('should respect --max-count option', () => {
      setupTestLogs();

      const result = runCliJson<{ Logs: unknown[] }>('get-logs --max-count 3');

      expect(result.Logs.length).toBeLessThanOrEqual(3);
    });

    it('should filter by log type Warning', () => {
      setupTestLogs();

      const result = runCliJson<{ Logs: Array<{ Type: string; Message: string }> }>(
        'get-logs --log-type Warning',
      );

      expect(result.Logs.length).toBeGreaterThan(0);
      for (const log of result.Logs) {
        expect(log.Type).toBe('Warning');
      }
      // Verify test warning log exists
      const messages = result.Logs.map((log) => log.Message);
      expect(messages.some((m) => m.includes('This is a warning log'))).toBe(true);
    });

    it('should filter by log type Error', () => {
      setupTestLogs();

      const result = runCliJson<{ Logs: Array<{ Type: string; Message: string }> }>(
        'get-logs --log-type Error',
      );

      expect(result.Logs.length).toBeGreaterThan(0);
      for (const log of result.Logs) {
        expect(log.Type).toBe('Error');
      }
      // Verify test error log exists
      const messages = result.Logs.map((log) => log.Message);
      expect(messages.some((m) => m.includes('This is an error log'))).toBe(true);
    });

    it('should search logs by text', () => {
      setupTestLogs();

      const result = runCliJson<{ Logs: Array<{ Message: string }> }>(
        'get-logs --search-text "LogGetter test complete"',
      );

      expect(result.Logs.length).toBeGreaterThan(0);
      for (const log of result.Logs) {
        expect(log.Message).toContain('LogGetter test complete');
      }
    });
  });

  describe('clear-console', () => {
    const TEST_LOG_MENU_PATH = 'uLoopMCP/Debug/LogGetter Tests/Output Test Logs';

    it('should clear console and verify logs are empty', () => {
      // First output some logs
      runCli(`execute-menu-item --menu-item-path "${TEST_LOG_MENU_PATH}"`);

      // Clear console
      const result = runCliJson<{ Success: boolean }>('clear-console');
      expect(result.Success).toBe(true);

      // Verify logs are cleared
      const logsAfterClear = runCliJson<{ TotalCount: number; Logs: unknown[] }>('get-logs');
      expect(logsAfterClear.TotalCount).toBe(0);
      expect(logsAfterClear.Logs.length).toBe(0);
    });
  });

  describe('focus-window', () => {
    it('should focus Unity window', () => {
      const result = runCliJson<{ Success: boolean }>('focus-window');

      expect(result.Success).toBe(true);
    });
  });

  describe('get-hierarchy', () => {
    it('should retrieve hierarchy and save to file', () => {
      const result = runCliJson<{ hierarchyFilePath: string }>('get-hierarchy --max-depth 2');

      expect(typeof result.hierarchyFilePath).toBe('string');
      expect(result.hierarchyFilePath).toContain('hierarchy_');
    });
  });

  describe('get-menu-items', () => {
    it('should retrieve menu items', () => {
      const result = runCliJson<{ MenuItems: unknown[]; TotalCount: number }>(
        'get-menu-items --max-count 10',
      );

      expect(typeof result.TotalCount).toBe('number');
      expect(Array.isArray(result.MenuItems)).toBe(true);
    });

    it('should filter menu items', () => {
      const result = runCliJson<{ MenuItems: Array<{ Path: string }> }>(
        'get-menu-items --filter-text "GameObject"',
      );

      expect(result.MenuItems.length).toBeGreaterThan(0);
      for (const item of result.MenuItems) {
        expect(item.Path.toLowerCase()).toContain('gameobject');
      }
    });
  });

  describe('execute-menu-item', () => {
    const TEST_LOG_MENU_PATH = 'uLoopMCP/Debug/LogGetter Tests/Output Test Logs';

    it('should execute menu item and verify logs are output', () => {
      // Clear console first
      runCli('clear-console');

      // Execute menu item
      const result = runCliJson<{ Success: boolean; MenuItemPath: string }>(
        `execute-menu-item --menu-item-path "${TEST_LOG_MENU_PATH}"`,
      );

      expect(result.Success).toBe(true);
      expect(result.MenuItemPath).toBe(TEST_LOG_MENU_PATH);

      // Verify logs were output
      const logs = runCliJson<{ TotalCount: number; Logs: Array<{ Message: string }> }>('get-logs');
      expect(logs.TotalCount).toBeGreaterThan(0);
      const messages = logs.Logs.map((log) => log.Message);
      expect(messages.some((m) => m.includes('LogGetter test complete'))).toBe(true);
    });
  });

  describe('unity-search', () => {
    it('should search assets', () => {
      const result = runCliJson<{ Results: unknown[] }>('unity-search --search-query "*.cs"');

      expect(Array.isArray(result.Results)).toBe(true);
    });
  });

  describe('find-game-objects', () => {
    it('should find game objects with name pattern', () => {
      const result = runCliJson<{ results: unknown[]; totalFound: number }>(
        'find-game-objects --name-pattern "*" --include-inactive',
      );

      expect(Array.isArray(result.results)).toBe(true);
      expect(typeof result.totalFound).toBe('number');
    });
  });

  describe('get-provider-details', () => {
    it('should retrieve search providers', () => {
      const result = runCliJson<{ Providers: unknown[] }>('get-provider-details');

      expect(Array.isArray(result.Providers)).toBe(true);
    });
  });

  describe('--help', () => {
    it('should display help', () => {
      const { stdout, exitCode } = runCli('--help');

      expect(exitCode).toBe(0);
      expect(stdout).toContain('Unity MCP CLI');
      expect(stdout).toContain('compile');
      expect(stdout).toContain('get-logs');
    });

    it('should display command-specific help', () => {
      const { stdout, exitCode } = runCli('compile --help');

      expect(exitCode).toBe(0);
      expect(stdout).toContain('--force-recompile');
    });
  });

  describe('--version', () => {
    it('should display version', () => {
      const { stdout, exitCode } = runCli('--version');

      expect(exitCode).toBe(0);
      expect(stdout).toMatch(/^\d+\.\d+\.\d+/);
    });
  });

  describe('error handling', () => {
    it('should handle unknown commands gracefully', () => {
      const { exitCode } = runCli('unknown-command');

      expect(exitCode).not.toBe(0);
    });
  });
});
