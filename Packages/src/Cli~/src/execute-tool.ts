/**
 * Tool execution logic for CLI.
 * Handles dynamic tool execution by connecting to Unity and sending requests.
 */

// CLI tools output to console by design, object keys come from Unity tool responses which are trusted,
// and lock file paths are constructed from trusted project root detection
/* eslint-disable no-console, security/detect-object-injection, security/detect-non-literal-fs-filename */

import * as readline from 'readline';
import { existsSync, readFileSync } from 'fs';
import { join } from 'path';
import * as semver from 'semver';
import { DirectUnityClient } from './direct-unity-client.js';
import { resolveUnityPort } from './port-resolver.js';
import { saveToolsCache, getCacheFilePath, ToolsCache, ToolDefinition } from './tool-cache.js';
import { VERSION } from './version.js';
import { createSpinner } from './spinner.js';
import { findUnityProjectRoot } from './project-root.js';

/**
 * Suppress stdin echo during async operation to prevent escape sequences from being displayed.
 * Returns a cleanup function to restore stdin state.
 */
function suppressStdinEcho(): () => void {
  if (!process.stdin.isTTY) {
    return () => {};
  }

  const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout,
    terminal: false,
  });

  process.stdin.setRawMode(true);
  process.stdin.resume();

  const onData = (data: Buffer): void => {
    // Ctrl+C (0x03) should trigger process exit
    if (data[0] === 0x03) {
      process.exit(130);
    }
  };
  process.stdin.on('data', onData);

  return () => {
    process.stdin.off('data', onData);
    process.stdin.setRawMode(false);
    process.stdin.pause();
    rl.close();
  };
}

export interface GlobalOptions {
  port?: string;
}

const RETRY_DELAY_MS = 500;
const MAX_RETRIES = 3;
const DOMAIN_RELOAD_WAIT_TIMEOUT_MS = 10000;
const DOMAIN_RELOAD_WAIT_POLL_INTERVAL_MS = 100;
const DOMAIN_RELOAD_START_GRACE_MS = 3000;

interface CompileExecutionOptions {
  forceRecompile: boolean;
  waitForDomainReload: boolean;
}

function toBoolean(value: unknown): boolean {
  if (typeof value === 'boolean') {
    return value;
  }

  if (typeof value === 'string') {
    return value.toLowerCase() === 'true';
  }

  return false;
}

function getCompileExecutionOptions(
  toolName: string,
  params: Record<string, unknown>,
): CompileExecutionOptions {
  if (toolName !== 'compile') {
    return {
      forceRecompile: false,
      waitForDomainReload: false,
    };
  }

  return {
    forceRecompile: toBoolean(params['ForceRecompile']),
    waitForDomainReload: toBoolean(params['WaitForDomainReload']),
  };
}

function createCompileRequestId(): string {
  const timestamp = Date.now();
  const randomToken = Math.floor(Math.random() * 1000000)
    .toString()
    .padStart(6, '0');
  return `compile_${timestamp}_${randomToken}`;
}

function ensureCompileRequestId(params: Record<string, unknown>): string {
  const existingRequestId = params['RequestId'];
  if (typeof existingRequestId === 'string' && existingRequestId.length > 0) {
    return existingRequestId;
  }

  const requestId = createCompileRequestId();
  params['RequestId'] = requestId;
  return requestId;
}

function getCompileResultFilePath(projectRoot: string, requestId: string): string {
  return join(projectRoot, 'Temp', 'uLoopMCP', 'compile-results', `${requestId}.json`);
}

function isUnityBusyByLockFiles(projectRoot: string): boolean {
  const compilingLock = join(projectRoot, 'Temp', 'compiling.lock');
  if (existsSync(compilingLock)) {
    return true;
  }

  const domainReloadLock = join(projectRoot, 'Temp', 'domainreload.lock');
  if (existsSync(domainReloadLock)) {
    return true;
  }

  const serverStartingLock = join(projectRoot, 'Temp', 'serverstarting.lock');
  return existsSync(serverStartingLock);
}

async function canConnectToUnity(port: number): Promise<boolean> {
  const client = new DirectUnityClient(port);
  try {
    await client.connect();
    return true;
  } catch {
    return false;
  } finally {
    client.disconnect();
  }
}

