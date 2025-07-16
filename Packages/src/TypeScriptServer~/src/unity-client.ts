import * as net from 'net';
import { UNITY_CONNECTION, JSONRPC, TIMEOUTS, ERROR_MESSAGES } from './constants.js';
// Debug logging removed
import { safeSetTimeout } from './utils/safe-timer.js';
import { ConnectionManager } from './connection-manager.js';
import { MessageHandler } from './message-handler.js';

/**
 * Unity client interface for external dependencies
 */
interface UnityDiscovery {
  handleConnectionLost(): void;
}

/**
 * TCP/IP client for communication with Unity
 *
 * Design document reference: Packages/src/TypeScriptServer~/ARCHITECTURE.md
 *
 * Related classes:
 * - UnityMcpServer: The main server class that uses this client
 * - DynamicUnityCommandTool: Uses this client to execute tools in Unity
 * - ConnectionManager: Handles connection state and reconnection polling
 * - MessageHandler: Handles JSON-RPC message processing
 */
export class UnityClient {
  private socket: net.Socket | null = null;
  private _connected: boolean = false;
  private port: number;
  private readonly host: string = UNITY_CONNECTION.DEFAULT_HOST;
  private reconnectHandlers: Set<() => void> = new Set();
  private connectionManager: ConnectionManager = new ConnectionManager();
  private messageHandler: MessageHandler = new MessageHandler();
  private unityDiscovery: UnityDiscovery | null = null; // Reference to UnityDiscovery for connection loss handling

  constructor() {
    // Get port number from environment variable UNITY_TCP_PORT, default is 7400
    this.port = parseInt(process.env.UNITY_TCP_PORT || UNITY_CONNECTION.DEFAULT_PORT, 10);
  }

  /**
   * Update Unity connection port (for discovery)
   */
  updatePort(newPort: number): void {
    this.port = newPort;
  }

  /**
   * Set Unity Discovery reference for connection loss handling
   */
  setUnityDiscovery(unityDiscovery: UnityDiscovery): void {
    this.unityDiscovery = unityDiscovery;
  }

  get connected(): boolean {
    return this._connected && this.socket !== null && !this.socket.destroyed;
  }

  /**
   * Register notification handler for specific method
   */
  onNotification(method: string, handler: (params: unknown) => void): void {
    this.messageHandler.onNotification(method, handler);
  }

  /**
   * Remove notification handler
   */
  offNotification(method: string): void {
    this.messageHandler.offNotification(method);
  }

  /**
   * Register reconnect handler
   */
  onReconnect(handler: () => void): void {
    this.reconnectHandlers.add(handler);
  }

  /**
   * Remove reconnect handler
   */
  offReconnect(handler: () => void): void {
    this.reconnectHandlers.delete(handler);
  }

  /**
   * Lightweight connection health check
   * Tests socket state without creating new connections
   */
  async testConnection(): Promise<boolean> {
    // First check: basic connection state (lightweight)
    if (!this._connected || this.socket === null || this.socket.destroyed) {
      return false;
    }

    // Second check: socket readability/writability (lightweight)
    if (!this.socket.readable || !this.socket.writable) {
      this._connected = false;
      return false;
    }

    // Third check: ping test with timeout (only if socket state is good)
    try {
      // Add timeout to prevent hanging
      await Promise.race([
        this.ping(UNITY_CONNECTION.CONNECTION_TEST_MESSAGE),
        new Promise((_, reject) =>
          setTimeout(() => reject(new Error('Health check timeout')), 1000),
        ),
      ]);
      return true;
    } catch (error) {
      // Ping failed or timed out, but don't disconnect - just report unhealthy
      // This prevents creating new connections during health checks
      return false;
    }
  }

