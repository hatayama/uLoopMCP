import { mkdirSync, mkdtempSync, realpathSync, rmSync, writeFileSync } from 'fs';
import { tmpdir } from 'os';
import { join } from 'path';
import { createProjectIpcEndpoint } from '../ipc-endpoint.js';
import { validateProjectPath, resolveUnityConnection, resolveUnityPort } from '../port-resolver.js';

/* eslint-disable security/detect-non-literal-fs-filename */

describe('validateProjectPath', () => {
  it('throws when path does not exist', () => {
    expect(() => validateProjectPath('/nonexistent/path/to/project')).toThrow(
      'Path does not exist: /nonexistent/path/to/project',
    );
  });

  it('throws when path is not a Unity project', () => {
    expect(() => validateProjectPath(tmpdir())).toThrow('Not a Unity project');
  });
});

describe('resolveUnityPort', () => {
  it('throws when both port and projectPath are specified', async () => {
    await expect(resolveUnityPort(8700, '/some/path')).rejects.toThrow(
      'Cannot specify both --port and --project-path',
    );
  });

  it('returns explicit debug port when only port is specified', async () => {
    const port = await resolveUnityPort(8711);
    expect(port).toBe(8711);
  });

  it('does not expose a project settings port for the normal project route', async () => {
    const tempProjectRoot = createTempUnityProject('unity-port-test-');
    try {
      writeSettings(tempProjectRoot, {
        isServerRunning: true,
        customPort: 8711,
      });

      await expect(resolveUnityPort(undefined, tempProjectRoot)).rejects.toThrow(
        'Unity connection for a project is no longer represented by a TCP port',
      );
    } finally {
      rmSync(tempProjectRoot, { recursive: true });
    }
  });
});

describe('resolveUnityConnection', () => {
  let tempProjectRoot: string;

  beforeEach(() => {
    tempProjectRoot = createTempUnityProject('unity-connection-test-');
  });

  afterEach(() => {
    rmSync(tempProjectRoot, { recursive: true });
  });

  it('uses project IPC endpoint instead of settings customPort for the normal route', async () => {
    writeSettings(tempProjectRoot, {
      isServerRunning: true,
      customPort: 8730,
    });

    const connection = await resolveUnityConnection(undefined, tempProjectRoot);
    const canonicalProjectRoot = realpathSync(tempProjectRoot);

    expect(connection.port).toBeNull();
    expect(connection.endpoint).toEqual(createProjectIpcEndpoint(canonicalProjectRoot));
    expect(connection.projectRoot).toBe(canonicalProjectRoot);
  });

  it('keeps explicit TCP port as debug opt-in', async () => {
    const connection = await resolveUnityConnection(8730, undefined);

    expect(connection).toEqual({
      endpoint: {
        kind: 'tcp',
        host: '127.0.0.1',
        port: 8730,
      },
      port: 8730,
      projectRoot: null,
      requestMetadata: null,
      shouldValidateProject: false,
    });
  });

  it('uses fast request metadata when settings identity matches the canonical project', async () => {
    const canonicalProjectRoot = realpathSync(tempProjectRoot);
    writeSettings(tempProjectRoot, {
      customPort: 8730,
      projectRootPath: canonicalProjectRoot,
      serverSessionId: 'session-123',
    });

    const connection = await resolveUnityConnection(undefined, tempProjectRoot);

    expect(connection).toEqual({
      endpoint: createProjectIpcEndpoint(canonicalProjectRoot),
      port: null,
      projectRoot: canonicalProjectRoot,
      requestMetadata: {
        expectedProjectRoot: canonicalProjectRoot,
        expectedServerSessionId: 'session-123',
      },
      shouldValidateProject: false,
    });
  });

  it('falls back to legacy validation when server session identity is missing', async () => {
    const canonicalProjectRoot = realpathSync(tempProjectRoot);
    writeSettings(tempProjectRoot, {
      customPort: 8731,
      projectRootPath: canonicalProjectRoot,
    });

    const connection = await resolveUnityConnection(undefined, tempProjectRoot);

    expect(connection.endpoint).toEqual(createProjectIpcEndpoint(canonicalProjectRoot));
    expect(connection.port).toBeNull();
    expect(connection.projectRoot).toBe(canonicalProjectRoot);
    expect(connection.requestMetadata).toBeNull();
    expect(connection.shouldValidateProject).toBe(true);
  });

  it('falls back to legacy validation when settings project root differs from the resolved project', async () => {
    const canonicalProjectRoot = realpathSync(tempProjectRoot);
    writeSettings(tempProjectRoot, {
      customPort: 8732,
      projectRootPath: `${canonicalProjectRoot}-other`,
      serverSessionId: 'session-456',
    });

    const connection = await resolveUnityConnection(undefined, tempProjectRoot);

    expect(connection.endpoint).toEqual(createProjectIpcEndpoint(canonicalProjectRoot));
    expect(connection.port).toBeNull();
    expect(connection.projectRoot).toBe(canonicalProjectRoot);
    expect(connection.requestMetadata).toBeNull();
    expect(connection.shouldValidateProject).toBe(true);
  });

  it('falls back to temp settings when primary settings file contains invalid JSON', async () => {
    const canonicalProjectRoot = realpathSync(tempProjectRoot);
    writeFileSync(join(tempProjectRoot, 'UserSettings/UnityMcpSettings.json'), 'not valid json{{{');
    writeFileSync(
      join(tempProjectRoot, 'UserSettings/UnityMcpSettings.json.tmp'),
      JSON.stringify({
        customPort: 8726,
        projectRootPath: canonicalProjectRoot,
        serverSessionId: 'session-from-temp',
      }),
    );

    const connection = await resolveUnityConnection(undefined, tempProjectRoot);

    expect(connection.endpoint).toEqual(createProjectIpcEndpoint(canonicalProjectRoot));
    expect(connection.requestMetadata).toEqual({
      expectedProjectRoot: canonicalProjectRoot,
      expectedServerSessionId: 'session-from-temp',
    });
  });
});

function createTempUnityProject(prefix: string): string {
  const tempProjectRoot = mkdtempSync(join(tmpdir(), prefix));
  mkdirSync(join(tempProjectRoot, 'Assets'));
  mkdirSync(join(tempProjectRoot, 'ProjectSettings'));
  mkdirSync(join(tempProjectRoot, 'UserSettings'));
  return tempProjectRoot;
}

function writeSettings(tempProjectRoot: string, settings: Record<string, unknown>): void {
  writeFileSync(
    join(tempProjectRoot, 'UserSettings/UnityMcpSettings.json'),
    JSON.stringify(settings),
  );
}
