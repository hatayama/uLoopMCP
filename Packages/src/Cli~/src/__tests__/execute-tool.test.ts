import {
  appendCliTimingsToDynamicCodeResult,
  diagnoseRetryableProjectConnectionError,
  isTransportDisconnectError,
  isSettingsReadError,
  prewarmDynamicCodeAfterCompile,
  stripInternalFields,
  resolveRecoveryPortOrKeepCurrent,
  resolveUnityConnectionWithStartupDiagnosis,
  shouldPromoteToServerStartingError,
  shouldReportServerStarting,
  shouldPrewarmDynamicCodeAfterCompile,
  shouldRetryWhenUnityProcessIsRunning,
} from '../execute-tool.js';
import {
  type ResolvedUnityConnection,
  UnityNotRunningError,
  UnityServerNotRunningError,
} from '../port-resolver.js';
import { ProjectMismatchError } from '../project-validator.js';

function createConnection(
  port: number,
  overrides?: Partial<ResolvedUnityConnection>,
): ResolvedUnityConnection {
  return {
    port,
    projectRoot: '/project',
    requestMetadata: null,
    shouldValidateProject: true,
    ...overrides,
  };
}

describe('isTransportDisconnectError', () => {
  it('returns true for UNITY_NO_RESPONSE', () => {
    expect(isTransportDisconnectError(new Error('UNITY_NO_RESPONSE'))).toBe(true);
  });

  it('returns true for Connection lost with details', () => {
    expect(isTransportDisconnectError(new Error('Connection lost: read ECONNRESET'))).toBe(true);
  });

  it('returns true for Connection lost with EPIPE', () => {
    expect(isTransportDisconnectError(new Error('Connection lost: write EPIPE'))).toBe(true);
  });

  it('returns false for JSON-RPC error from Unity', () => {
    expect(isTransportDisconnectError(new Error('Unity error: compilation failed'))).toBe(false);
  });

  it('returns false for connection refused (pre-dispatch error)', () => {
    expect(isTransportDisconnectError(new Error('connect ECONNREFUSED 127.0.0.1:8711'))).toBe(
      false,
    );
  });

  it('returns false for non-Error values', () => {
    expect(isTransportDisconnectError('UNITY_NO_RESPONSE')).toBe(false);
    expect(isTransportDisconnectError(null)).toBe(false);
    expect(isTransportDisconnectError(undefined)).toBe(false);
  });

  it('returns false for UnityNotRunningError', () => {
    expect(isTransportDisconnectError(new UnityNotRunningError('/project'))).toBe(false);
  });

  it('returns false for UnityServerNotRunningError', () => {
    expect(isTransportDisconnectError(new UnityServerNotRunningError('/project'))).toBe(false);
  });

  it('returns false for ProjectMismatchError', () => {
    expect(isTransportDisconnectError(new ProjectMismatchError('/a', '/b'))).toBe(false);
  });
});

describe('appendCliTimingsToDynamicCodeResult', () => {
  it('appends CLI total and overhead when RequestTotal is present', () => {
    const result: Record<string, unknown> = {
      Timings: ['[Perf] RequestTotal: 84.2ms'],
    };

    appendCliTimingsToDynamicCodeResult(result, 310.4, 415.9);

    expect(result['Timings']).toEqual([
      '[Perf] RequestTotal: 84.2ms',
      '[Perf] CliTotal: 310.4ms',
      '[Perf] CliProcessTotal: 415.9ms',
      '[Perf] CliBootstrap: 105.5ms',
      '[Perf] CliOverhead: 226.2ms',
    ]);
  });

  it('appends only CLI total when RequestTotal is missing', () => {
    const result: Record<string, unknown> = {
      Timings: ['[Perf] Backend: SharedRoslynWorker'],
    };

    appendCliTimingsToDynamicCodeResult(result, 180.0, 260.0);

    expect(result['Timings']).toEqual([
      '[Perf] Backend: SharedRoslynWorker',
      '[Perf] CliTotal: 180.0ms',
      '[Perf] CliProcessTotal: 260.0ms',
      '[Perf] CliBootstrap: 80.0ms',
    ]);
  });
});

