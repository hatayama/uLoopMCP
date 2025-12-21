/**
 * Direct Unity TCP client for CLI usage.
 * Establishes one-shot TCP connections to Unity without going through MCP server.
 */

// Non-null assertions are used after TCP frame parsing where data existence is guaranteed by protocol
/* eslint-disable @typescript-eslint/no-non-null-assertion */

import * as net from 'net';
import { createFrame, parseFrameFromBuffer, extractFrameFromBuffer } from './simple-framer.js';

const JSONRPC_VERSION = '2.0';
const DEFAULT_HOST = '127.0.0.1';
const NETWORK_TIMEOUT_MS = 180000;

export interface JsonRpcRequest {
  jsonrpc: string;
  method: string;
  params?: Record<string, unknown>;
  id: number;
}

export interface JsonRpcResponse {
  jsonrpc: string;
  result?: unknown;
  error?: {
    code: number;
    message: string;
    data?: unknown;
  };
  id: number;
}

export class DirectUnityClient {
  private socket: net.Socket | null = null;
  private requestId: number = 0;
  private receiveBuffer: Buffer = Buffer.alloc(0);

  constructor(
    private readonly port: number,
    private readonly host: string = DEFAULT_HOST,
  ) {}

  async connect(): Promise<void> {
    return new Promise((resolve, reject) => {
      this.socket = new net.Socket();

      this.socket.on('error', (error: Error) => {
        reject(new Error(`Connection error: ${error.message}`));
      });

      this.socket.connect(this.port, this.host, () => {
        resolve();
      });
    });
  }

  async sendRequest<T>(method: string, params?: Record<string, unknown>): Promise<T> {
    if (!this.socket) {
      throw new Error('Not connected to Unity');
    }

    const request: JsonRpcRequest = {
      jsonrpc: JSONRPC_VERSION,
      method,
      params: params ?? {},
      id: ++this.requestId,
    };

    const requestJson = JSON.stringify(request);
    const framedMessage = createFrame(requestJson);

    return new Promise((resolve, reject) => {
      const socket = this.socket!;
      const timeoutId = setTimeout(() => {
        reject(new Error(`Request timed out after ${NETWORK_TIMEOUT_MS}ms`));
      }, NETWORK_TIMEOUT_MS);

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

        clearTimeout(timeoutId);
        socket.off('data', onData);

        this.receiveBuffer = extractResult.remainingData;

        const response = JSON.parse(extractResult.jsonContent) as JsonRpcResponse;

        if (response.error) {
          reject(new Error(`Unity error: ${response.error.message}`));
          return;
        }

        resolve(response.result as T);
      };

      socket.on('data', onData);
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
}
