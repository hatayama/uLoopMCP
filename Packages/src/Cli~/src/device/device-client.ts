/**
 * Device Agent client.
 * Wraps DirectUnityClient with auth.login handshake for Device Agent connections.
 */

import { DirectUnityClient } from '../direct-unity-client.js';
import { VERSION } from '../version.js';
import { DEVICE_AGENT_HOST, DEVICE_AGENT_PORT } from './device-constants.js';

export interface AuthLoginResponse {
  protocolVersion: string;
  agentVersion: string;
  minCliVersion: string;
  capabilities: Record<string, unknown>;
}

export class DeviceClient {
  private readonly client: DirectUnityClient;

  constructor(port: number = DEVICE_AGENT_PORT) {
    this.client = new DirectUnityClient(port, DEVICE_AGENT_HOST);
  }

  async connect(token: string): Promise<AuthLoginResponse> {
    await this.client.connect();

    const response: AuthLoginResponse = await this.client.sendRequest<AuthLoginResponse>(
      'auth.login',
      {
        token,
        cliVersion: VERSION,
      },
    );

    return response;
  }

  async sendToolRequest<T>(toolName: string, params?: Record<string, unknown>): Promise<T> {
    return this.client.sendRequest<T>(toolName, params);
  }

  disconnect(): void {
    this.client.disconnect();
  }

  isConnected(): boolean {
    return this.client.isConnected();
  }
}

export function resolveDeviceToken(): string {
  const envToken: string | undefined = process.env['ULOOP_DEVICE_TOKEN'];
  if (envToken && envToken.length > 0) {
    return envToken;
  }

  throw new Error(
    'Device token not found. Set ULOOP_DEVICE_TOKEN environment variable or use --token option.',
  );
}