  /**
   * Connect to Unity (reconnect if necessary)
   * Now more conservative about creating new connections
   */
  async ensureConnected(): Promise<void> {
    // First: quick socket state check
    if (
      this._connected &&
      this.socket &&
      !this.socket.destroyed &&
      this.socket.readable &&
      this.socket.writable
    ) {
      // Socket looks healthy, do a lightweight ping test
      try {
        if (await this.testConnection()) {
          return; // Connection is healthy
        }
      } catch (error) {
        // Ping failed, but don't immediately reconnect
        // Give it another chance before creating new connection
      }
    }

    // Second chance: basic socket state check only
    if (this._connected && this.socket && !this.socket.destroyed) {
      // Socket exists but might be temporarily unresponsive
      // Don't create new connection immediately
      return;
    }

    // Only reconnect if socket is actually destroyed/null
    this.disconnect();
    await this.connect();
  }

  /**
   * Connect to Unity
   */
  async connect(): Promise<void> {
    return new Promise((resolve, reject) => {
      this.socket = new net.Socket();

      this.socket.connect(this.port, this.host, () => {
        this._connected = true;

        // Notify reconnect handlers (this will handle client name setting)
        this.reconnectHandlers.forEach((handler) => {
          try {
            handler();
          } catch (error) {
            // Error in reconnect handler
          }
        });

        resolve();
      });

      // Handle errors (both during connection and after establishment)
      this.socket.on('error', (error) => {
        this._connected = false;
        if (this.socket?.connecting) {
          // Error during connection attempt
          reject(new Error(`Unity connection failed: ${error.message}`));
        } else {
          // Error after connection was established
          this.handleConnectionLoss();
        }
      });

      this.socket.on('close', () => {
        this._connected = false;
        this.handleConnectionLoss();
      });

      // Handle graceful end of connection
      this.socket.on('end', () => {
        this._connected = false;
        this.handleConnectionLoss();
      });

      // Handle incoming data (both notifications and responses)
      this.socket.on('data', (data) => {
        // Handle incoming data as Buffer for proper Content-Length framing
        this.messageHandler.handleIncomingData(data);
      });
    });
  }

  /**
   * Detect client name from environment variables
   */
  private detectClientName(): string {
    return process.env.MCP_CLIENT_NAME || 'MCP Client';
  }

  /**
   * Send client name to Unity for identification
   */
  async setClientName(clientName?: string): Promise<void> {
    if (!this.connected) {
      return; // Skip if not connected
    }

    // Use provided client name or fallback to environment detection
    const finalClientName = clientName || this.detectClientName();

    const request = {
      jsonrpc: JSONRPC.VERSION,
      id: this.generateId(),
      method: 'set-client-name',
      params: {
        ClientName: finalClientName,
      },
    };

    try {
      const response = await this.sendRequest(request);

      if (response.error) {
        // Failed to set client name
      }
    } catch (error) {
      // Error setting client name
    }
  }

  /**
   * Send ping to Unity
   */
  async ping(message: string): Promise<unknown> {
    if (!this.connected) {
      throw new Error('Not connected to Unity');
    }

    const request = {
      jsonrpc: JSONRPC.VERSION,
      id: this.generateId(),
      method: 'ping',
      params: {
        Message: message, // Updated to match PingSchema property name
      },
    };

    const response = await this.sendRequest(request, 1000); // 1秒でタイムアウト

    if (response.error) {
      throw new Error(`Unity error: ${response.error.message}`);
    }

    // Return the full response object (now includes timing information)
    return response.result || { Message: 'Unity pong' };
  }

  /**
   * Get available tools from Unity
   */
  async getAvailableTools(): Promise<string[]> {
    await this.ensureConnected();

    const request = {
      jsonrpc: JSONRPC.VERSION,
      id: this.generateId(),
      method: 'getAvailableTools',
      params: {},
    };

    const response = await this.sendRequest(request);

    if (response.error) {
      throw new Error(`Failed to get available tools: ${response.error.message}`);
    }

    return (response.result as string[]) || [];
  }

  /**
   * Get tool details from Unity
   */
  async getToolDetails(
    includeDevelopmentOnly: boolean = false,
  ): Promise<Array<{ name: string; description: string; parameterSchema?: unknown }>> {
    await this.ensureConnected();

    const request = {
      jsonrpc: JSONRPC.VERSION,
      id: this.generateId(),
      method: 'get-tool-details',
      params: { IncludeDevelopmentOnly: includeDevelopmentOnly },
    };

    const response = await this.sendRequest(request);

    if (response.error) {
      throw new Error(`Failed to get tool details: ${response.error.message}`);
    }

    return (
      (response.result as Array<{
        name: string;
        description: string;
        parameterSchema?: unknown;
      }>) || []
    );
  }