describe('shouldPrewarmDynamicCodeAfterCompile', () => {
  it('returns true when compile succeeded without errors', () => {
    expect(
      shouldPrewarmDynamicCodeAfterCompile({
        Success: true,
        ErrorCount: 0,
      }),
    ).toBe(true);
  });

  it('returns true for force-recompile responses that stay indeterminate across domain reload', () => {
    expect(
      shouldPrewarmDynamicCodeAfterCompile({
        Success: null,
        ErrorCount: 0,
        Message: 'Force compilation executed. Use get-logs tool to retrieve compilation messages.',
      }),
    ).toBe(true);
  });

  it('returns false for indeterminate force-recompile responses with compiler errors', () => {
    expect(
      shouldPrewarmDynamicCodeAfterCompile({
        Success: null,
        ErrorCount: 2,
        Message: 'Force compilation executed. Use get-logs tool to retrieve compilation messages.',
      }),
    ).toBe(false);
  });

  it('returns false when compile failed or reported errors', () => {
    expect(
      shouldPrewarmDynamicCodeAfterCompile({
        Success: false,
        ErrorCount: 0,
      }),
    ).toBe(false);
    expect(
      shouldPrewarmDynamicCodeAfterCompile({
        Success: true,
        ErrorCount: 2,
      }),
    ).toBe(false);
    expect(
      shouldPrewarmDynamicCodeAfterCompile({
        Success: null,
        ErrorCount: null,
        Message: 'Compilation did not start.',
      }),
    ).toBe(false);
  });
});

describe('prewarmDynamicCodeAfterCompile', () => {
  it('spawns an isolated execute-dynamic-code process against the same project', async () => {
    const spawnCliProcess = jest.fn().mockReturnValue({
      status: 0,
      stdout: JSON.stringify({ Success: true }),
    });

    await prewarmDynamicCodeAfterCompile('/project', {
      spawnCliProcess,
    });

    expect(spawnCliProcess).toHaveBeenCalledTimes(2);
    expect(spawnCliProcess).toHaveBeenNthCalledWith(1, [
      'execute-dynamic-code',
      '--code',
      'using UnityEngine; bool previous = Debug.unityLogger.logEnabled; Debug.unityLogger.logEnabled = false; try { Debug.Log("Unity CLI Loop dynamic code prewarm"); return "Unity CLI Loop dynamic code prewarm"; } finally { Debug.unityLogger.logEnabled = previous; }',
      '--yield-to-foreground-requests',
      'true',
        '--project-path',
        '/project',
      ]);
    expect(spawnCliProcess).toHaveBeenNthCalledWith(2, [
      'execute-dynamic-code',
      '--code',
      'using UnityEngine; bool previous = Debug.unityLogger.logEnabled; Debug.unityLogger.logEnabled = false; try { Debug.Log("Unity CLI Loop dynamic code prewarm"); return "Unity CLI Loop dynamic code prewarm"; } finally { Debug.unityLogger.logEnabled = previous; }',
      '--yield-to-foreground-requests',
      'true',
        '--project-path',
        '/project',
      ]);
  });

  it('throws when the isolated CLI prewarm fails', async () => {
    await expect(
      prewarmDynamicCodeAfterCompile('/project', {
        spawnCliProcess: jest.fn().mockReturnValue({ status: 1 }),
      }),
    ).rejects.toThrow('Post-compile dynamic code prewarm failed.');
  });

  it('throws when the isolated CLI prewarm returns Success=false with exit code 0', async () => {
    await expect(
      prewarmDynamicCodeAfterCompile('/project', {
        spawnCliProcess: jest.fn().mockReturnValue({
          status: 0,
          stdout: JSON.stringify({
            Success: false,
            ErrorMessage: 'Another execution is already in progress',
          }),
        }),
      }),
    ).rejects.toThrow('Another execution is already in progress');
  });

  it('retries transient busy warmup failures until both passes complete successfully', async () => {
    const spawnCliProcess = jest
      .fn()
      .mockReturnValueOnce({
        status: 0,
        stdout: JSON.stringify({
          Success: false,
          ErrorMessage: 'Another execution is already in progress',
        }),
      })
      .mockReturnValue({
        status: 0,
        stdout: JSON.stringify({ Success: true }),
      });

    await expect(
      prewarmDynamicCodeAfterCompile('/project', {
        spawnCliProcess,
      }),
    ).resolves.toBeUndefined();

    expect(spawnCliProcess).toHaveBeenCalledTimes(3);
  });

  it('throws when spawning the isolated CLI prewarm process fails', async () => {
    await expect(
      prewarmDynamicCodeAfterCompile('/project', {
        spawnCliProcess: jest.fn().mockReturnValue({ status: null, error: new Error('spawn failed') }),
      }),
    ).rejects.toThrow('spawn failed');
  });

  it('throws a generic warmup failure when the isolated CLI prints malformed stdout', async () => {
    await expect(
      prewarmDynamicCodeAfterCompile('/project', {
        spawnCliProcess: jest.fn().mockReturnValue({
          status: 0,
          stdout: 'not-json',
        }),
      }),
    ).rejects.toThrow('Post-compile dynamic code prewarm failed.');
  });

  it('retries when the isolated CLI reports that Unity is still starting', async () => {
    const spawnCliProcess = jest
      .fn()
      .mockReturnValueOnce({
        status: 1,
        stderr: 'Unity server is starting',
      })
      .mockReturnValue({
        status: 0,
        stdout: JSON.stringify({ Success: true }),
      });

    await expect(
      prewarmDynamicCodeAfterCompile('/project', {
        spawnCliProcess,
      }),
    ).resolves.toBeUndefined();

    expect(spawnCliProcess).toHaveBeenCalledTimes(3);
  });
});

