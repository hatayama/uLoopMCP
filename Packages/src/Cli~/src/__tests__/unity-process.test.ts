import {
  buildUnityProcessCommand,
  extractUnityProjectPath,
  findRunningUnityProcessForProject,
  isUnityEditorProcess,
  isUnityProcessForProject,
  normalizeUnityProjectPath,
  parseUnityProcesses,
  tokenizeCommandLine,
} from '../unity-process.js';

describe('buildUnityProcessCommand', () => {
  it('builds ps command for macOS', () => {
    expect(buildUnityProcessCommand('darwin')).toEqual({
      command: 'ps',
      args: ['-Ao', 'pid=,command='],
    });
  });

  it('builds powershell command for Windows', () => {
    expect(buildUnityProcessCommand('win32')).toEqual({
      command: 'powershell.exe',
      args: [
        '-NoProfile',
        '-NonInteractive',
        '-Command',
        'Get-CimInstance Win32_Process -Filter "name = \'Unity.exe\'" | Select-Object ProcessId, CommandLine | ConvertTo-Json -Compress',
      ],
    });
  });
});

describe('tokenizeCommandLine', () => {
  it('keeps quoted project path as one token', () => {
    expect(
      tokenizeCommandLine(
        '/Applications/Unity.app/Contents/MacOS/Unity -projectPath "/Users/me/My Project"',
      ),
    ).toEqual([
      '/Applications/Unity.app/Contents/MacOS/Unity',
      '-projectPath',
      '/Users/me/My Project',
    ]);
  });
});

describe('extractUnityProjectPath', () => {
  it('extracts macOS project path', () => {
    expect(
      extractUnityProjectPath(
        '/Applications/Unity.app/Contents/MacOS/Unity -projectPath /Users/me/project',
      ),
    ).toBe('/Users/me/project');
  });

  it('extracts Windows project path case-insensitively', () => {
    expect(
      extractUnityProjectPath(
        'C:\\Program Files\\Unity\\Editor\\Unity.exe -projectpath "C:\\Work\\My Project"',
      ),
    ).toBe('C:\\Work\\My Project');
  });
});

describe('normalizeUnityProjectPath', () => {
  it('normalizes Windows paths case-insensitively', () => {
    expect(normalizeUnityProjectPath('C:\\Work\\My Project\\', 'win32')).toBe('c:/work/my project');
  });
});

describe('parseUnityProcesses', () => {
  it('parses ps output', () => {
    expect(
      parseUnityProcesses(
        'darwin',
        '123 /Applications/Unity.app/Contents/MacOS/Unity -projectPath /Users/me/project\n',
      ),
    ).toEqual([
      {
        pid: 123,
        commandLine: '/Applications/Unity.app/Contents/MacOS/Unity -projectPath /Users/me/project',
      },
    ]);
  });

  it('parses Windows powershell JSON array output', () => {
    expect(
      parseUnityProcesses(
        'win32',
        '[{"ProcessId":101,"CommandLine":"C:\\\\Program Files\\\\Unity\\\\Editor\\\\Unity.exe -projectPath \\"C:\\\\Work\\\\Project A\\""}]',
      ),
    ).toEqual([
      {
        pid: 101,
        commandLine:
          'C:\\Program Files\\Unity\\Editor\\Unity.exe -projectPath "C:\\Work\\Project A"',
      },
    ]);
  });

  it('returns empty array when Windows output is empty', () => {
    expect(parseUnityProcesses('win32', '')).toEqual([]);
  });
});

describe('isUnityProcessForProject', () => {
  it('matches project path on macOS', () => {
    expect(
      isUnityProcessForProject(
        '/Applications/Unity.app/Contents/MacOS/Unity -projectPath "/Users/me/Project A"',
        '/Users/me/Project A',
        'darwin',
      ),
    ).toBe(true);
  });

  it('matches a macOS project path even when ps output has flattened quotes', () => {
    expect(
      isUnityProcessForProject(
        '/Applications/Unity.app/Contents/MacOS/Unity -projectPath /Users/me/My Project',
        '/Users/me/My Project',
        'darwin',
      ),
    ).toBe(true);
  });

  it('does not match a different macOS project that only shares the prefix', () => {
    expect(
      isUnityProcessForProject(
        '/Applications/Unity.app/Contents/MacOS/Unity -projectPath /Users/me/My Project Backup',
        '/Users/me/My Project',
        'darwin',
      ),
    ).toBe(false);
  });

  it('matches project path on Windows case-insensitively', () => {
    expect(
      isUnityProcessForProject(
        'C:\\Program Files\\Unity\\Editor\\Unity.exe -projectPath "C:\\Work\\Project A"',
        'c:/work/project a',
        'win32',
      ),
    ).toBe(true);
  });
});

