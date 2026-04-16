const mockResolveUnityConnection = jest.fn();
const mockValidateProjectPath = jest.fn<string, [string]>();
const mockFindUnityProjectRoot = jest.fn<string | null, []>();
const mockExistsSync = jest.fn<boolean, [string]>();

jest.mock('../port-resolver.js', () => ({
  resolveUnityConnection: (...args: unknown[]) => mockResolveUnityConnection(...args),
  validateProjectPath: (projectPath: string): string => mockValidateProjectPath(projectPath),
  UnityNotRunningError: class UnityNotRunningError extends Error {},
  UnityServerNotRunningError: class UnityServerNotRunningError extends Error {},
}));

jest.mock('../project-root.js', () => ({
  findUnityProjectRoot: (): string | null => mockFindUnityProjectRoot(),
}));

jest.mock('fs', () => ({
  existsSync: (path: string): boolean => mockExistsSync(path),
}));

import { executeToolCommand, listAvailableTools, syncTools } from '../execute-tool.js';

describe('busy state detection order', () => {
  beforeEach(() => {
    mockResolveUnityConnection.mockReset();
    mockResolveUnityConnection.mockRejectedValue(new Error('RESOLVE_CALLED_BEFORE_BUSY_CHECK'));

    mockValidateProjectPath.mockReset();
    mockValidateProjectPath.mockReturnValue('/project');

    mockFindUnityProjectRoot.mockReset();
    mockFindUnityProjectRoot.mockReturnValue('/project');

    mockExistsSync.mockReset();
    mockExistsSync.mockImplementation((path: string) => path.endsWith('compiling.lock'));
  });

  it('checks busy state before resolving port for tool execution', async () => {
    await expect(executeToolCommand('get-logs', {}, { projectPath: '/project' })).rejects.toThrow(
      'UNITY_COMPILING',
    );

    expect(mockResolveUnityConnection).not.toHaveBeenCalled();
  });

  it('checks busy state before resolving port for list', async () => {
    await expect(listAvailableTools({ projectPath: '/project' })).rejects.toThrow('UNITY_COMPILING');

    expect(mockResolveUnityConnection).not.toHaveBeenCalled();
  });

  it('checks busy state before resolving port for sync', async () => {
    await expect(syncTools({ projectPath: '/project' })).rejects.toThrow('UNITY_COMPILING');

    expect(mockResolveUnityConnection).not.toHaveBeenCalled();
  });

  it('skips busy state checks when an explicit port is provided for tool execution', async () => {
    mockResolveUnityConnection.mockRejectedValue(new Error('EXPLICIT_PORT_RESOLVED'));

    await expect(executeToolCommand('get-logs', {}, { port: '8711' })).rejects.toThrow(
      'EXPLICIT_PORT_RESOLVED',
    );

    expect(mockResolveUnityConnection).toHaveBeenCalledWith(8711, undefined);
  });

  it('preserves usage errors before busy state checks for tool execution', async () => {
    mockResolveUnityConnection.mockRejectedValue(
      new Error('Cannot specify both --port and --project-path. Use one or the other.'),
    );

    await expect(
      executeToolCommand('get-logs', {}, { port: '8711', projectPath: '/project' }),
    ).rejects.toThrow('Cannot specify both --port and --project-path. Use one or the other.');

    expect(mockResolveUnityConnection).toHaveBeenCalledWith(8711, '/project');
  });

  it('skips busy state checks when an explicit port is provided for list', async () => {
    mockResolveUnityConnection.mockRejectedValue(new Error('EXPLICIT_PORT_RESOLVED'));

    await expect(listAvailableTools({ port: '8711' })).rejects.toThrow('EXPLICIT_PORT_RESOLVED');

    expect(mockResolveUnityConnection).toHaveBeenCalledWith(8711, undefined);
  });

  it('preserves usage errors before busy state checks for list', async () => {
    mockResolveUnityConnection.mockRejectedValue(
      new Error('Cannot specify both --port and --project-path. Use one or the other.'),
    );

    await expect(listAvailableTools({ port: '8711', projectPath: '/project' })).rejects.toThrow(
      'Cannot specify both --port and --project-path. Use one or the other.',
    );

    expect(mockResolveUnityConnection).toHaveBeenCalledWith(8711, '/project');
  });

  it('skips busy state checks when an explicit port is provided for sync', async () => {
    mockResolveUnityConnection.mockRejectedValue(new Error('EXPLICIT_PORT_RESOLVED'));

    await expect(syncTools({ port: '8711' })).rejects.toThrow('EXPLICIT_PORT_RESOLVED');

    expect(mockResolveUnityConnection).toHaveBeenCalledWith(8711, undefined);
  });

  it('preserves usage errors before busy state checks for sync', async () => {
    mockResolveUnityConnection.mockRejectedValue(
      new Error('Cannot specify both --port and --project-path. Use one or the other.'),
    );

    await expect(syncTools({ port: '8711', projectPath: '/project' })).rejects.toThrow(
      'Cannot specify both --port and --project-path. Use one or the other.',
    );

    expect(mockResolveUnityConnection).toHaveBeenCalledWith(8711, '/project');
  });
});