describe('stripInternalFields', () => {
  it('removes ProjectRoot from all tool outputs', () => {
    const cleaned = stripInternalFields({
      ProjectRoot: '/project',
      Success: true,
    });

    expect(cleaned).toEqual({
      Success: true,
    });
  });
});

describe('diagnoseRetryableProjectConnectionError', () => {
  it('returns UnityNotRunningError when connection fails and Unity is not running', async () => {
    const error = await diagnoseRetryableProjectConnectionError(
      new Error('Connection error: connect ECONNREFUSED 127.0.0.1:8711'),
      '/project',
      true,
      {
        findRunningUnityProcessForProjectFn: jest.fn().mockResolvedValue(null),
      },
    );

    expect(error).toBeInstanceOf(UnityNotRunningError);
  });

  it('returns UnityServerNotRunningError when Unity is running but server is unavailable', async () => {
    const error = await diagnoseRetryableProjectConnectionError(
      new Error('UNITY_NO_RESPONSE'),
      '/project',
      true,
      {
        findRunningUnityProcessForProjectFn: jest.fn().mockResolvedValue({ pid: 1234 }),
      },
    );

    expect(error).toBeInstanceOf(UnityServerNotRunningError);
  });

  it('preserves non-retryable errors', async () => {
    const originalError = new ProjectMismatchError('/expected', '/actual');

    const error = await diagnoseRetryableProjectConnectionError(originalError, '/project', true, {
      findRunningUnityProcessForProjectFn: jest.fn(),
    });

    expect(error).toBe(originalError);
  });

  it('preserves retryable errors when project diagnosis is disabled', async () => {
    const originalError = new Error('Connection error: connect ECONNREFUSED 127.0.0.1:8711');

    const error = await diagnoseRetryableProjectConnectionError(originalError, '/project', false, {
      findRunningUnityProcessForProjectFn: jest.fn(),
    });

    expect(error).toBe(originalError);
  });

  it('preserves the original error when OS-level process inspection fails', async () => {
    const originalError = new Error('Connection error: connect ECONNREFUSED 127.0.0.1:8711');

    const error = await diagnoseRetryableProjectConnectionError(originalError, '/project', true, {
      findRunningUnityProcessForProjectFn: jest.fn().mockRejectedValue(new Error('ps failed')),
    });

    expect(error).toBe(originalError);
  });
});

