import { execFile } from 'node:child_process';
import { promisify } from 'node:util';

const execFileAsync = promisify(execFile);
const WINDOWS_PROCESS_QUERY =
  'Get-CimInstance Win32_Process -Filter "name = \'Unity.exe\'" | Select-Object ProcessId, CommandLine | ConvertTo-Json -Compress';

interface RunningUnityProcess {
  pid: number;
}

interface RawUnityProcess {
  pid: number;
  commandLine: string;
}

interface UnityProcessCommand {
  command: string;
  args: string[];
}

interface UnityProcessDependencies {
  platform: NodeJS.Platform;
  runCommand: (command: string, args: string[]) => Promise<string>;
}

const defaultDependencies: UnityProcessDependencies = {
  platform: process.platform,
  runCommand: runUnityProcessQuery,
};

export function buildUnityProcessCommand(platform: NodeJS.Platform): UnityProcessCommand | null {
  if (platform === 'darwin') {
    return {
      command: 'ps',
      args: ['-Ao', 'pid=,command='],
    };
  }

  if (platform === 'linux') {
    return {
      command: 'ps',
      args: ['-eo', 'pid=,args='],
    };
  }

  if (platform === 'win32') {
    return {
      command: 'powershell.exe',
      args: ['-NoProfile', '-NonInteractive', '-Command', WINDOWS_PROCESS_QUERY],
    };
  }

  return null;
}

export function parseUnityProcesses(platform: NodeJS.Platform, output: string): RawUnityProcess[] {
  if (platform === 'win32') {
    return parseWindowsUnityProcesses(output);
  }

  return parsePsUnityProcesses(output);
}

export function tokenizeCommandLine(commandLine: string): string[] {
  const tokens: string[] = [];
  let current = '';
  let inQuotes = false;

  for (let i = 0; i < commandLine.length; i++) {
    const character = commandLine[i];

    if (character === '"') {
      inQuotes = !inQuotes;
      continue;
    }

    if (!inQuotes && /\s/.test(character)) {
      if (current.length > 0) {
        tokens.push(current);
        current = '';
      }
      continue;
    }

    current += character;
  }

  if (current.length > 0) {
    tokens.push(current);
  }

  return tokens;
}

export function extractUnityProjectPath(commandLine: string): string | null {
  const tokens = tokenizeCommandLine(commandLine);

  for (let i = 0; i < tokens.length; i++) {
    const token = tokens[i].toLowerCase();
    if (token !== '-projectpath') {
      continue;
    }

    const projectPath = tokens[i + 1];
    return projectPath ?? null;
  }

  return null;
}

export function normalizeUnityProjectPath(projectPath: string, platform: NodeJS.Platform): string {
  const normalizedSeparators = projectPath.replace(/\\/g, '/').replace(/\/+$/, '');
  if (platform === 'win32') {
    return normalizedSeparators.toLowerCase();
  }

  return normalizedSeparators;
}

export function isUnityProcessForProject(
  commandLine: string,
  projectRoot: string,
  platform: NodeJS.Platform,
): boolean {
  if (platform !== 'win32') {
    return commandLineContainsProjectRoot(commandLine, projectRoot, platform);
  }

  const extractedProjectPath = extractUnityProjectPath(commandLine);
  if (extractedProjectPath === null) {
    return false;
  }

  return (
    normalizeUnityProjectPath(extractedProjectPath, platform) ===
    normalizeUnityProjectPath(projectRoot, platform)
  );
}

function commandLineContainsProjectRoot(
  commandLine: string,
  projectRoot: string,
  platform: NodeJS.Platform,
): boolean {
  const projectPathFlagIndex = commandLine.toLowerCase().indexOf(' -projectpath');
  if (projectPathFlagIndex === -1) {
    return false;
  }

  const normalizedProjectRoot = normalizeUnityProjectPath(projectRoot, platform);
  let projectRootIndex = commandLine.indexOf(normalizedProjectRoot, projectPathFlagIndex);

  while (projectRootIndex !== -1) {
    const beforeProjectRoot = commandLine[projectRootIndex - 1];
    const projectPathEndIndex = skipTrailingProjectPathSeparators(
      commandLine,
      projectRootIndex + normalizedProjectRoot.length,
    );
    if (
      isProjectPathBoundaryCharacter(beforeProjectRoot) &&
      isProjectPathTerminator(commandLine, projectPathEndIndex)
    ) {
      return true;
    }

    projectRootIndex = commandLine.indexOf(normalizedProjectRoot, projectRootIndex + 1);
  }

  return false;
}