describe('isUnityEditorProcess', () => {
  it('detects the Unity editor on macOS', () => {
    expect(
      isUnityEditorProcess(
        '/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity -projectPath "/Users/me/Project A"',
        'darwin',
      ),
    ).toBe(true);
  });

  it('rejects a non-Unity process on macOS even when projectPath is present', () => {
    expect(
      isUnityEditorProcess(
        '/usr/local/bin/custom-tool -projectPath "/Users/me/Project A"',
        'darwin',
      ),
    ).toBe(false);
  });

  it('detects the Unity editor on Windows without relying on command line casing', () => {
    expect(
      isUnityEditorProcess(
        'C:\\Program Files\\Unity\\Editor\\UNITY.EXE -projectPath "C:\\Work\\Project A"',
        'win32',
      ),
    ).toBe(true);
  });
});

describe('findRunningUnityProcessForProject', () => {
  it('returns null when no Unity process is running', async () => {
    const runCommand = jest.fn<Promise<string>, [string, string[]]>().mockResolvedValue('');

    await expect(
      findRunningUnityProcessForProject('/Users/me/project', {
        platform: 'darwin',
        runCommand,
      }),
    ).resolves.toBeNull();
  });

  it('returns matching Unity process on macOS', async () => {
    const runCommand = jest
      .fn<Promise<string>, [string, string[]]>()
      .mockResolvedValue(
        [
          '111 /Applications/Unity.app/Contents/MacOS/Unity -projectPath /Users/me/other',
          '222 /Applications/Unity.app/Contents/MacOS/Unity -projectPath "/Users/me/project"',
        ].join('\n'),
      );

    await expect(
      findRunningUnityProcessForProject('/Users/me/project', {
        platform: 'darwin',
        runCommand,
      }),
    ).resolves.toEqual({ pid: 222 });
  });

  it('returns a matching macOS Unity process when ps output has flattened quotes', async () => {
    const runCommand = jest
      .fn<Promise<string>, [string, string[]]>()
      .mockResolvedValue(
        '222 /Applications/Unity.app/Contents/MacOS/Unity -projectPath /Users/me/My Project',
      );

    await expect(
      findRunningUnityProcessForProject('/Users/me/My Project', {
        platform: 'darwin',
        runCommand,
      }),
    ).resolves.toEqual({ pid: 222 });
  });

  it('ignores non-Unity processes that happen to share the same projectPath', async () => {
    const runCommand = jest
      .fn<Promise<string>, [string, string[]]>()
      .mockResolvedValue(
        [
          '111 /usr/local/bin/custom-tool -projectPath "/Users/me/project"',
          '222 /Applications/Unity.app/Contents/MacOS/Unity -projectPath "/Users/me/project"',
        ].join('\n'),
      );

    await expect(
      findRunningUnityProcessForProject('/Users/me/project', {
        platform: 'darwin',
        runCommand,
      }),
    ).resolves.toEqual({ pid: 222 });
  });

  it('returns matching Unity process on Windows', async () => {
    const runCommand = jest
      .fn<Promise<string>, [string, string[]]>()
      .mockResolvedValue(
        '[{"ProcessId":333,"CommandLine":"C:\\\\Program Files\\\\Unity\\\\Editor\\\\UNITY.EXE -projectPath \\"C:\\\\Work\\\\My Project\\""}]',
      );

    await expect(
      findRunningUnityProcessForProject('c:/work/my project', {
        platform: 'win32',
        runCommand,
      }),
    ).resolves.toEqual({ pid: 333 });
  });
});