describe('shouldRetryWhenUnityProcessIsRunning', () => {
  it('returns true for retryable failures when Unity is still running', async () => {
    await expect(
      shouldRetryWhenUnityProcessIsRunning(new Error('UNITY_NO_RESPONSE'), '/project', true, {
        findRunningUnityProcessForProjectFn: jest.fn().mockResolvedValue({ pid: 1234 }),
      }),
    ).resolves.toBe(true);
  });

  it('returns false for non-retryable Unity errors even when Unity is still running', async () => {
    await expect(
      shouldRetryWhenUnityProcessIsRunning(
        new Error('Unity error: compilation failed'),
        '/project',
        true,
        {
          findRunningUnityProcessForProjectFn: jest.fn().mockResolvedValue({ pid: 1234 }),
        },
      ),
    ).resolves.toBe(false);
  });

  it('returns true for fast project validation session changes when Unity is still running', async () => {
    await expect(
      shouldRetryWhenUnityProcessIsRunning(
        new Error(
          'Unity error: Invalid params: Unity CLI Loop server session changed. Retry the command.',
        ),
        '/project',
        true,
        {
          findRunningUnityProcessForProjectFn: jest.fn().mockResolvedValue({ pid: 1234 }),
        },
      ),
    ).resolves.toBe(true);
  });
});

describe('resolveRecoveryPortOrKeepCurrent', () => {
  it('keeps the current port when recovery settings are temporarily unreadable', async () => {
    await expect(
      resolveRecoveryPortOrKeepCurrent(
        createConnection(8711),
        undefined,
        '/project',
        jest.fn().mockRejectedValue(new Error('busy')),
      ),
    ).resolves.toEqual(createConnection(8711));
  });

  it('falls back to legacy project validation when recovery settings are temporarily unreadable', async () => {
    await expect(
      resolveRecoveryPortOrKeepCurrent(
        createConnection(8711, {
          requestMetadata: {
            expectedProjectRoot: '/project',
            expectedServerSessionId: 'session-1',
          },
          shouldValidateProject: false,
        }),
        undefined,
        '/project',
        jest.fn().mockRejectedValue(new Error('busy')),
      ),
    ).resolves.toEqual(
      createConnection(8711, {
        requestMetadata: null,
        shouldValidateProject: true,
      }),
    );
  });

  it('re-resolves the port when recovery settings are available', async () => {
    await expect(
      resolveRecoveryPortOrKeepCurrent(
        createConnection(8711),
        undefined,
        '/project',
        jest.fn().mockResolvedValue(
          createConnection(8712, {
            requestMetadata: {
              expectedProjectRoot: '/project',
              expectedServerSessionId: 'session-2',
            },
            shouldValidateProject: false,
          }),
        ),
      ),
    ).resolves.toEqual(
      createConnection(8712, {
        requestMetadata: {
          expectedProjectRoot: '/project',
          expectedServerSessionId: 'session-2',
        },
        shouldValidateProject: false,
      }),
    );
  });
});

