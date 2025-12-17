/**
 * Output formatter for CLI results.
 * Formats tool results for human-readable console output.
 */

export interface CompileResult {
  Success: boolean;
  ErrorCount: number;
  WarningCount: number;
  Errors?: Array<{ Message: string; File?: string; Line?: number }>;
  Warnings?: Array<{ Message: string; File?: string; Line?: number }>;
}

export interface LogEntry {
  Message: string;
  Type: string;
  StackTrace?: string;
}

export interface LogsResult {
  Logs: LogEntry[];
  TotalCount: number;
}

export interface TestResult {
  Passed: boolean;
  TestCount: number;
  PassedCount: number;
  FailedCount: number;
  SkippedCount: number;
  Duration?: number;
  FailedTests?: Array<{ Name: string; Message: string }>;
}

export function formatOutput(toolName: string, result: unknown): void {
  const formatter = formatters[toolName];
  if (formatter) {
    formatter(result);
    return;
  }

  defaultFormatter(result);
}

function formatCompile(result: unknown): void {
  const compileResult = result as CompileResult;

  if (compileResult.Success) {
    console.log('\x1b[32m✓ Compilation successful\x1b[0m');
  } else {
    console.log('\x1b[31m✗ Compilation failed\x1b[0m');
  }

  console.log(`  Errors: ${compileResult.ErrorCount}, Warnings: ${compileResult.WarningCount}`);

  if (compileResult.Errors && compileResult.Errors.length > 0) {
    console.log('\n\x1b[31mErrors:\x1b[0m');
    for (const error of compileResult.Errors) {
      const location = error.File ? ` (${error.File}:${error.Line ?? '?'})` : '';
      console.log(`  • ${error.Message}${location}`);
    }
  }

  if (compileResult.Warnings && compileResult.Warnings.length > 0) {
    console.log('\n\x1b[33mWarnings:\x1b[0m');
    for (const warning of compileResult.Warnings) {
      const location = warning.File ? ` (${warning.File}:${warning.Line ?? '?'})` : '';
      console.log(`  • ${warning.Message}${location}`);
    }
  }
}

function formatLogs(result: unknown): void {
  const logsResult = result as LogsResult;

  console.log(`Total logs: ${logsResult.TotalCount}\n`);

  for (const log of logsResult.Logs) {
    const typeColor = getLogTypeColor(log.Type);
    console.log(`${typeColor}[${log.Type}]\x1b[0m ${log.Message}`);

    if (log.StackTrace) {
      console.log(`\x1b[90m${log.StackTrace}\x1b[0m`);
    }
  }
}

function formatTests(result: unknown): void {
  const testResult = result as TestResult;

  if (testResult.Passed) {
    console.log('\x1b[32m✓ All tests passed\x1b[0m');
  } else {
    console.log('\x1b[31m✗ Some tests failed\x1b[0m');
  }

  console.log(
    `  Total: ${testResult.TestCount}, Passed: ${testResult.PassedCount}, ` +
      `Failed: ${testResult.FailedCount}, Skipped: ${testResult.SkippedCount}`,
  );

  if (testResult.Duration !== undefined) {
    console.log(`  Duration: ${testResult.Duration}ms`);
  }

  if (testResult.FailedTests && testResult.FailedTests.length > 0) {
    console.log('\n\x1b[31mFailed tests:\x1b[0m');
    for (const test of testResult.FailedTests) {
      console.log(`  • ${test.Name}`);
      console.log(`    ${test.Message}`);
    }
  }
}

function formatClearConsole(result: unknown): void {
  const clearResult = result as { Success: boolean; Message?: string };
  if (clearResult.Success) {
    console.log('\x1b[32m✓ Console cleared\x1b[0m');
  } else {
    console.log(`\x1b[31m✗ Failed to clear console\x1b[0m`);
    if (clearResult.Message) {
      console.log(`  ${clearResult.Message}`);
    }
  }
}

function formatFocusWindow(result: unknown): void {
  const focusResult = result as { Success: boolean };
  if (focusResult.Success) {
    console.log('\x1b[32m✓ Unity window focused\x1b[0m');
  } else {
    console.log('\x1b[31m✗ Failed to focus Unity window\x1b[0m');
  }
}

function defaultFormatter(result: unknown): void {
  if (result === null || result === undefined) {
    console.log('(no result)');
    return;
  }

  if (typeof result === 'object') {
    console.log(JSON.stringify(result, null, 2));
    return;
  }

  if (typeof result === 'string') {
    console.log(result);
    return;
  }

  console.log(JSON.stringify(result));
}

function getLogTypeColor(logType: string): string {
  switch (logType.toLowerCase()) {
    case 'error':
      return '\x1b[31m';
    case 'warning':
      return '\x1b[33m';
    case 'log':
      return '\x1b[37m';
    default:
      return '\x1b[37m';
  }
}

const formatters: Record<string, (result: unknown) => void> = {
  compile: formatCompile,
  'get-logs': formatLogs,
  'run-tests': formatTests,
  'clear-console': formatClearConsole,
  'focus-window': formatFocusWindow,
};
