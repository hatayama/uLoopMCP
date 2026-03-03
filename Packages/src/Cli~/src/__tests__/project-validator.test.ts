import { mkdtempSync, mkdirSync, rmSync } from 'fs';
import { join } from 'path';
import { tmpdir } from 'os';
import { ProjectMismatchError, validateConnectedProject } from '../project-validator.js';
import { isTransportDisconnectError } from '../execute-tool.js';
import type { DirectUnityClient } from '../direct-unity-client.js';

function createMockClient(response?: unknown, error?: Error): DirectUnityClient {
  return {
    isConnected: () => true,
    sendRequest: jest.fn().mockImplementation(() => {
      if (error) {
        return Promise.reject(error);
      }
      return Promise.resolve(response);
    }),
    connect: jest.fn(),
    disconnect: jest.fn(),
  } as unknown as DirectUnityClient;
}

describe('validateConnectedProject', () => {
  let tempDirA: string;
  let tempDirB: string;

  beforeEach(() => {
    tempDirA = mkdtempSync(join(tmpdir(), 'project-a-'));
    tempDirB = mkdtempSync(join(tmpdir(), 'project-b-'));
    mkdirSync(join(tempDirA, 'Assets'));
    mkdirSync(join(tempDirB, 'Assets'));
  });

  afterEach(() => {
    rmSync(tempDirA, { recursive: true });
    rmSync(tempDirB, { recursive: true });
  });

  it('throws ProjectMismatchError when connected project differs from expected', async () => {
    const client = createMockClient({ DataPath: join(tempDirB, 'Assets') });

    await expect(validateConnectedProject(client, tempDirA)).rejects.toThrow(ProjectMismatchError);
  });

  it('does not throw when connected project matches expected', async () => {
    const client = createMockClient({ DataPath: join(tempDirA, 'Assets') });

    await expect(validateConnectedProject(client, tempDirA)).resolves.toBeUndefined();
  });

  it('normalizes paths with trailing separators', async () => {
    const client = createMockClient({ DataPath: join(tempDirA, 'Assets') });

    await expect(validateConnectedProject(client, tempDirA + '/')).resolves.toBeUndefined();
  });

  it('logs warning and continues when get-version returns Method not found', async () => {
    const client = createMockClient(undefined, new Error('Unity error: Method not found (-32601)'));
    const stderrSpy = jest.spyOn(console, 'error').mockImplementation();

    await expect(validateConnectedProject(client, tempDirA)).resolves.toBeUndefined();

    expect(stderrSpy).toHaveBeenCalledWith(
      expect.stringContaining('Could not verify project identity'),
    );
    stderrSpy.mockRestore();
  });

  it('re-throws non-Method-not-found errors', async () => {
    const client = createMockClient(undefined, new Error('Unity error: some other error'));

    await expect(validateConnectedProject(client, tempDirA)).rejects.toThrow(
      'Unity error: some other error',
    );
  });
});

describe('ProjectMismatchError', () => {
  it('is not a transport disconnect error', () => {
    const error = new ProjectMismatchError('/project/a', '/project/b');
    expect(isTransportDisconnectError(error)).toBe(false);
  });

  it('stores expected and connected project roots', () => {
    const error = new ProjectMismatchError('/expected/path', '/connected/path');
    expect(error.expectedProjectRoot).toBe('/expected/path');
    expect(error.connectedProjectRoot).toBe('/connected/path');
    expect(error.message).toBe('PROJECT_MISMATCH');
  });
});