function isProjectPathBoundaryCharacter(character: string | undefined): boolean {
  return character === undefined || /\s|["']/.test(character);
}

function skipTrailingProjectPathSeparators(commandLine: string, startIndex: number): number {
  let index = startIndex;

  while (readCharacterAt(commandLine, index) === '/') {
    index += 1;
  }

  return index;
}

function isProjectPathTerminator(commandLine: string, projectRootEndIndex: number): boolean {
  const character = readCharacterAt(commandLine, projectRootEndIndex);
  if (character === null) {
    return true;
  }

  if (character === '"' || character === "'") {
    return true;
  }

  if (!/\s/.test(character)) {
    return false;
  }

  for (let i = projectRootEndIndex; i < commandLine.length; i++) {
    const trailingCharacter = readCharacterAt(commandLine, i);
    if (trailingCharacter === null) {
      return true;
    }

    if (/\s/.test(trailingCharacter)) {
      continue;
    }

    return trailingCharacter === '-';
  }

  return true;
}

function readCharacterAt(value: string, index: number): string | null {
  const character = value.slice(index, index + 1);
  if (character.length === 0) {
    return null;
  }

  return character;
}

export function isUnityEditorProcess(commandLine: string, platform: NodeJS.Platform): boolean {
  const lowerCommandLine = commandLine.toLowerCase();
  if (lowerCommandLine.length === 0) {
    return false;
  }

  const projectPathFlagIndex = lowerCommandLine.indexOf(' -projectpath');
  const executableSection =
    projectPathFlagIndex === -1
      ? lowerCommandLine
      : lowerCommandLine.slice(0, projectPathFlagIndex);

  if (platform === 'win32') {
    return executableSection.includes('unity.exe');
  }

  if (platform === 'darwin') {
    return executableSection.includes('/unity.app/contents/macos/unity');
  }

  if (platform === 'linux') {
    return (
      executableSection.endsWith('/unity') ||
      executableSection.endsWith('/unity-editor') ||
      executableSection.includes('/editor/unity')
    );
  }

  return false;
}

export async function findRunningUnityProcessForProject(
  projectRoot: string,
  dependencies: UnityProcessDependencies = defaultDependencies,
): Promise<RunningUnityProcess | null> {
  const unityProcessCommand = buildUnityProcessCommand(dependencies.platform);
  if (unityProcessCommand === null) {
    return null;
  }

  const output = await dependencies.runCommand(
    unityProcessCommand.command,
    unityProcessCommand.args,
  );
  const runningProcesses = parseUnityProcesses(dependencies.platform, output);
  const matchingProcess = runningProcesses.find(
    (processInfo) =>
      isUnityEditorProcess(processInfo.commandLine, dependencies.platform) &&
      isUnityProcessForProject(processInfo.commandLine, projectRoot, dependencies.platform),
  );

  if (matchingProcess === undefined) {
    return null;
  }

  return {
    pid: matchingProcess.pid,
  };
}

async function runUnityProcessQuery(command: string, args: string[]): Promise<string> {
  const { stdout } = await execFileAsync(command, args, {
    encoding: 'utf8',
    maxBuffer: 1024 * 1024,
  });
  return stdout;
}

function parsePsUnityProcesses(output: string): RawUnityProcess[] {
  return output
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.length > 0)
    .map((line) => {
      const match = line.match(/^(\d+)\s+(.+)$/);
      if (match === null) {
        return null;
      }

      return {
        pid: Number.parseInt(match[1], 10),
        commandLine: match[2],
      };
    })
    .filter((processInfo): processInfo is RawUnityProcess => processInfo !== null);
}

function parseWindowsUnityProcesses(output: string): RawUnityProcess[] {
  const trimmed = output.trim();
  if (trimmed.length === 0) {
    return [];
  }

  const parsed = JSON.parse(trimmed) as WindowsUnityProcessJson | WindowsUnityProcessJson[];
  const processArray = Array.isArray(parsed) ? parsed : [parsed];

  return processArray.filter(isWindowsUnityProcessWithCommandLine).map((processInfo) => ({
    pid: processInfo.ProcessId,
    commandLine: processInfo.CommandLine,
  }));
}

interface WindowsUnityProcessJson {
  ProcessId: number;
  CommandLine?: string;
}

function isWindowsUnityProcessWithCommandLine(
  processInfo: WindowsUnityProcessJson,
): processInfo is WindowsUnityProcessJson & { CommandLine: string } {
  return typeof processInfo.ProcessId === 'number' && typeof processInfo.CommandLine === 'string';
}
