const mockResolveUnityConnection = jest.fn();
const mockValidateProjectPath = jest.fn<string, [string]>();
const mockExistsSync = jest.fn<boolean, [string]>();
const mockWaitForCompileCompletion = jest.fn();
const mockResolveCompileExecutionOptions = jest.fn();
const mockConsoleLog = jest.spyOn(console, 'log').mockImplementation(() => {});

class MockDirectUnityClient {
  public constructor(private readonly _port: number) {}

  public connect(): Promise<void> {
    return Promise.resolve();
  }

  public disconnect(): void {}

  public sendRequest<T>(): Promise<T> {
    return Promise.resolve({
      Success: true,
      ErrorCount: 1,
      ProjectRoot: '/project',
      Ver: '1.7.3',
    } as T);
  }
}

jest.mock('../port-resolver.js', () => ({
  resolveUnityConnection: (...args: unknown[]) => mockResolveUnityConnection(...args),
  validateProjectPath: (projectPath: string): string => mockValidateProjectPath(projectPath),
  UnityNotRunningError: class UnityNotRunningError extends Error {},
  UnityServerNotRunningError: class UnityServerNotRunningError extends Error {},
}));

jest.mock('fs', () => ({
  existsSync: (path: string): boolean => mockExistsSync(path),
}));

jest.mock('../compile-helpers.js', () => ({
  ensureCompileRequestId: (): string => 'request-id',
  resolveCompileExecutionOptions: (...args: unknown[]) => mockResolveCompileExecutionOptions(...args),
  sleep: jest.fn().mockResolvedValue(undefined),
  waitForCompileCompletion: (...args: unknown[]) => mockWaitForCompileCompletion(...args),
}));

jest.mock('../spinner.js', () => ({
  createSpinner: (): { update: () => void; stop: () => void } => ({
    update: (): void => {},
    stop: (): void => {},
  }),
}));

jest.mock('../project-validator.js', () => ({
  validateConnectedProject: jest.fn(),
  ProjectMismatchError: class ProjectMismatchError extends Error {},
}));

jest.mock('../direct-unity-client.js', () => ({
  DirectUnityClient: MockDirectUnityClient,
}));

import { executeToolCommand } from '../execute-tool.js';

describe('executeToolCommand compile wait options', () => {
  beforeEach(() => {
    mockResolveUnityConnection.mockReset();
    mockResolveUnityConnection.mockResolvedValue({
      port: 8711,
      projectRoot: '/project',
      requestMetadata: null,
      shouldValidateProject: true,
    });

    mockValidateProjectPath.mockReset();
    mockValidateProjectPath.mockReturnValue('/project');

    mockExistsSync.mockReset();
    mockExistsSync.mockReturnValue(false);

    mockResolveCompileExecutionOptions.mockReset();
    mockResolveCompileExecutionOptions.mockReturnValue({
      forceRecompile: true,
      waitForDomainReload: true,
    });

    mockWaitForCompileCompletion.mockReset();
    mockWaitForCompileCompletion.mockResolvedValue({
      outcome: 'completed',
      result: {
        Success: true,
        ErrorCount: 1,
        Ver: '1.7.3',
      },
    });

    mockConsoleLog.mockClear();
  });

  afterAll(() => {
    mockConsoleLog.mockRestore();
  });

  it('ignores serverstarting.lock while waiting for compile completion', async () => {
    await expect(
      executeToolCommand('compile', { ForceRecompile: true, WaitForDomainReload: true }, { projectPath: '/project' }),
    ).resolves.toBeUndefined();

    expect(mockWaitForCompileCompletion).toHaveBeenCalledWith(
      expect.objectContaining({
        projectRoot: '/project',
        requestId: 'request-id',
        unityPort: 8711,
        includeServerStartingLock: false,
      }),
    );
  });
});
