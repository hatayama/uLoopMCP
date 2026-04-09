const mockResolveUnityPort = jest.fn<Promise<number>, [number | undefined, string | undefined]>();
const mockValidateProjectPath = jest.fn<string, [string]>();
const mockFindUnityProjectRoot = jest.fn<string | null, []>();
const mockExistsSync = jest.fn<boolean, [string]>();

jest.mock('../port-resolver.js', () => ({
  resolveUnityPort: (explicitPort?: number, projectPath?: string): Promise<number> =>
    mockResolveUnityPort(explicitPort, projectPath),
  validateProjectPath: (projectPath: string): string => mockValidateProjectPath(projectPath),
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
    mockResolveUnityPort.mockReset();
    mockResolveUnityPort.mockRejectedValue(new Error('RESOLVE_CALLED_BEFORE_BUSY_CHECK'));

    mockValidateProjectPath.mockReset();
    mockValidateProjectPath.mockReturnValue('/project');

    mockFindUnityProjectRoot.mockReset();
    mockFindUnityProjectRoot.mockReturnValue('/project');

    mockExistsSync.mockReset();
    mockExistsSync.mockImplementation((path: string) => path.endsWith('serverstarting.lock'));
  });

  it('checks busy state before resolving port for tool execution', async () => {
    await expect(executeToolCommand('get-logs', {}, { projectPath: '/project' })).rejects.toThrow(
      'UNITY_SERVER_STARTING',
    );

    expect(mockResolveUnityPort).not.toHaveBeenCalled();
  });

  it('checks busy state before resolving port for list', async () => {
    await expect(listAvailableTools({ projectPath: '/project' })).rejects.toThrow(
      'UNITY_SERVER_STARTING',
    );

    expect(mockResolveUnityPort).not.toHaveBeenCalled();
  });

  it('checks busy state before resolving port for sync', async () => {
    await expect(syncTools({ projectPath: '/project' })).rejects.toThrow('UNITY_SERVER_STARTING');

    expect(mockResolveUnityPort).not.toHaveBeenCalled();
  });
});
