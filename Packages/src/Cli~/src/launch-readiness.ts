import { DirectUnityClient } from './direct-unity-client.js';
import { sleep } from './compile-helpers.js';
import { resolveUnityPort, UnityNotRunningError } from './port-resolver.js';
import { ProjectMismatchError, validateConnectedProject } from './project-validator.js';

const LAUNCH_READINESS_TIMEOUT_MS = 180000;
const LAUNCH_READINESS_RETRY_MS = 1000;
const LAUNCH_READINESS_CODE = 'return null;';
const TRANSIENT_EXECUTE_DYNAMIC_CODE_ERROR_MESSAGES = [
  'Another execution is already in progress',
  'Execution was cancelled or timed out',
];
const TRANSIENT_EXECUTE_DYNAMIC_CODE_ERROR_PREFIXES = ['COMPILATION_PROVIDER_UNAVAILABLE:'];

interface ExecuteDynamicCodeReadinessResponse {
  Success?: boolean;
  ErrorMessage?: string;
}

interface DynamicCodeLaunchReadinessDependencies {
  resolveUnityPortFn: typeof resolveUnityPort;
  validateConnectedProjectFn: typeof validateConnectedProject;
  createClient: (port: number) => DirectUnityClient;
  sleepFn: typeof sleep;
  nowFn: () => number;
}

const defaultDependencies: DynamicCodeLaunchReadinessDependencies = {
  resolveUnityPortFn: resolveUnityPort,
  validateConnectedProjectFn: validateConnectedProject,
  createClient: (port: number) => new DirectUnityClient(port),
  sleepFn: sleep,
  nowFn: () => Date.now(),
};

function isTransientExecuteDynamicCodeFailure(
  payload: ExecuteDynamicCodeReadinessResponse,
): boolean {
  if (typeof payload.Success !== 'boolean') {
    return true;
  }

  if (payload.Success) {
    return false;
  }

  const errorMessage: string = payload.ErrorMessage ?? '';
  if (errorMessage.length === 0) {
    return true;
  }

  if (TRANSIENT_EXECUTE_DYNAMIC_CODE_ERROR_MESSAGES.includes(errorMessage)) {
    return true;
  }

  return TRANSIENT_EXECUTE_DYNAMIC_CODE_ERROR_PREFIXES.some((prefix) =>
    errorMessage.startsWith(prefix),
  );
}

function isRetryableLaunchReadinessError(error: unknown): boolean {
  if (error instanceof ProjectMismatchError) {
    return false;
  }

  if (error instanceof UnityNotRunningError) {
    return true;
  }

  if (!(error instanceof Error)) {
    return false;
  }

  const message: string = error.message;
  return (
    message.includes('Could not read Unity server port from settings') ||
    message.includes('ECONNREFUSED') ||
    message.includes('EADDRNOTAVAIL') ||
    message === 'UNITY_NO_RESPONSE' ||
    message.startsWith('Connection lost:') ||
    message.startsWith('Unity error:')
  );
}

function createLaunchReadinessFailure(payload: ExecuteDynamicCodeReadinessResponse): Error {
  const errorMessage: string = payload.ErrorMessage ?? 'unknown execute-dynamic-code error';
  return new Error(`execute-dynamic-code launch readiness probe failed: ${errorMessage}`);
}

export async function waitForDynamicCodeReadyAfterLaunch(
  projectPath: string,
  dependencies: DynamicCodeLaunchReadinessDependencies = defaultDependencies,
): Promise<void> {
  const startTime: number = dependencies.nowFn();

  while (dependencies.nowFn() - startTime < LAUNCH_READINESS_TIMEOUT_MS) {
    let client: DirectUnityClient | null = null;

    try {
      const port: number = await dependencies.resolveUnityPortFn(undefined, projectPath);
      client = dependencies.createClient(port);
      await client.connect();
      await dependencies.validateConnectedProjectFn(client, projectPath);

      const payload = await client.sendRequest<ExecuteDynamicCodeReadinessResponse>(
        'execute-dynamic-code',
        {
          Code: LAUNCH_READINESS_CODE,
          CompileOnly: false,
        },
      );

      if (payload.Success) {
        return;
      }

      if (!isTransientExecuteDynamicCodeFailure(payload)) {
        throw createLaunchReadinessFailure(payload);
      }
    } catch (error) {
      if (!isRetryableLaunchReadinessError(error)) {
        throw error;
      }
    } finally {
      client?.disconnect();
    }

    await dependencies.sleepFn(LAUNCH_READINESS_RETRY_MS);
  }

  throw new Error(
    `Timed out waiting for execute-dynamic-code to become ready after launch (${LAUNCH_READINESS_TIMEOUT_MS}ms).`,
  );
}