function tryReadCompileResult(
  projectRoot: string,
  requestId: string,
): Record<string, unknown> | undefined {
  const resultFilePath = getCompileResultFilePath(projectRoot, requestId);
  if (!existsSync(resultFilePath)) {
    return undefined;
  }

  const content = readFileSync(resultFilePath, 'utf-8');
  const parsed = JSON.parse(stripUtf8Bom(content)) as unknown;
  if (typeof parsed !== 'object' || parsed === null) {
    throw new Error(`Invalid compile result format: ${resultFilePath}`);
  }

  return parsed as Record<string, unknown>;
}

function stripUtf8Bom(content: string): string {
  if (content.charCodeAt(0) === 0xfeff) {
    return content.slice(1);
  }

  return content;
}

async function waitForDomainReloadToSettle(projectRoot: string, port: number): Promise<void> {
  let waitedMs = 0;
  let busyObserved = false;

  while (waitedMs < DOMAIN_RELOAD_WAIT_TIMEOUT_MS) {
    const isBusy = isUnityBusyByLockFiles(projectRoot);
    if (isBusy) {
      busyObserved = true;
    }

    if (!busyObserved && waitedMs >= DOMAIN_RELOAD_START_GRACE_MS) {
      return;
    }

    if (busyObserved && !isBusy) {
      const reachable = await canConnectToUnity(port);
      if (reachable) {
        return;
      }
    }

    await sleep(DOMAIN_RELOAD_WAIT_POLL_INTERVAL_MS);
    waitedMs += DOMAIN_RELOAD_WAIT_POLL_INTERVAL_MS;
  }

  throw new Error(
    `Domain reload wait timed out after ${DOMAIN_RELOAD_WAIT_TIMEOUT_MS}ms. Run 'uloop fix' and retry.`,
  );
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function isRetryableError(error: unknown): boolean {
  if (!(error instanceof Error)) {
    return false;
  }
  const message = error.message;
  return (
    message.includes('ECONNREFUSED') ||
    message.includes('EADDRNOTAVAIL') ||
    message === 'UNITY_NO_RESPONSE'
  );
}

/**
 * Compare two semantic versions safely.
 * Returns true if v1 < v2, false otherwise.
 * Falls back to string comparison if versions are invalid.
 */
export function isVersionOlder(v1: string, v2: string): boolean {
  const parsed1 = semver.valid(v1);
  const parsed2 = semver.valid(v2);

  if (parsed1 && parsed2) {
    return semver.lt(parsed1, parsed2);
  }

  return v1 < v2;
}

/**
 * Print version mismatch warning to stderr.
 * Does not block execution - just warns the user.
 */
function printVersionWarning(cliVersion: string, serverVersion: string): void {
  const isCliOlder = isVersionOlder(cliVersion, serverVersion);
  const updateCommand = isCliOlder
    ? `npm install -g uloop-cli@${serverVersion}`
    : `Update uLoopMCP package to ${cliVersion} via Unity Package Manager`;

  console.error('\x1b[33m⚠️ Version mismatch detected!\x1b[0m');
  console.error(`   uloop-cli version:    ${cliVersion}`);
  console.error(`   uloop server version: ${serverVersion}`);
  console.error('');
  console.error('   This may cause unexpected behavior or errors.');
  console.error('');
  console.error(`   ${isCliOlder ? 'To update CLI:' : 'To update server:'} ${updateCommand}`);
  console.error('');
}

/**
 * Check server version from response and print warning if mismatched.
 */
function checkServerVersion(result: Record<string, unknown>): void {
  const serverVersion = result['Ver'] as string | undefined;
  if (serverVersion && serverVersion !== VERSION) {
    printVersionWarning(VERSION, serverVersion);
  }
}

/**
 * Check if Unity is in a busy state (compiling, reloading, or server starting).
 * Throws an error with appropriate message if busy.
 */
function checkUnityBusyState(): void {
  const projectRoot = findUnityProjectRoot();
  if (projectRoot === null) {
    return;
  }

  const compilingLock = join(projectRoot, 'Temp', 'compiling.lock');
  if (existsSync(compilingLock)) {
    throw new Error('UNITY_COMPILING');
  }

  const domainReloadLock = join(projectRoot, 'Temp', 'domainreload.lock');
  if (existsSync(domainReloadLock)) {
    throw new Error('UNITY_DOMAIN_RELOAD');
  }

  const serverStartingLock = join(projectRoot, 'Temp', 'serverstarting.lock');
  if (existsSync(serverStartingLock)) {
    throw new Error('UNITY_SERVER_STARTING');
  }
}

export async function executeToolCommand(
  toolName: string,
  params: Record<string, unknown>,
  globalOptions: GlobalOptions,
): Promise<void> {
  let portNumber: number | undefined;
  if (globalOptions.port) {
    const parsed = parseInt(globalOptions.port, 10);
    if (isNaN(parsed)) {
      throw new Error(`Invalid port number: ${globalOptions.port}`);
    }
    portNumber = parsed;
  }
  const port = await resolveUnityPort(portNumber);
  const compileOptions = getCompileExecutionOptions(toolName, params);
  const shouldWaitForDomainReload =
    compileOptions.forceRecompile && compileOptions.waitForDomainReload;
  const compileRequestId = shouldWaitForDomainReload ? ensureCompileRequestId(params) : undefined;

  const restoreStdin = suppressStdinEcho();
  const spinner = createSpinner('Connecting to Unity...');

  let lastError: unknown;
  let immediateResult: Record<string, unknown> | undefined;
  const projectRoot = findUnityProjectRoot();

  for (let attempt = 0; attempt <= MAX_RETRIES; attempt++) {
    checkUnityBusyState();

    const client = new DirectUnityClient(port);
    try {
      await client.connect();

      spinner.update(`Executing ${toolName}...`);
      const result = await client.sendRequest<Record<string, unknown>>(toolName, params);

      if (result === undefined || result === null) {
        throw new Error('UNITY_NO_RESPONSE');
      }

      immediateResult = result;
      if (!shouldWaitForDomainReload) {
        spinner.stop();
        restoreStdin();

        checkServerVersion(result);
        console.log(JSON.stringify(result, null, 2));
        return;
      }

      break;
    } catch (error) {
      lastError = error;
      client.disconnect();

      if (shouldWaitForDomainReload) {
        break;
      }

      if (!isRetryableError(error) || attempt >= MAX_RETRIES) {
        break;
      }
      spinner.update('Retrying connection...');
      await sleep(RETRY_DELAY_MS);
    } finally {
      client.disconnect();
    }
  }

  if (shouldWaitForDomainReload && compileRequestId) {
    if (projectRoot === null) {
      if (immediateResult !== undefined) {
        spinner.stop();
        restoreStdin();
        checkServerVersion(immediateResult);
        console.log(JSON.stringify(immediateResult, null, 2));
        return;
      }
    } else {
      try {
        spinner.update('Waiting for domain reload to complete...');
        await waitForDomainReloadToSettle(projectRoot, port);

        const storedResult = tryReadCompileResult(projectRoot, compileRequestId);
        const finalResult = storedResult ?? immediateResult;
        if (finalResult !== undefined) {
          spinner.stop();
          restoreStdin();
          checkServerVersion(finalResult);
          console.log(JSON.stringify(finalResult, null, 2));
          return;
        }
      } catch (error) {
        lastError = error;
      }
    }
  }

  spinner.stop();
  restoreStdin();
  if (lastError === undefined) {
    throw new Error('Tool execution failed without error details.');
  }
  if (lastError instanceof Error) {
    throw lastError;
  }
  if (typeof lastError === 'string') {
    throw new Error(lastError);
  }

  const serializedError = JSON.stringify(lastError);
  throw new Error(serializedError ?? 'Unknown error');
}

export async function listAvailableTools(globalOptions: GlobalOptions): Promise<void> {
  let portNumber: number | undefined;
  if (globalOptions.port) {
    const parsed = parseInt(globalOptions.port, 10);
    if (isNaN(parsed)) {
      throw new Error(`Invalid port number: ${globalOptions.port}`);
    }
    portNumber = parsed;
  }
  const port = await resolveUnityPort(portNumber);

  const restoreStdin = suppressStdinEcho();
  const spinner = createSpinner('Connecting to Unity...');

  let lastError: unknown;
  for (let attempt = 0; attempt <= MAX_RETRIES; attempt++) {
    checkUnityBusyState();

    const client = new DirectUnityClient(port);
    try {
      await client.connect();

      spinner.update('Fetching tool list...');
      const result = await client.sendRequest<{
        Tools: Array<{ name: string; description: string }>;
      }>('get-tool-details', { IncludeDevelopmentOnly: false });

      if (!result.Tools || !Array.isArray(result.Tools)) {
        throw new Error('Unexpected response from Unity: missing Tools array');
      }

      // Success - stop spinner and output result
      spinner.stop();
      restoreStdin();
      for (const tool of result.Tools) {
        console.log(`  - ${tool.name}`);
      }
      return;
    } catch (error) {
      lastError = error;
      client.disconnect();

      if (!isRetryableError(error) || attempt >= MAX_RETRIES) {
        break;
      }
      spinner.update('Retrying connection...');
      await sleep(RETRY_DELAY_MS);
    } finally {
      client.disconnect();
    }
  }

  spinner.stop();
  restoreStdin();
  throw lastError;
}

interface UnityToolInfo {
  name: string;
  description: string;
  parameterSchema: {
    Properties: Record<string, UnityPropertyInfo>;
    Required?: string[];
  };
}

interface UnityPropertyInfo {
  Type: string;
  Description?: string;
  DefaultValue?: unknown;
  Enum?: string[] | null;
}

function convertProperties(
  unityProps: Record<string, UnityPropertyInfo>,
): Record<string, ToolDefinition['inputSchema']['properties'][string]> {
  const result: Record<string, ToolDefinition['inputSchema']['properties'][string]> = {};
  for (const [key, prop] of Object.entries(unityProps)) {
    result[key] = {
      type: prop.Type?.toLowerCase() ?? 'string',
      description: prop.Description,
      default: prop.DefaultValue,
      enum: prop.Enum ?? undefined,
    };
  }
  return result;
}

export async function syncTools(globalOptions: GlobalOptions): Promise<void> {
  let portNumber: number | undefined;
  if (globalOptions.port) {
    const parsed = parseInt(globalOptions.port, 10);
    if (isNaN(parsed)) {
      throw new Error(`Invalid port number: ${globalOptions.port}`);
    }
    portNumber = parsed;
  }
  const port = await resolveUnityPort(portNumber);

  const restoreStdin = suppressStdinEcho();
  const spinner = createSpinner('Connecting to Unity...');

  let lastError: unknown;
  for (let attempt = 0; attempt <= MAX_RETRIES; attempt++) {
    checkUnityBusyState();

    const client = new DirectUnityClient(port);
    try {
      await client.connect();

      spinner.update('Syncing tools...');
      const result = await client.sendRequest<{
        Tools: UnityToolInfo[];
        Ver?: string;
      }>('get-tool-details', { IncludeDevelopmentOnly: false });

      spinner.stop();
      if (!result.Tools || !Array.isArray(result.Tools)) {
        restoreStdin();
        throw new Error('Unexpected response from Unity: missing Tools array');
      }

      const cache: ToolsCache = {
        version: VERSION,
        serverVersion: result.Ver,
        updatedAt: new Date().toISOString(),
        tools: result.Tools.map((tool) => ({
          name: tool.name,
          description: tool.description,
          inputSchema: {
            type: 'object',
            properties: convertProperties(tool.parameterSchema.Properties),
            required: tool.parameterSchema.Required,
          },
        })),
      };

      saveToolsCache(cache);

      console.log(`Synced ${cache.tools.length} tools to ${getCacheFilePath()}`);
      console.log('\nTools:');
      for (const tool of cache.tools) {
        console.log(`  - ${tool.name}`);
      }
      restoreStdin();
      return;
    } catch (error) {
      lastError = error;
      client.disconnect();

      if (!isRetryableError(error) || attempt >= MAX_RETRIES) {
        break;
      }
      spinner.update('Retrying connection...');
      await sleep(RETRY_DELAY_MS);
    } finally {
      client.disconnect();
    }
  }

  spinner.stop();
  restoreStdin();
  throw lastError;
}
