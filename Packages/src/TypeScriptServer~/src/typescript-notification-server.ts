import * as net from 'net';
import { VibeLogger } from './utils/vibe-logger.js';

/**
 * Unity Notification data structure
 */
interface UnityNotification {
  type: 'server_started' | 'server_stopped';
  reason?: string;
  server_port?: number;
  timestamp?: string;
}

/**
 * TypeScript Notification Server
 * Receives push notifications from Unity for server lifecycle events
 * Enables complete elimination of polling-based discovery
 *
 * Design document reference: working-notes/2025-07-21_SOW_TypeScript_Notification_Server.md
 *
 * Related classes:
 * - UnityConnectionManager: Uses this server for event-driven connection management
 * - UnityClient: Connects to Unity as TCP client for MCP Tools
 * - McpBridgeServer (Unity): Sends notifications to this server
 */
export class TypeScriptNotificationServer {
  private server: net.Server | null = null;
  private port: number = 0;
  private isRunning: boolean = false;
  private correlationId: string;

  // Event callbacks
  private onServerStartedCallback?: () => void;
  private onServerStoppedCallback?: (reason: string) => void;
  private onConnectionEstablishedCallback?: () => void;

  constructor() {
    this.correlationId = VibeLogger.generateCorrelationId();
    VibeLogger.logInfo(
      'notification_server_init',
      'Initializing TypeScript Notification Server',
      {},
      this.correlationId,
      'Setting up TCP server for Unity push notifications',
    );
  }

  /**
   * Find an available random port in the specified range
   */
  private async findAvailablePort(
    startPort: number = 3000,
    endPort: number = 9000,
  ): Promise<number> {
    const correlationId = VibeLogger.generateCorrelationId();

    for (let attempt = 0; attempt < 50; attempt++) {
      const port = Math.floor(Math.random() * (endPort - startPort + 1)) + startPort;

      if (await this.isPortAvailable(port)) {
        VibeLogger.logInfo(
          'notification_server_port_found',
          'Found available port for notification server',
          { port, attempt },
          correlationId,
          'Random port selection successful',
        );
        return port;
      }
    }

    throw new Error('Could not find available port for TypeScript Notification Server');
  }

  /**
   * Check if a port is available
   */
  private async isPortAvailable(port: number): Promise<boolean> {
    return new Promise((resolve) => {
      const testServer = net.createServer();

      testServer.listen(port, () => {
        testServer.close(() => {
          resolve(true);
        });
      });

      testServer.on('error', () => {
        resolve(false);
      });
    });
  }

  /**
   * Start the notification server
   */
  async start(): Promise<void> {
    if (this.isRunning) {
      return;
    }

    try {
      // Find available port
      this.port = await this.findAvailablePort();

      // Create TCP server
      this.server = net.createServer((socket) => {
        this.handleUnityConnection(socket);
      });

      // Start listening
      await new Promise<void>((resolve, reject) => {
        this.server!.listen(this.port, () => {
          this.isRunning = true;

          VibeLogger.logInfo(
            'notification_server_started',
            'TypeScript Notification Server started successfully',
            {
              port: this.port,
              listening: true,
            },
            this.correlationId,
            'Server ready to receive Unity notifications',
          );

          resolve();
        });

        this.server!.on('error', (error) => {
          VibeLogger.logError(
            'notification_server_start_error',
            'Failed to start TypeScript Notification Server',
            {
              port: this.port,
              error_message: error.message,
            },
            this.correlationId,
            'Server startup failed',
          );
          reject(error);
        });
      });
    } catch (error) {
      VibeLogger.logError(
        'notification_server_start_exception',
        'Exception during notification server startup',
        {
          error_message: error instanceof Error ? error.message : String(error),
        },
        this.correlationId,
      );
      throw error;
    }
  }

  /**
   * Handle incoming Unity connection
   */
  private handleUnityConnection(socket: net.Socket): void {
    const connectionId = VibeLogger.generateCorrelationId();
    const remoteAddress = `${socket.remoteAddress}:${socket.remotePort}`;

    VibeLogger.logInfo(
      'notification_server_unity_connected',
      'Unity connected to notification server',
      {
        remote_address: remoteAddress,
        local_port: this.port,
      },
      connectionId,
      'Unity established reverse connection for notifications',
    );

    // Trigger connection established callback
    if (this.onConnectionEstablishedCallback) {
      this.onConnectionEstablishedCallback();
    }

    // Handle incoming data
    socket.on('data', (data) => {
      this.handleUnityNotification(data, connectionId);
    });

    // Handle connection close
    socket.on('close', () => {
      VibeLogger.logInfo(
        'notification_server_unity_disconnected',
        'Unity disconnected from notification server',
        {
          remote_address: remoteAddress,
        },
        connectionId,
        'Unity closed notification connection',
      );
    });

    // Handle errors
    socket.on('error', (error) => {
      VibeLogger.logError(
        'notification_server_connection_error',
        'Error in Unity notification connection',
        {
          remote_address: remoteAddress,
          error_message: error.message,
        },
        connectionId,
      );
    });
  }

