import * as net from 'net';
import {
  UNITY_CONNECTION,
  JSONRPC,
  TIMEOUTS,
  ERROR_MESSAGES,
  DEFAULT_CLIENT_NAME,
  ServerShutdownReason,
} from './constants.js';
// Debug logging removed
import { SafeTimer, safeSetTimeout, stopSafeTimer } from './utils/safe-timer.js';

const createSafeTimeout = (callback: () => void, delay: number): SafeTimer => {
  return safeSetTimeout(callback, delay);
};
import { ConnectionManager } from './connection-manager.js';
import { MessageHandler } from './message-handler.js';
import { VibeLogger } from './utils/vibe-logger.js';

/**
 * Unity client interface for external dependencies
 */
interface UnityDiscovery {
  handleConnectionLost(): void;
}

/**
 * TCP/IP client for communication with Unity
 * Implemented as Singleton to prevent multiple Unity connections
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
  private static readonly MAX_COUNTER = 9999;
  private static readonly COUNTER_PADDING = 4;
  private static instance: UnityClient | null = null;

  private socket: net.Socket | null = null;
  private _connected: boolean = false;
  private port: number;
  private readonly host: string = UNITY_CONNECTION.DEFAULT_HOST;
  private reconnectHandlers: Set<() => void> = new Set();
  private connectionManager: ConnectionManager = new ConnectionManager();
  private messageHandler: MessageHandler = new MessageHandler();
  private unityDiscovery: UnityDiscovery | null = null; // Reference to UnityDiscovery for connection loss handling
  private requestIdCounter: number = 0; // Will be incremented to 1 on first use
  private readonly processId: number = process.pid;
  private readonly randomSeed: number = Math.floor(Math.random() * 1000);
  private storedClientName: string | null = null;
  private isConnecting: boolean = false;
  private connectingPromise: Promise<void> | null = null;
  private hasEverConnected: boolean = false;
  private shutdownReason: ServerShutdownReason | null = null;

  private constructor() {
    const unityTcpPort: string | undefined = process.env.UNITY_TCP_PORT;

    if (!unityTcpPort) {
      throw new Error('UNITY_TCP_PORT environment variable is required but not set');
    }

    const parsedPort: number = parseInt(unityTcpPort, 10);
    if (isNaN(parsedPort) || parsedPort <= 0 || parsedPort > 65535) {
      throw new Error(`UNITY_TCP_PORT must be a valid port number (1-65535), got: ${unityTcpPort}`);
    }

    this.port = parsedPort;
  }

  /**
   * Get the singleton instance of UnityClient
   */
  static getInstance(): UnityClient {
    if (!UnityClient.instance) {
      UnityClient.instance = new UnityClient();
    }
    return UnityClient.instance;
  }

  /**
   * Reset the singleton instance (for testing purposes)
   */
  static resetInstance(): void {
    if (UnityClient.instance) {
      UnityClient.instance.disconnect();
      UnityClient.instance.storedClientName = null;
      UnityClient.instance = null;
    }
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
   * Set shutdown reason received from Unity server
   * @param reason The reason for server shutdown
   */
  setShutdownReason(reason: ServerShutdownReason): void {
    this.shutdownReason = reason;
  }

  /**
   * Get whether the client has ever connected successfully
   */
  getHasEverConnected(): boolean {
    return this.hasEverConnected;
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
    let timeoutTimer: SafeTimer | null = null;

    try {
      await Promise.race([
        this.ping(UNITY_CONNECTION.CONNECTION_TEST_MESSAGE),
        new Promise<never>((_resolve, reject: (reason?: unknown) => void) => {
          timeoutTimer = createSafeTimeout(() => {
            reject(new Error('Health check timeout'));
          }, 1000);
        }),
      ]);
    } catch {
      return false;
    } finally {
      stopSafeTimer(timeoutTimer);
      timeoutTimer = null;
    }

    return true;
  }

  /**
   * Ensure connection to Unity (singleton-safe reconnection)
   * Properly manages single connection instance
   */
  async ensureConnected(): Promise<void> {
    // If already connected and healthy, return immediately
    if (this._connected && this.socket && !this.socket.destroyed) {
      try {
        if (await this.testConnection()) {
          return;
        }
      } catch {
        // Health check failed - need to reconnect
      }
    }

    // Deduplicate concurrent connect attempts
    if (this.connectingPromise) {
      await this.connectingPromise;
      return;
    }

    // Start a single connect attempt
    this.connectingPromise = (async (): Promise<void> => {
      this.isConnecting = true;
      // Disconnect any existing connection before creating new one
      this.disconnect();
      await this.connect();
    })()
      .catch((error) => {
        VibeLogger.logError('unity_connect_failed', 'Unity connect attempt failed', {
          message: error instanceof Error ? error.message : String(error),
        });
        throw error;
      })
      .finally(() => {
        this.isConnecting = false;
        this.connectingPromise = null;
      });

    await this.connectingPromise;
  }

  /**
   * Connect to Unity
   * Creates a new socket connection (should only be called after disconnect)
   */
  async connect(): Promise<void> {
    // Ensure we don't create multiple connections
    if (this._connected && this.socket && !this.socket.destroyed) {
      return; // Already connected
    }

    return new Promise((resolve, reject) => {
      this.socket = new net.Socket();
      const currentSocket: net.Socket = this.socket;
      let connectionEstablished: boolean = false;
      let promiseSettled: boolean = false;

      const finalizeInitialFailure = (error: Error, logCode: string, logMessage: string): void => {
        if (promiseSettled) {
          return;
        }
        promiseSettled = true;
        VibeLogger.logError(logCode, logMessage, { message: error.message });
        if (!currentSocket.destroyed) {
          currentSocket.destroy();
        }
        if (this.socket === currentSocket) {
          this.socket = null;
        }
        reject(error);
      };

      currentSocket.connect(this.port, this.host, () => {
        this._connected = true;
        this.hasEverConnected = true;
        this.shutdownReason = null; // Reset shutdown reason on successful connection
        connectionEstablished = true;
        promiseSettled = true;

        this.reconnectHandlers.forEach((handler) => {
          try {
            handler();
          } catch (error) {
            VibeLogger.logError(
              'unity_reconnect_handler_error',
              'Unity reconnect handler threw an error',
              {
                message: error instanceof Error ? error.message : String(error),
                stack: error instanceof Error ? error.stack : undefined,
              },
            );
          }
        });

        resolve();
      });

      currentSocket.on('error', (error: Error) => {
        this._connected = false;
        if (!connectionEstablished) {
          finalizeInitialFailure(
            new Error(`Unity connection failed: ${error.message}`),
            'unity_connect_attempt_failed',
            'Unity socket error during connection attempt',
          );
          return;
        }

        VibeLogger.logError('unity_socket_error', 'Unity socket error', {
          message: error.message,
        });
        this.handleConnectionLoss();
      });

      currentSocket.on('close', () => {
        this._connected = false;
        if (!connectionEstablished) {
          finalizeInitialFailure(
            new Error('Unity connection closed before being established'),
            'unity_connect_closed_pre_handshake',
            'Unity socket closed during connection attempt',
          );
          return;
        }
        this.handleConnectionLoss();
      });

      currentSocket.on('end', () => {
        this._connected = false;
        if (!connectionEstablished) {
          finalizeInitialFailure(
            new Error('Unity connection ended before being established'),
            'unity_connect_end_pre_handshake',
            'Unity socket ended during connection attempt',
          );
          return;
        }
        this.handleConnectionLoss();
      });

      currentSocket.on('data', (data: Buffer) => {
        this.messageHandler.handleIncomingData(data);
      });
    });
  }

  /**
   * Detect client name from stored value, environment variables, or default
   */
  private detectClientName(): string {
    if (this.storedClientName) {
      return this.storedClientName;
    }
    return process.env.MCP_CLIENT_NAME || DEFAULT_CLIENT_NAME;
  }

  /**
   * Send client name to Unity for identification
   */
  async setClientName(clientName?: string): Promise<void> {
    if (!this.connected) {
      return; // Skip if not connected
    }

    // Store client name if explicitly provided
    if (clientName) {
      this.storedClientName = clientName;
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
    } catch {
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
    // Handle disconnected state with appropriate message
    if (!this.connected) {
      // Server never started → error
      if (!this.hasEverConnected) {
        throw new Error(this.getServerNotRunningMessage());
      }
      // Editor quit (shutdown notification received with EditorQuit reason) → error
      if (this.shutdownReason === ServerShutdownReason.EDITOR_QUIT) {
        throw new Error(this.getServerNotRunningMessage());
      }
      // Domain reload or compilation in progress → guidance message as success response
      return this.getOsSpecificReconnectMessage();
    }

    // Ensure client name is set (this completes the connection handshake)
    await this.setClientName();

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

      return this.handleToolResponse(response);
    } catch (error) {
      // Log timeout details to file for debugging in production
      if (error instanceof Error && error.message.includes('timed out')) {
        // Timeout occurred
      }

      throw error;
    }
  }

  /**
   * Build a guidance message for temporary disconnection after compile.
   */
  private getOsSpecificReconnectMessage(): string {
    const baseMessage: string =
      'Waiting for Unity to be ready (normal during compilation). Wait 3 seconds then retry. If still not ready after several attempts, increase wait time (5 → 10 seconds). Report as error only after 1+ minute of failures.';

    const platform: string = process.platform;

    if (platform === 'win32') {
      return `${baseMessage} Example: Start-Sleep -Seconds 3`;
    }

    return `${baseMessage} Example: sleep 3`;
  }

  /**
   * Build an error message for when Unity server is not running.
   */
  private getServerNotRunningMessage(): string {
    return 'Unity server is not running. Please start Unity Editor and ensure uLoopMCP package is properly installed. If Unity is already running, check Window > uLoopMCP > Server Status.';
  }

  private handleToolResponse(response: { error?: { message: string }; result?: unknown }): unknown {
    if (response.error) {
      throw new Error(response.error.message);
    }

    return response.result;
  }

  /**
   * Generate unique request ID as string
   * Uses timestamp + process ID + random seed + counter for guaranteed uniqueness across processes
   */
  private generateId(): string {
    if (this.requestIdCounter >= UnityClient.MAX_COUNTER) {
      this.requestIdCounter = 1;
    } else {
      this.requestIdCounter++;
    }

    // Format: "ts_[timestamp]_[processId]_[randomSeed]_[counter]"
    // Example: "ts_1752718618309_58009_123_0001"
    const timestamp = Date.now();
    const processId = this.processId;
    const randomSeed = this.randomSeed;
    const counter = this.requestIdCounter.toString().padStart(UnityClient.COUNTER_PADDING, '0');

    return `ts_${timestamp}_${processId}_${randomSeed}_${counter}`;
  }

  /**
   * Send request and wait for response
   */
  private async sendRequest(
    request: { id: string; method: string; [key: string]: unknown },
    timeoutMs?: number,
  ): Promise<{ id: string; error?: { message: string }; result?: unknown }> {
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
          stopSafeTimer(timeoutTimer); // Clean up timer
          resolve(response as { id: string; error?: { message: string }; result?: unknown });
        },
        (error) => {
          stopSafeTimer(timeoutTimer); // Clean up timer
          reject(error instanceof Error ? error : new Error(String(error)));
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

    // Reset request ID counter to prevent ID conflicts on reconnection
    this.requestIdCounter = 0;

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

  /**
   * Fetch tool details from Unity with development mode support
   */
  async fetchToolDetailsFromUnity(
    includeDevelopmentOnly: boolean = false,
  ): Promise<unknown[] | null> {
    // Get detailed tool information including schemas
    // Include development-only tools if in development mode
    const params = { IncludeDevelopmentOnly: includeDevelopmentOnly };

    // Requesting tool details from Unity with params
    const toolDetailsResponse = await this.executeTool('get-tool-details', params);
    // Received tool details response

    // Handle new GetToolDetailsResponse structure
    const toolDetails =
      (toolDetailsResponse as { Tools?: unknown[] })?.Tools || toolDetailsResponse;
    if (!Array.isArray(toolDetails)) {
      // Invalid tool details response
      return null;
    }

    // Successfully parsed tools from Unity
    return toolDetails as unknown[];
  }

  /**
   * Check if Unity is available on specific port
   * Performs low-level TCP connection test with short timeout
   */
  static async isUnityAvailable(port: number): Promise<boolean> {
    return new Promise((resolve) => {
      const socket = new net.Socket();
      const timeout = 500; // Shorter timeout for faster discovery

      const timer = setTimeout(() => {
        socket.destroy();
        resolve(false);
      }, timeout);

      socket.connect(port, UNITY_CONNECTION.DEFAULT_HOST, () => {
        clearTimeout(timer);
        socket.destroy();
        resolve(true);
      });

      socket.on('error', () => {
        clearTimeout(timer);
        resolve(false);
      });
    });
  }
}