  /**
   * Execute any Unity tool dynamically
   */
  async executeTool(toolName: string, params: Record<string, unknown> = {}): Promise<unknown> {
    const request = {
      jsonrpc: JSONRPC.VERSION,
      id: this.generateId(),
      method: toolName,
      params: params,
    };

    // Executing tool with params

    // Unity handles timeout control, so TS uses longer network timeout
    const timeoutMs = TIMEOUTS.NETWORK;

    try {
      const response = await this.sendRequest(request, timeoutMs);

      return this.handleToolResponse(response, toolName);
    } catch (error) {
      // Log timeout details to file for debugging in production
      if (error instanceof Error && error.message.includes('timed out')) {
        // Timeout occurred
      }

      throw error;
    }
  }

  private handleToolResponse(
    response: { error?: { message: string }; result?: unknown },
    toolName: string,
  ): unknown {
    if (response.error) {
      throw new Error(`Failed to execute tool '${toolName}': ${response.error.message}`);
    }

    return response.result;
  }

  /**
   * Generate unique request ID
   */
  private generateId(): number {
    return Date.now();
  }

  /**
   * Send request and wait for response
   */
  private async sendRequest(
    request: { id: number; method: string; [key: string]: unknown },
    timeoutMs?: number,
  ): Promise<{ id: number; error?: { message: string }; result?: unknown }> {
    return new Promise((resolve, reject) => {
      // Use provided timeout or default to NETWORK timeout
      const timeout_duration = timeoutMs || TIMEOUTS.NETWORK;

      // Use SafeTimer for automatic cleanup to prevent orphaned processes
      const timeoutTimer = safeSetTimeout(() => {
        // Network timeout occurred
        this.messageHandler.clearPendingRequests(`Request ${ERROR_MESSAGES.TIMEOUT}`);
        reject(new Error(`Request ${ERROR_MESSAGES.TIMEOUT}`));
      }, timeout_duration);

      // Register the pending request
      this.messageHandler.registerPendingRequest(
        request.id,
        (response) => {
          timeoutTimer.stop(); // Clean up timer
          resolve(response as { id: number; error?: { message: string }; result?: unknown });
        },
        (error) => {
          timeoutTimer.stop(); // Clean up timer
          reject(error);
        },
      );

      // Send the request
      const requestStr = this.messageHandler.createRequest(
        request.method,
        request.params as Record<string, unknown>,
        request.id,
      );
      if (this.socket) {
        this.socket.write(requestStr);
      }
    });
  }

  /**
   * Disconnect
   *
   * IMPORTANT: Always clean up timers when disconnecting!
   * Failure to properly clean up timers can cause orphaned processes
   * that prevent Node.js from exiting gracefully.
   */
  disconnect(): void {
    this.connectionManager.stopPolling(); // Stop polling when manually disconnecting

    // Clean up all pending requests and their timers
    // CRITICAL: This prevents orphaned processes by ensuring all setTimeout timers are cleared
    this.messageHandler.clearPendingRequests('Connection closed');

    // Clear the Content-Length framing buffer
    this.messageHandler.clearBuffer();

    if (this.socket) {
      this.socket.destroy();
      this.socket = null;
    }
    this._connected = false;
  }

  /**
   * Handle connection loss by delegating to UnityDiscovery
   */
  private handleConnectionLoss(): void {
    // Trigger ConnectionManager callback for backward compatibility
    this.connectionManager.triggerConnectionLost();

    // Delegate to UnityDiscovery for unified connection management
    if (this.unityDiscovery) {
      this.unityDiscovery.handleConnectionLost();
    }
  }

  /**
   * Set callback for when connection is restored
   */
  setReconnectedCallback(callback: () => void): void {
    this.connectionManager.setReconnectedCallback(callback);
  }
}