  /**
   * Handle incoming notification from Unity
   */
  private handleUnityNotification(data: Buffer, connectionId: string): void {
    try {
      const notificationText = data.toString('utf8');
      const notification = JSON.parse(notificationText) as UnityNotification;

      VibeLogger.logInfo(
        'notification_server_received',
        'Received notification from Unity',
        {
          notification_type: notification.type,
          reason: notification.reason,
          data_size: data.length,
        },
        connectionId,
        'Processing Unity notification',
      );

      // Route notification to appropriate handler
      switch (notification.type) {
        case 'server_started':
          this.handleServerStartedNotification(notification, connectionId);
          break;
        case 'server_stopped':
          this.handleServerStoppedNotification(notification, connectionId);
          break;
        default:
          VibeLogger.logWarning(
            'notification_server_unknown_type',
            'Received unknown notification type from Unity',
            {
              notification_type: notification.type,
              full_notification: notification,
            },
            connectionId,
            'Unknown notification type - may need to update handler',
          );
      }
    } catch (error) {
      VibeLogger.logError(
        'notification_server_parse_error',
        'Failed to parse Unity notification',
        {
          raw_data: data.toString('utf8'),
          error_message: error instanceof Error ? error.message : String(error),
        },
        connectionId,
      );
    }
  }

  /**
   * Handle server_started notification
   */
  private handleServerStartedNotification(
    notification: UnityNotification,
    connectionId: string,
  ): void {
    VibeLogger.logInfo(
      'notification_server_started_received',
      'Unity server started notification received',
      {
        reason: notification.reason,
        server_port: notification.server_port,
        timestamp: notification.timestamp,
      },
      connectionId,
      'Unity MCP server is now available for connections',
    );

    // Trigger callback
    if (this.onServerStartedCallback) {
      this.onServerStartedCallback();
    }
  }

  /**
   * Handle server_stopped notification
   */
  private handleServerStoppedNotification(
    notification: UnityNotification,
    connectionId: string,
  ): void {
    const reason = notification.reason || 'unknown';

    VibeLogger.logInfo(
      'notification_server_stopped_received',
      'Unity server stopped notification received',
      {
        reason: reason,
        timestamp: notification.timestamp,
      },
      connectionId,
      'Unity MCP server has been stopped',
    );

    // Trigger callback
    if (this.onServerStoppedCallback) {
      this.onServerStoppedCallback(reason);
    }
  }

  /**
   * Stop the notification server
   */
  async stop(): Promise<void> {
    if (!this.isRunning || !this.server) {
      return;
    }

    return new Promise<void>((resolve) => {
      this.server!.close(() => {
        this.isRunning = false;
        this.server = null;

        VibeLogger.logInfo(
          'notification_server_stopped',
          'TypeScript Notification Server stopped',
          {
            port: this.port,
          },
          this.correlationId,
          'Notification server shutdown complete',
        );

        resolve();
      });
    });
  }

  /**
   * Get the current port number
   */
  getPort(): number {
    return this.port;
  }

  /**
   * Check if server is running
   */
  isServerRunning(): boolean {
    return this.isRunning;
  }

  /**
   * Set callback for server started notifications
   */
  setOnServerStartedCallback(callback: () => void): void {
    this.onServerStartedCallback = callback;
  }

  /**
   * Set callback for server stopped notifications
   */
  setOnServerStoppedCallback(callback: (reason: string) => void): void {
    this.onServerStoppedCallback = callback;
  }

  /**
   * Backward compatibility aliases
   */
  onServerStarted(callback: () => void): void {
    this.setOnServerStartedCallback(callback);
  }

  onServerStopped(callback: (reason: string) => void): void {
    this.setOnServerStoppedCallback(callback);
  }

  /**
   * Set callback for connection established
   */
  onConnectionEstablished(callback: () => void): void {
    this.onConnectionEstablishedCallback = callback;
  }

  /**
   * Get debugging information
   */
  getDebugInfo(): object {
    return {
      port: this.port,
      isRunning: this.isRunning,
      hasServer: this.server !== null,
      correlationId: this.correlationId,
    };
  }
}
