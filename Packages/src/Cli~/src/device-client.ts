/**
 * TCP client for Device Agent communication.
 * Connects to a Device Agent running on a physical Android/iOS device over USB
 * via ADB port forwarding (Android) or iproxy (iOS).
 */

// Non-null assertions are used after TCP frame parsing where data existence is guaranteed by protocol
/* eslint-disable @typescript-eslint/no-non-null-assertion */

import assert from 'node:assert';
import * as net from 'net';
import { createFrame, parseFrameFromBuffer, extractFrameFromBuffer } from './simple-framer.js';
import { JsonRpcRequest, JsonRpcResponse } from './direct-unity-client.js';
import { VERSION } from './version.js';

const JSONRPC_VERSION = '2.0';

export const DEVICE_DEFAULT_HOST = '127.0.0.1';
export const DEVICE_DEFAULT_PORT = 8800;

const DEVICE_NETWORK_TIMEOUT_MS = 30000;

const AUTH_ERROR_CODE = -32001;
const INCOMPATIBLE_VERSION_ERROR_CODE = -32005;

export interface DeviceCapabilities {
  [key: string]: unknown;
}

export interface AuthLoginResult {
  capabilities: DeviceCapabilities;
}

export class DeviceClient {
  private socket: net.Socket | null = null;
  private requestId: number = 0;
  private receiveBuffer: Buffer = Buffer.alloc(0);
  private capabilities: DeviceCapabilities | null = null;

  constructor(
    private readonly port: number = DEVICE_DEFAULT_PORT,
    private readonly host: string = DEVICE_DEFAULT_HOST,
  ) {}

  async connect(): Promise<void> {
    return new Promise((resolve, reject) => {
      this.socket = new net.Socket();

      this.socket.on('error', (error: Error) => {
        reject(new Error(`Device connection error: ${error.message}`));
      });

      this.socket.connect(this.port, this.host, () => {
        resolve();
      });
    });
  }

  async authenticate(token: string): Promise<AuthLoginResult> {
    assert(token.length > 0, 'token must not be empty');

    const result = await this.sendRequest<AuthLoginResult>('auth.login', {
      token,
      cliVersion: VERSION,
    });

    assert(result !== null && result !== undefined, 'auth.login response must not be null');
    this.capabilities = result.capabilities ?? {};
    return result;
  }

  async sendRequest<T>(method: string, params?: Record<string, unknown>): Promise<T> {
    if (!this.socket) {
      throw new Error('Not connected to Device Agent');
    }

    const request: JsonRpcRequest = {
      jsonrpc: JSONRPC_VERSION,
      method,
      params: params ?? {},
      id: ++this.requestId,
    };

    const requestJson: string = JSON.stringify(request);
    const framedMessage: string = createFrame(requestJson);

    return new Promise((resolve, reject) => {
      const socket = this.socket!;

      const cleanup = (): void => {
        clearTimeout(timeoutId);
        socket.off('data', onData);
        socket.off('error', onError);
        socket.off('close', onClose);
      };

      const timeoutId = setTimeout(() => {
        cleanup();
        reject(
          new Error(
            `Device request timed out after ${DEVICE_NETWORK_TIMEOUT_MS}ms. Device Agent may be unresponsive.`,
          ),
        );
      }, DEVICE_NETWORK_TIMEOUT_MS);

      const onData = (chunk: Buffer): void => {
        this.receiveBuffer = Buffer.concat([this.receiveBuffer, chunk]);

        const parseResult = parseFrameFromBuffer(this.receiveBuffer);
        if (!parseResult.isComplete) {
          return;
        }

        const extractResult = extractFrameFromBuffer(
          this.receiveBuffer,
          parseResult.contentLength,
          parseResult.headerLength,
        );

        if (extractResult.jsonContent === null) {
          return;
        }

        cleanup();
        this.receiveBuffer = extractResult.remainingData;

        const response = JSON.parse(extractResult.jsonContent) as JsonRpcResponse;

        if (response.error) {
          const errorMessage: string = buildDeviceErrorMessage(response.error);
          reject(new Error(errorMessage));
          return;
        }

        resolve(response.result as T);
      };

      const onError = (error: Error): void => {
        cleanup();
        reject(new Error(`Device connection lost: ${error.message}`));
      };

      const onClose = (): void => {
        cleanup();
        reject(new Error('DEVICE_NO_RESPONSE'));
      };

      socket.on('data', onData);
      socket.on('error', onError);
      socket.on('close', onClose);
      socket.write(framedMessage);
    });
  }

  disconnect(): void {
    if (this.socket) {
      this.socket.destroy();
      this.socket = null;
    }
    this.receiveBuffer = Buffer.alloc(0);
  }

  isConnected(): boolean {
    return this.socket !== null && !this.socket.destroyed;
  }

  getCapabilities(): DeviceCapabilities | null {
    return this.capabilities;
  }
}

function buildDeviceErrorMessage(error: { code: number; message: string; data?: unknown }): string {
  if (error.code === AUTH_ERROR_CODE) {
    return `Device auth failed: ${error.message}`;
  }
  if (error.code === INCOMPATIBLE_VERSION_ERROR_CODE) {
    return `Incompatible version: ${error.message}`;
  }
  return `Device Agent error: ${error.message}`;
}
