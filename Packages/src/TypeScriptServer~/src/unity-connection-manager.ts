import { UnityClient } from './unity-client.js';
import { VibeLogger } from './utils/vibe-logger.js';

/**
 * Event-driven Unity Connection Manager
 * Replaces polling-based discovery with push notification system
 *
 * Design document reference: working-notes/2025-07-21_SOW_TypeScript_Notification_Server.md
 *
 * Related classes:
 * - UnityClient: TCP client for MCP Tools communication
 * - TypeScriptNotificationServer: Receives push notifications from Unity
 * - UnityDiscovery: Legacy polling-based discovery (DEPRECATED)
 *
 * Key improvements over legacy implementation:
 * - Complete elimination of polling timers
 * - Event-driven connection management
 * - Zero CPU usage when Unity is stopped
 * - Instant connection on Unity server start
 */
export class UnityConnectionManager {
  private unityClient: UnityClient;
  private isConnected: boolean = false;
  private onConnectedCallback: (() => Promise<void>) | null = null;
  private onDisconnectedCallback: (() => void) | null = null;
  private correlationId: string;
  private isInitialized: boolean = false;

  constructor(unityClient: UnityClient) {
    this.unityClient = unityClient;
    this.correlationId = VibeLogger.generateCorrelationId();

    VibeLogger.logInfo(
      'connection_manager_init',
      'Unity Connection Manager initialized with event-driven architecture',
      {
        polling_eliminated: true,
        event_driven: true,
      },
      this.correlationId,
      'Replaced polling-based discovery with push notification system',
    );
  }

  /**
   * Initialize event-driven connection manager with TypeScript notification server callbacks
   */
  initialize(onConnectionEstablished?: () => Promise<void>): void {
    if (this.isInitialized) {
      return;
    }

    this.isInitialized = true;
    this.onConnectedCallback = onConnectionEstablished || null;

    // Setup notification server callbacks for event-driven connection management
    this.setupNotificationServerCallbacks();

    VibeLogger.logInfo(
      'connection_manager_initialized',
      'Event-driven Unity Connection Manager initialized',
      {
        polling_eliminated: true,
        push_notifications: true,
        zero_cpu_when_stopped: true,
      },
      this.correlationId,
      'Manager now uses push notifications instead of polling',
    );
  }

  /**
   * Setup TypeScript notification server callbacks for event-driven architecture
   */
  private setupNotificationServerCallbacks(): void {
    const notificationServer = this.unityClient.getNotificationServer();
    if (!notificationServer) {
      VibeLogger.logWarning(
        'notification_server_unavailable',
        'TypeScript notification server not available',
        {},
        this.correlationId,
        'Cannot setup event-driven callbacks - falling back to manual connection',
      );
      return;
    }

    // Set callback for Unity server started notifications
    notificationServer.setOnServerStartedCallback(() => {
      void this.handleUnityServerStarted();
    });

    // Set callback for Unity server stopped notifications
    notificationServer.setOnServerStoppedCallback((reason: string) => {
      this.handleUnityServerStopped(reason);
    });

    VibeLogger.logInfo(
      'notification_callbacks_setup',
      'Event-driven notification callbacks established',
      {
        server_start_callback: true,
        server_stop_callback: true,
      },
      this.correlationId,
      'Connection manager now responds to Unity server lifecycle events',
    );
  }

