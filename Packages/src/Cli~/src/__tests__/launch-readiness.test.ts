import { DirectUnityClient } from '../direct-unity-client.js';
import { waitForDynamicCodeReadyAfterLaunch } from '../launch-readiness.js';
import { ProjectMismatchError } from '../project-validator.js';

interface MockReadinessResponse {
  Success?: boolean;
  ErrorMessage?: string;
  Timings?: string[];
}

function createMockClient(
  responses: Array<MockReadinessResponse | Error>,
  recordedMethods: string[],
): { client: DirectUnityClient; disconnectSpy: jest.Mock } {
  const disconnectSpy = jest.fn();

  return {
    disconnectSpy,
    client: {
      connect: jest.fn().mockImplementation((): Promise<void> => Promise.resolve()),
      disconnect: disconnectSpy,
      isConnected: jest.fn().mockReturnValue(true),
      sendRequest: jest.fn().mockImplementation((method: string) => {
        recordedMethods.push(method);
        const next = responses.shift();
        if (next instanceof Error) {
          return Promise.reject(next);
        }

        return Promise.resolve(next);
      }),
    } as unknown as DirectUnityClient,
  };
}

describe('waitForDynamicCodeReadyAfterLaunch', () => {
  afterEach(() => {
    jest.restoreAllMocks();
  });

  it('retries transient execute-dynamic-code failures until success', async () => {
    const recordedMethods: string[] = [];
    const createdClients: DirectUnityClient[] = [];
    const disconnectSpies: jest.Mock[] = [];
    const responses: Array<MockReadinessResponse | Error> = [
      { Success: false, ErrorMessage: 'COMPILATION_PROVIDER_UNAVAILABLE: warming up' },
      { Success: true },
    ];
    let sleepCount = 0;

    await waitForDynamicCodeReadyAfterLaunch('/project', {
      resolveUnityPortFn: jest.fn().mockResolvedValue(8711),
      validateConnectedProjectFn: jest.fn().mockResolvedValue(undefined),
      createClient: () => {
        const mockClient = createMockClient(responses, recordedMethods);
        createdClients.push(mockClient.client);
        disconnectSpies.push(mockClient.disconnectSpy);
        return mockClient.client;
      },
      sleepFn: jest.fn().mockImplementation((): Promise<void> => {
        sleepCount++;
        return Promise.resolve();
      }),
      nowFn: (() => {
        let now = 0;
        return (): number => {
          now += 100;
          return now;
        };
      })(),
    });

    expect(recordedMethods).toEqual(['execute-dynamic-code', 'execute-dynamic-code']);
    expect(sleepCount).toBe(1);
    expect(createdClients).toHaveLength(2);
    expect(disconnectSpies[0]).toHaveBeenCalled();
    expect(disconnectSpies[1]).toHaveBeenCalled();
  });

  it('rethrows non-transient readiness failures', async () => {
    const recordedMethods: string[] = [];

    await expect(
      waitForDynamicCodeReadyAfterLaunch('/project', {
        resolveUnityPortFn: jest.fn().mockResolvedValue(8711),
        validateConnectedProjectFn: jest.fn().mockResolvedValue(undefined),
        createClient: () =>
          createMockClient([{ Success: false, ErrorMessage: 'Syntax error' }], recordedMethods)
            .client,
        sleepFn: jest.fn(),
        nowFn: () => 0,
      }),
    ).rejects.toThrow('execute-dynamic-code launch readiness probe failed: Syntax error');
  });

  it('does not retry permanent compilation provider unavailability', async () => {
    const recordedMethods: string[] = [];

    await expect(
      waitForDynamicCodeReadyAfterLaunch('/project', {
        resolveUnityPortFn: jest.fn().mockResolvedValue(8711),
        validateConnectedProjectFn: jest.fn().mockResolvedValue(undefined),
        createClient: () =>
          createMockClient(
            [
              {
                Success: false,
                ErrorMessage:
                  'COMPILATION_PROVIDER_UNAVAILABLE: No compilation provider is registered. Check initialization.',
              },
            ],
            recordedMethods,
          ).client,
        sleepFn: jest.fn(),
        nowFn: () => 0,
      }),
    ).rejects.toThrow(
      'execute-dynamic-code launch readiness probe failed: COMPILATION_PROVIDER_UNAVAILABLE: No compilation provider is registered. Check initialization.',
    );

    expect(recordedMethods).toEqual(['execute-dynamic-code']);
  });

  it('retries Unity JSON-RPC errors raised during startup until success', async () => {
    const recordedMethods: string[] = [];
    const responses: Array<MockReadinessResponse | Error> = [
      new Error('Unity error: Internal error (can only be called from the main thread.)'),
      { Success: true },
    ];
    let sleepCount = 0;

    await waitForDynamicCodeReadyAfterLaunch('/project', {
      resolveUnityPortFn: jest.fn().mockResolvedValue(8711),
      validateConnectedProjectFn: jest.fn().mockResolvedValue(undefined),
      createClient: () => createMockClient(responses, recordedMethods).client,
      sleepFn: jest.fn().mockImplementation((): Promise<void> => {
        sleepCount++;
        return Promise.resolve();
      }),
      nowFn: (() => {
        let now = 0;
        return (): number => {
          now += 100;
          return now;
        };
      })(),
    });

    expect(recordedMethods).toEqual(['execute-dynamic-code', 'execute-dynamic-code']);
    expect(sleepCount).toBe(1);
  });

  it('does not retry non-transient Unity JSON-RPC errors', async () => {
    const recordedMethods: string[] = [];

    await expect(
      waitForDynamicCodeReadyAfterLaunch('/project', {
        resolveUnityPortFn: jest.fn().mockResolvedValue(8711),
        validateConnectedProjectFn: jest.fn().mockResolvedValue(undefined),
        createClient: () =>
          createMockClient(
            [new Error('Unity error: Internal error (Object reference not set to an instance)')],
            recordedMethods,
          ).client,
        sleepFn: jest.fn(),
        nowFn: () => 0,
      }),
    ).rejects.toThrow('Unity error: Internal error (Object reference not set to an instance)');

    expect(recordedMethods).toEqual(['execute-dynamic-code']);
  });

  it('retries indeterminate payloads until success', async () => {
    const recordedMethods: string[] = [];
    const responses: Array<MockReadinessResponse | Error> = [{}, { Success: true }];
    let sleepCount = 0;

    await waitForDynamicCodeReadyAfterLaunch('/project', {
      resolveUnityPortFn: jest.fn().mockResolvedValue(8711),
      validateConnectedProjectFn: jest.fn().mockResolvedValue(undefined),
      createClient: () => createMockClient(responses, recordedMethods).client,
      sleepFn: jest.fn().mockImplementation((): Promise<void> => {
        sleepCount++;
        return Promise.resolve();
      }),
      nowFn: (() => {
        let now = 0;
        return (): number => {
          now += 100;
          return now;
        };
      })(),
    });

    expect(recordedMethods).toEqual(['execute-dynamic-code', 'execute-dynamic-code']);
    expect(sleepCount).toBe(1);
  });

  it('retries successful probes until RequestTotal settles below threshold', async () => {
    const recordedMethods: string[] = [];
    const responses: Array<MockReadinessResponse | Error> = [
      {
        Success: true,
        Timings: ['[Perf] RequestTotal: 420.0ms'],
      },
      {
        Success: true,
        Timings: ['[Perf] RequestTotal: 180.0ms'],
      },
    ];
    let sleepCount = 0;

    await waitForDynamicCodeReadyAfterLaunch('/project', {
      resolveUnityPortFn: jest.fn().mockResolvedValue(8711),
      validateConnectedProjectFn: jest.fn().mockResolvedValue(undefined),
      createClient: () => createMockClient(responses, recordedMethods).client,
      sleepFn: jest.fn().mockImplementation((): Promise<void> => {
        sleepCount++;
        return Promise.resolve();
      }),
      nowFn: (() => {
        let now = 0;
        return (): number => {
          now += 100;
          return now;
        };
      })(),
    });

    expect(recordedMethods).toEqual(['execute-dynamic-code', 'execute-dynamic-code']);
    expect(sleepCount).toBe(1);
  });

  it('accepts successful probes when RequestTotal timing is missing', async () => {
    const recordedMethods: string[] = [];

    await waitForDynamicCodeReadyAfterLaunch('/project', {
      resolveUnityPortFn: jest.fn().mockResolvedValue(8711),
      validateConnectedProjectFn: jest.fn().mockResolvedValue(undefined),
      createClient: () =>
        createMockClient([{ Success: true, Timings: ['[Perf] Build: 50.0ms'] }], recordedMethods)
          .client,
      sleepFn: jest.fn(),
      nowFn: () => 0,
    });

    expect(recordedMethods).toEqual(['execute-dynamic-code']);
  });

  it('returns after settle timeout even when RequestTotal stays above threshold', async () => {
    const recordedMethods: string[] = [];
    const responses: Array<MockReadinessResponse | Error> = [
      { Success: true, Timings: ['[Perf] RequestTotal: 420.0ms'] },
      { Success: true, Timings: ['[Perf] RequestTotal: 410.0ms'] },
      { Success: true, Timings: ['[Perf] RequestTotal: 405.0ms'] },
    ];
    let sleepCount = 0;

    await waitForDynamicCodeReadyAfterLaunch('/project', {
      resolveUnityPortFn: jest.fn().mockResolvedValue(8711),
      validateConnectedProjectFn: jest.fn().mockResolvedValue(undefined),
      createClient: () => createMockClient(responses, recordedMethods).client,
      sleepFn: jest.fn().mockImplementation((): Promise<void> => {
        sleepCount++;
        return Promise.resolve();
      }),
      nowFn: (() => {
        const values = [0, 100, 1100, 2100, 11100, 11200];
        let index = 0;
        return (): number => values[Math.min(index++, values.length - 1)];
      })(),
    });

    expect(recordedMethods).toEqual(['execute-dynamic-code', 'execute-dynamic-code']);
    expect(sleepCount).toBe(1);
  });

  it('does not retry project mismatch errors', async () => {
    await expect(
      waitForDynamicCodeReadyAfterLaunch('/project', {
        resolveUnityPortFn: jest.fn().mockResolvedValue(8711),
        validateConnectedProjectFn: jest
          .fn()
          .mockRejectedValue(new ProjectMismatchError('/expected', '/actual')),
        createClient: () => createMockClient([], []).client,
        sleepFn: jest.fn(),
        nowFn: () => 0,
      }),
    ).rejects.toThrow(ProjectMismatchError);
  });
});