describe('shouldReportServerStarting', () => {
  it('returns true when startup lock exists and Unity is still running', async () => {
    const dependencies = {
      findRunningUnityProcessForProjectFn: jest.fn().mockResolvedValue({ pid: 1234 }),
    };
    const existsSpy = jest.spyOn(require('fs'), 'existsSync').mockReturnValue(true);
    const statSpy = jest
      .spyOn(require('fs'), 'statSync')
      .mockReturnValue({ mtimeMs: Date.now() } as { mtimeMs: number });

    await expect(
      shouldReportServerStarting('/project', true, dependencies),
    ).resolves.toBe(true);

    existsSpy.mockRestore();
    statSpy.mockRestore();
  });

  it('returns false when startup lock exists but Unity is not running', async () => {
    const dependencies = {
      findRunningUnityProcessForProjectFn: jest.fn().mockResolvedValue(null),
    };
    const existsSpy = jest.spyOn(require('fs'), 'existsSync').mockReturnValue(true);
    const statSpy = jest
      .spyOn(require('fs'), 'statSync')
      .mockReturnValue({ mtimeMs: Date.now() } as { mtimeMs: number });

    await expect(
      shouldReportServerStarting('/project', true, dependencies),
    ).resolves.toBe(false);

    existsSpy.mockRestore();
    statSpy.mockRestore();
  });

  it('returns false when the startup lock is stale', async () => {
    const dependencies = {
      findRunningUnityProcessForProjectFn: jest.fn().mockResolvedValue({ pid: 1234 }),
    };
    const existsSpy = jest.spyOn(require('fs'), 'existsSync').mockReturnValue(true);
    const statSpy = jest
      .spyOn(require('fs'), 'statSync')
      .mockReturnValue({ mtimeMs: Date.now() - 80000 } as { mtimeMs: number });

    await expect(
      shouldReportServerStarting('/project', true, dependencies),
    ).resolves.toBe(false);

    existsSpy.mockRestore();
    statSpy.mockRestore();
  });

  it('returns false when the startup lock disappears before statSync', async () => {
    const dependencies = {
      findRunningUnityProcessForProjectFn: jest.fn().mockResolvedValue({ pid: 1234 }),
    };
    const existsSpy = jest.spyOn(require('fs'), 'existsSync').mockReturnValue(true);
    const statSpy = jest.spyOn(require('fs'), 'statSync').mockImplementation(() => {
      throw Object.assign(new Error('ENOENT'), { code: 'ENOENT' });
    });

    await expect(
      shouldReportServerStarting('/project', true, dependencies),
    ).resolves.toBe(false);

    existsSpy.mockRestore();
    statSpy.mockRestore();
  });
});

describe('shouldPromoteToServerStartingError', () => {
  it('returns false for non-retryable errors even when startup lock exists', async () => {
    const dependencies = {
      findRunningUnityProcessForProjectFn: jest.fn().mockResolvedValue({ pid: 1234 }),
    };
    const existsSpy = jest.spyOn(require('fs'), 'existsSync').mockReturnValue(true);
    const statSpy = jest
      .spyOn(require('fs'), 'statSync')
      .mockReturnValue({ mtimeMs: Date.now() } as { mtimeMs: number });

    await expect(
      shouldPromoteToServerStartingError(
        new Error('Unexpected response from Unity: missing Tools array'),
        '/project',
        true,
        dependencies,
      ),
    ).resolves.toBe(false);

    existsSpy.mockRestore();
    statSpy.mockRestore();
  });
});

describe('isSettingsReadError', () => {
  it('returns true for settings read failures', () => {
    expect(
      isSettingsReadError(
        new Error('Could not read Unity server port from settings.\n\n  Settings file: /project/UserSettings/UnityMcpSettings.json'),
      ),
    ).toBe(true);
  });
});

describe('resolveUnityConnectionWithStartupDiagnosis', () => {
  it('promotes settings read failures to UNITY_SERVER_STARTING when startup lock is fresh', async () => {
    const dependencies = {
      findRunningUnityProcessForProjectFn: jest.fn().mockResolvedValue({ pid: 1234 }),
    };
    const existsSpy = jest.spyOn(require('fs'), 'existsSync').mockReturnValue(true);
    const statSpy = jest
      .spyOn(require('fs'), 'statSync')
      .mockReturnValue({ mtimeMs: Date.now() } as { mtimeMs: number });

    await expect(
      resolveUnityConnectionWithStartupDiagnosis(
        undefined,
        '/project',
        dependencies,
        jest.fn().mockRejectedValue(
          new Error(
            'Could not read Unity server port from settings.\n\n  Settings file: /project/UserSettings/UnityMcpSettings.json',
          ),
        ),
      ),
    ).rejects.toThrow('UNITY_SERVER_STARTING');

    existsSpy.mockRestore();
    statSpy.mockRestore();
  });

  it('preserves the original usage error when both --port and --project-path are specified', async () => {
    await expect(
      resolveUnityConnectionWithStartupDiagnosis(
        8711,
        '/definitely/missing/project',
        undefined,
        jest
          .fn()
          .mockRejectedValue(new Error('Cannot specify both --port and --project-path')),
      ),
    ).rejects.toThrow('Cannot specify both --port and --project-path');
  });
});