  /**
   * Wait for Unity connection with timeout (event-driven)
   * No polling - waits for server started notifications
   */
  async waitForUnityConnectionWithTimeout(timeoutMs: number): Promise<void> {
    return new Promise((resolve, reject) => {
      const timeout = setTimeout(() => {
        reject(new Error(`Unity connection timeout after ${timeoutMs}ms`));
      }, timeoutMs);

      // If already connected, resolve immediately
      if (this.unityClient.connected) {
        clearTimeout(timeout);
        resolve();
        return;
      }

      // Initialize connection manager if not already done
      if (!this.isInitialized) {
        this.initialize(() => {
          clearTimeout(timeout);
          return Promise.resolve();
        });
      }

      // Setup one-time callback for connection success
      const originalCallback = this.onConnectedCallback;
      this.onConnectedCallback = async (): Promise<void> => {
        clearTimeout(timeout);

        // Restore original callback
        this.onConnectedCallback = originalCallback;

        // Execute original callback if it exists
        if (originalCallback) {
          await originalCallback();
        }

        resolve();
      };

      VibeLogger.logInfo(
        'connection_wait_started',
        'Waiting for Unity server started notification',
        { timeout_ms: timeoutMs },
        this.correlationId,
        'Event-driven wait - no polling involved',
      );
    });
  }

  /**
   * Handle Unity server started notification (replaces discovery)
   * Called when TypeScript notification server receives server_started from Unity
   */
  private async handleUnityServerStarted(): Promise<void> {
    try {
      VibeLogger.logInfo(
        'unity_server_started_received',
        'Received Unity server started notification - establishing connection',
        {},
        this.correlationId,
        'Event-driven connection establishment triggered by push notification',
      );

      // Ensure connection to Unity
      await this.unityClient.ensureConnected();
      this.isConnected = true;

      // Execute connection established callback
      if (this.onConnectedCallback) {
        await this.onConnectedCallback();
      }

      VibeLogger.logInfo(
        'unity_connection_established',
        'Unity connection established via push notification',
        {
          connected: this.isConnected,
          method: 'push_notification',
        },
        this.correlationId,
        'Zero-latency connection establishment completed',
      );
    } catch (error) {
      VibeLogger.logError(
        'unity_connection_failed',
        'Failed to establish Unity connection after server started notification',
        {
          error_message: error instanceof Error ? error.message : String(error),
        },
        this.correlationId,
        'Connection establishment failed despite server started notification',
      );
    }
  }

  /**
   * Handle Unity server stopped notification
   * Called when TypeScript notification server receives server_stopped from Unity
   */
  private handleUnityServerStopped(reason: string): void {
    VibeLogger.logInfo(
      'unity_server_stopped_received',
      'Received Unity server stopped notification',
      {
        reason: reason,
        was_connected: this.isConnected,
      },
      this.correlationId,
      'Event-driven disconnection triggered by push notification',
    );

    this.isConnected = false;

    // Execute disconnection callback
    if (this.onDisconnectedCallback) {
      this.onDisconnectedCallback();
    }
  }

  /**
   * Set callback for when Unity connection is established
   */
  setOnConnectedCallback(callback: () => Promise<void>): void {
    this.onConnectedCallback = callback;
  }

  /**
   * Set callback for when Unity connection is lost
   */
  setOnDisconnectedCallback(callback: () => void): void {
    this.onDisconnectedCallback = callback;
  }

  /**
   * Setup reconnection callback (event-driven)
   * Uses push notifications instead of polling for reconnection
   */
  setupReconnectionCallback(callback: () => Promise<void>): void {
    this.unityClient.setReconnectedCallback(() => {
      VibeLogger.logInfo(
        'unity_reconnection_detected',
        'Unity reconnection detected - event-driven recovery',
        {},
        this.correlationId,
        'No polling required - push notifications will handle state updates',
      );

      void callback();
    });
  }

  /**
   * Check if Unity is connected
   */
  isUnityConnected(): boolean {
    return this.isConnected && this.unityClient.connected;
  }

  /**
   * Disconnect from Unity (event-driven cleanup)
   */
  disconnect(): void {
    VibeLogger.logInfo(
      'connection_manager_disconnect',
      'Disconnecting Unity connection manager',
      {
        was_connected: this.isConnected,
      },
      this.correlationId,
      'Clean disconnection - no timers to stop',
    );

    this.isConnected = false;
    this.unityClient.disconnect();
  }
}
