import { existsSync, readFileSync } from 'fs';
import { join } from 'path';

export const COMPILE_FORCE_RECOMPILE_ARG_KEYS = [
  'ForceRecompile',
  'forceRecompile',
  'force_recompile',
  'force-recompile',
] as const;

export const COMPILE_WAIT_FOR_DOMAIN_RELOAD_ARG_KEYS = [
  'WaitForDomainReload',
  'waitForDomainReload',
  'wait_for_domain_reload',
  'wait-for-domain-reload',
] as const;

export interface CompileExecutionOptions {
  forceRecompile: boolean;
  waitForDomainReload: boolean;
}

export interface DomainReloadWaitOptions {
  projectRoot: string;
  timeoutMs: number;
  pollIntervalMs: number;
  isUnityReadyWhenIdle?: () => Promise<boolean>;
}

export type DomainReloadWaitOutcome = 'settled' | 'not_detected' | 'timed_out';

export interface CompileResultWaitOptions {
  projectRoot: string;
  requestId: string;
  timeoutMs: number;
  pollIntervalMs: number;
}

export type CompileCompletionOutcome = 'completed' | 'timed_out';

/**
 * Between compilationFinished and beforeAssemblyReload, there is a brief gap (~50ms measured)
 * where no lock files exist. This grace period prevents false "completed" detection during that gap.
 */
const LOCK_GRACE_PERIOD_MS = 500;

export interface CompileCompletionWaitOptions {
  projectRoot: string;
  requestId: string;
  timeoutMs: number;
  pollIntervalMs: number;
  isUnityReadyWhenIdle?: () => Promise<boolean>;
}

export interface CompileCompletionResult<T> {
  outcome: CompileCompletionOutcome;
  result?: T;
}

export function toBoolean(value: unknown): boolean {
  if (typeof value === 'boolean') {
    return value;
  }

  if (typeof value === 'string') {
    return value.toLowerCase() === 'true';
  }

  return false;
}

export function getCompileBooleanArg(
  args: Record<string, unknown>,
  keys: readonly string[],
): boolean {
  for (const key of keys) {
    if (!(key in args)) {
      continue;
    }

    return toBoolean(args[key]);
  }

  return false;
}

export function resolveCompileExecutionOptions(
  args: Record<string, unknown>,
): CompileExecutionOptions {
  return {
    forceRecompile: getCompileBooleanArg(args, COMPILE_FORCE_RECOMPILE_ARG_KEYS),
    waitForDomainReload: getCompileBooleanArg(args, COMPILE_WAIT_FOR_DOMAIN_RELOAD_ARG_KEYS),
  };
}

export function createCompileRequestId(): string {
  const timestamp = Date.now();
  const randomToken = Math.floor(Math.random() * 1000000)
    .toString()
    .padStart(6, '0');
  return `compile_${timestamp}_${randomToken}`;
}

export function ensureCompileRequestId(args: Record<string, unknown>): string {
  const existingRequestId = args['RequestId'];
  if (typeof existingRequestId === 'string' && existingRequestId.length > 0) {
    return existingRequestId;
  }

  const requestId = createCompileRequestId();
  args['RequestId'] = requestId;
  return requestId;
}

export function getCompileResultFilePath(projectRoot: string, requestId: string): string {
  return join(projectRoot, 'Temp', 'uLoopMCP', 'compile-results', `${requestId}.json`);
}

export function isUnityBusyByLockFiles(projectRoot: string): boolean {
  const compilingLockPath = join(projectRoot, 'Temp', 'compiling.lock');
  if (existsSync(compilingLockPath)) {
    return true;
  }

  const domainReloadLockPath = join(projectRoot, 'Temp', 'domainreload.lock');
  if (existsSync(domainReloadLockPath)) {
    return true;
  }

  const serverStartingLockPath = join(projectRoot, 'Temp', 'serverstarting.lock');
  return existsSync(serverStartingLockPath);
}

export function stripUtf8Bom(content: string): string {
  if (content.charCodeAt(0) === 0xfeff) {
    return content.slice(1);
  }

  return content;
}

export function tryReadCompileResult<T>(projectRoot: string, requestId: string): T | undefined {
  const resultFilePath = getCompileResultFilePath(projectRoot, requestId);
  if (!existsSync(resultFilePath)) {
    return undefined;
  }

  const content = readFileSync(resultFilePath, 'utf-8');
  const parsed = JSON.parse(stripUtf8Bom(content)) as unknown;
  return parsed as T;
}

export async function waitForDomainReloadToSettle(
  options: DomainReloadWaitOptions,
): Promise<DomainReloadWaitOutcome> {
  let waitedMs = 0;
  let busyObserved = false;

  while (waitedMs < options.timeoutMs) {
    const isBusy = isUnityBusyByLockFiles(options.projectRoot);
    if (isBusy) {
      busyObserved = true;
    }

    if (!busyObserved && !isBusy) {
      return 'not_detected';
    }

    if (busyObserved && !isBusy) {
      if (!options.isUnityReadyWhenIdle) {
        return 'settled';
      }

      const isReady = await options.isUnityReadyWhenIdle();
      if (isReady) {
        return 'settled';
      }
    }

    await sleep(options.pollIntervalMs);
    waitedMs += options.pollIntervalMs;
  }

  if (!busyObserved) {
    return 'not_detected';
  }

  return 'timed_out';
}

export async function waitForCompileResult<T>(
  options: CompileResultWaitOptions,
): Promise<T | undefined> {
  let waitedMs = 0;

  while (true) {
    const storedResult = tryReadCompileResult<T>(options.projectRoot, options.requestId);
    if (storedResult !== undefined) {
      return storedResult;
    }

    if (waitedMs >= options.timeoutMs) {
      return undefined;
    }

    await sleep(options.pollIntervalMs);
    waitedMs += options.pollIntervalMs;
  }
}

/**
 * Wait until the compile result file exists AND all Unity lock files are gone.
 * After the result file appears and locks disappear, waits an additional grace period
 * to catch the gap between compilationFinished and beforeAssemblyReload (~50ms measured).
 */
export async function waitForCompileCompletion<T>(
  options: CompileCompletionWaitOptions,
): Promise<CompileCompletionResult<T>> {
  let waitedMs = 0;
  let idleSinceMs: number | null = null;

  while (waitedMs < options.timeoutMs) {
    const result = tryReadCompileResult<T>(options.projectRoot, options.requestId);
    const isBusy = isUnityBusyByLockFiles(options.projectRoot);

    if (result !== undefined && !isBusy) {
      if (idleSinceMs === null) {
        idleSinceMs = waitedMs;
      }

      const idleDuration = waitedMs - idleSinceMs;
      if (idleDuration >= LOCK_GRACE_PERIOD_MS) {
        if (!options.isUnityReadyWhenIdle) {
          return { outcome: 'completed', result };
        }

        const isReady = await options.isUnityReadyWhenIdle();
        if (isReady) {
          return { outcome: 'completed', result };
        }
      }
    } else {
      idleSinceMs = null;
    }

    await sleep(options.pollIntervalMs);
    waitedMs += options.pollIntervalMs;
  }

  const lastResult = tryReadCompileResult<T>(options.projectRoot, options.requestId);
  if (lastResult !== undefined) {
    return { outcome: 'completed', result: lastResult };
  }

  return { outcome: 'timed_out' };
}

export function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
