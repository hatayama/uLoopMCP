import { UnityClient } from '../types/tool-types.js';

const FORCE_COMPILE_INDETERMINATE_MESSAGE_PREFIX = 'Force compilation executed.';
const POST_COMPILE_DYNAMIC_CODE_PREWARM_CODE =
  'using UnityEngine; bool previous = Debug.unityLogger.logEnabled; Debug.unityLogger.logEnabled = false; try { Debug.Log("Unity CLI Loop dynamic code prewarm"); return "Unity CLI Loop dynamic code prewarm"; } finally { Debug.unityLogger.logEnabled = previous; }';
// Why: domain-reload measurements still showed two cold execute-dynamic-code requests before
// the warmed steady state returned, so compile must keep the third pass off the user path.
// Why not stop at two passes: that reintroduces the "compile finished but first dynamic code is slow"
// regression for clients that issue execute-dynamic-code immediately after a forced compile.
const POST_COMPILE_DYNAMIC_CODE_PREWARM_PASS_COUNT = 3;
const POST_COMPILE_DYNAMIC_CODE_PREWARM_MAX_ATTEMPTS_PER_PASS = 20;
const POST_COMPILE_DYNAMIC_CODE_PREWARM_DELAY_MS = 500;
const EXECUTION_IN_PROGRESS_ERROR_MESSAGE = 'Another execution is already in progress';
const EXECUTION_CANCELLED_ERROR_MESSAGE = 'Execution was cancelled or timed out';
const RETRYABLE_COMPILATION_PROVIDER_UNAVAILABLE_SUBSTRINGS = ['warming up'];
const RETRYABLE_UNITY_STARTUP_ERROR_SUBSTRINGS = ['can only be called from the main thread'];
const RETRYABLE_DISPATCH_GUIDANCE_SUBSTRINGS = [
  'unity is currently compiling or reloading',
  'retry after the operation completes',
];

interface DynamicCodeWarmupResponse {
  Success?: boolean;
  ErrorMessage?: string;
}

interface WarmupAttemptResult {
  response?: unknown;
  error?: unknown;
}

export function shouldPrewarmDynamicCodeAfterCompile(result: unknown): boolean {
  if (typeof result !== 'object' || result === null) {
    return false;
  }

  const record = result as Record<string, unknown>;
  const success = record['Success'];
  const errorCount = record['ErrorCount'];
  if (success === true && errorCount === 0) {
    return true;
  }

  const message = record['Message'];
  return (
    success === null &&
    errorCount === 0 &&
    typeof message === 'string' &&
    message.startsWith(FORCE_COMPILE_INDETERMINATE_MESSAGE_PREFIX)
  );
}

export async function prewarmDynamicCodeAfterCompile(
  unityClient: UnityClient,
  sleepFn: (ms: number) => Promise<void> = sleep,
): Promise<void> {
  for (
    let successfulPassCount = 0;
    successfulPassCount < POST_COMPILE_DYNAMIC_CODE_PREWARM_PASS_COUNT;
    successfulPassCount++
  ) {
    let lastError: Error | undefined;

    for (
      let attemptIndex = 0;
      attemptIndex < POST_COMPILE_DYNAMIC_CODE_PREWARM_MAX_ATTEMPTS_PER_PASS;
      attemptIndex++
    ) {
      if (attemptIndex > 0) {
        await sleepFn(POST_COMPILE_DYNAMIC_CODE_PREWARM_DELAY_MS);
      }

      const attemptResult = await unityClient
        .executeTool('execute-dynamic-code', {
          Code: POST_COMPILE_DYNAMIC_CODE_PREWARM_CODE,
          CompileOnly: false,
          YieldToForegroundRequests: true,
        })
        .then(
          (response): WarmupAttemptResult => ({ response }),
          (error): WarmupAttemptResult => ({ error }),
        );

      if (attemptResult.error !== undefined) {
        lastError = toError(attemptResult.error);
      } else if (didWarmupSucceed(attemptResult.response)) {
        lastError = undefined;
        break;
      } else {
        lastError = createWarmupError(attemptResult.response);
      }

      if (!isRetryableWarmupError(lastError)) {
        break;
      }
    }

    if (lastError !== undefined) {
      throw lastError;
    }
  }
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function didWarmupSucceed(response: unknown): boolean {
  if (typeof response !== 'object' || response === null) {
    return false;
  }

  return (response as DynamicCodeWarmupResponse).Success === true;
}

function createWarmupError(response: unknown): Error {
  if (typeof response === 'string' && response.length > 0) {
    return new Error(response);
  }

  if (typeof response === 'object' && response !== null) {
    const errorMessage = (response as DynamicCodeWarmupResponse).ErrorMessage;
    if (typeof errorMessage === 'string' && errorMessage.length > 0) {
      return new Error(errorMessage);
    }
  }

  return new Error('Post-compile dynamic code prewarm failed.');
}

function isRetryableWarmupError(error: Error): boolean {
  return (
    error.message === EXECUTION_IN_PROGRESS_ERROR_MESSAGE ||
    error.message === EXECUTION_CANCELLED_ERROR_MESSAGE ||
    isRetryableDisconnectError(error.message) ||
    isRetryableCompilationProviderUnavailable(error.message) ||
    isRetryableUnityStartupError(error.message) ||
    isRetryableDispatchGuidance(error.message)
  );
}

function isRetryableDisconnectError(errorMessage: string): boolean {
  return errorMessage === 'UNITY_NO_RESPONSE' || errorMessage.startsWith('Connection lost:');
}

function isRetryableCompilationProviderUnavailable(errorMessage: string): boolean {
  if (!errorMessage.startsWith('COMPILATION_PROVIDER_UNAVAILABLE:')) {
    return false;
  }

  const normalizedMessage = errorMessage.toLowerCase();
  return RETRYABLE_COMPILATION_PROVIDER_UNAVAILABLE_SUBSTRINGS.some((substring) =>
    normalizedMessage.includes(substring),
  );
}

function isRetryableUnityStartupError(errorMessage: string): boolean {
  const normalizedMessage = errorMessage.toLowerCase();
  return RETRYABLE_UNITY_STARTUP_ERROR_SUBSTRINGS.some((substring) =>
    normalizedMessage.includes(substring),
  );
}

function isRetryableDispatchGuidance(errorMessage: string): boolean {
  const normalizedMessage = errorMessage.toLowerCase();
  return RETRYABLE_DISPATCH_GUIDANCE_SUBSTRINGS.some((substring) =>
    normalizedMessage.includes(substring),
  );
}

function toError(error: unknown): Error {
  if (error instanceof Error) {
    return error;
  }

  if (typeof error === 'string') {
    return new Error(error);
  }

  return new Error('Unknown error');
}
