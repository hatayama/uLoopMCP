/**
 * Project validation for CLI.
 * Verifies that the connected Unity instance belongs to the expected project.
 */

// Paths come from trusted Unity responses and validated project root, console.error for user warnings
/* eslint-disable security/detect-non-literal-fs-filename, no-console */

import assert from 'node:assert';
import { realpath } from 'fs/promises';
import { dirname } from 'path';
import { DirectUnityClient } from './direct-unity-client.js';

export class ProjectMismatchError extends Error {
  constructor(
    public readonly expectedProjectRoot: string,
    public readonly connectedProjectRoot: string,
  ) {
    super('PROJECT_MISMATCH');
  }
}

interface GetVersionResponse {
  DataPath: string;
}

const JSON_RPC_METHOD_NOT_FOUND = -32601;

async function normalizePath(path: string): Promise<string> {
  const resolved = await realpath(path);
  return resolved.replace(/\/+$/, '');
}

export async function validateConnectedProject(
  client: DirectUnityClient,
  expectedProjectRoot: string,
): Promise<void> {
  assert(client.isConnected(), 'client must be connected before validation');

  let response: GetVersionResponse;
  try {
    response = await client.sendRequest<GetVersionResponse>('get-version', {});
  } catch (error) {
    // Method not found: old uLoopMCP version without get-version tool
    if (
      error instanceof Error &&
      (error.message.includes(`${JSON_RPC_METHOD_NOT_FOUND}`) ||
        /method not found/i.test(error.message))
    ) {
      console.error(
        'Warning: Could not verify project identity (get-version not available). Consider updating uLoopMCP package.',
      );
      return;
    }
    throw error;
  }

  if (typeof response?.DataPath !== 'string' || response.DataPath.length === 0) {
    console.error('Warning: Could not verify project identity (invalid get-version response).');
    return;
  }

  const connectedProjectRoot = dirname(response.DataPath);
  const normalizedExpected = await normalizePath(expectedProjectRoot);
  const normalizedConnected = await normalizePath(connectedProjectRoot);

  if (normalizedExpected !== normalizedConnected) {
    throw new ProjectMismatchError(normalizedExpected, normalizedConnected);
  }
}
