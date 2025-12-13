import { UnityClient } from './unity-client.js';
import { UnityDiscovery } from './unity-discovery.js';
import { ENVIRONMENT, CONNECTION_RECOVERY } from './constants.js';
import { VibeLogger } from './utils/vibe-logger.js';

/**
 * Unity Connection Manager - Manages Unity connection and discovery functionality
 *
 * Design document reference: Packages/src/TypeScriptServer~/ARCHITECTURE.md
 *
 * Related classes:
 * - UnityClient: Manages TCP connection to Unity Editor
 * - UnityDiscovery: Handles Unity discovery and polling
 * - UnityMcpServer: Main server class that uses this manager
 *
 * Key features:
 * - Unity connection waiting and establishment
 * - Integration with discovery service
 * - Connection state monitoring
 * - Reconnection handling
 */
export class UnityConnectionManager {
  private unityClient: UnityClient;
  private unityDiscovery: UnityDiscovery;
  private readonly isDevelopment: boolean;
  private isInitialized: boolean = false;
  private isReconnecting: boolean = false;

  constructor(unityClient: UnityClient) {
    this.unityClient = unityClient;
    this.isDevelopment = process.env.NODE_ENV === ENVIRONMENT.NODE_ENV_DEVELOPMENT;

    // Initialize Unity discovery service (singleton pattern prevents duplicates)
    this.unityDiscovery = UnityDiscovery.getInstance(this.unityClient);

    // Set UnityDiscovery reference in UnityClient for unified connection management
    this.unityClient.setUnityDiscovery(this.unityDiscovery);
  }

  /**
   * Get Unity discovery instance
   */
  getUnityDiscovery(): UnityDiscovery {
    return this.unityDiscovery;
  }

  /**
   * Wait for Unity connection with timeout
   */
  async waitForUnityConnectionWithTimeout(timeoutMs: number): Promise<void> {
    return new Promise((resolve, reject) => {
      const timeout = setTimeout(() => {
        reject(new Error(`Unity connection timeout after ${timeoutMs}ms`));
      }, timeoutMs);

      const checkConnection = (): void => {
        if (this.unityClient.connected) {
          clearTimeout(timeout);
          resolve();
          return;
        }

        // If connection manager is already initialized, just wait for existing discovery
        if (this.isInitialized) {
          // Connection manager is already running, just wait for connection
          const connectionInterval = setInterval(() => {
            if (this.unityClient.connected) {
              clearTimeout(timeout);
              clearInterval(connectionInterval);
              resolve();
            }
          }, 100);
          return;
        }

        // Fallback: Initialize connection manager if not already done
        this.initialize(() => {
          return new Promise<void>((resolveCallback) => {
            clearTimeout(timeout);
            resolve();
            resolveCallback();
          });
        });
      };

      void checkConnection();
    });
  }

  /**
   * Handle Unity discovery and establish connection
   */
  async handleUnityDiscovered(onConnectionEstablished?: () => Promise<void>): Promise<void> {
    try {
      // Ensure UnityClient connection state is properly set
      // Even though discovery confirmed TCP connectivity, we need to ensure the connection state is updated
      await this.unityClient.ensureConnected();

      // Unity connection established via discovery

      // Execute callback if provided
      if (onConnectionEstablished) {
        await onConnectionEstablished();
      }

      // Stop discovery after successful connection
      this.unityDiscovery.stop();
    } catch (error) {
      // Log the error instead of silently swallowing it
      VibeLogger.logError(
        'unity_connection_manager_discovery_failed',
        'Failed to establish connection after Unity discovery',
        {
          error_message: error instanceof Error ? error.message : String(error),
        },
        undefined,
        'Discovery found Unity but connection establishment failed',
        'Will retry discovery to attempt recovery',
      );

      // Schedule retry discovery to attempt recovery
      this.scheduleRetryDiscovery();
    }
  }

  /**
   * Schedule retry discovery after connection failure
   */
  private scheduleRetryDiscovery(): void {
    setTimeout(() => {
      if (!this.unityClient.connected) {
        VibeLogger.logInfo(
          'unity_connection_manager_retry_discovery',
          'Retrying discovery after connection failure',
          undefined,
          undefined,
          'Restarting discovery to recover from failed connection attempt',
        );
        // Trigger connection lost handling which will restart discovery
        this.unityDiscovery.handleConnectionLost();
      }
    }, CONNECTION_RECOVERY.FORCE_RECONNECT_DELAY_MS);
  }

  /**
   * Initialize connection manager with guard against concurrent initialization
   */
  initialize(onConnectionEstablished?: () => Promise<void>): void {
    // Guard: already initialized
    if (this.isInitialized) {
      return;
    }

    this.isInitialized = true;

    // Setup discovery callback
    this.unityDiscovery.setOnDiscoveredCallback(async (_port: number) => {
      await this.handleUnityDiscovered(onConnectionEstablished);
    });

    // Start Unity discovery immediately
    this.unityDiscovery.start();
  }

  /**
   * Setup reconnection callback
   */
  setupReconnectionCallback(callback: () => Promise<void>): void {
    this.unityClient.setReconnectedCallback(() => {
      // Prevent duplicate reconnection handling
      if (this.isReconnecting) {
        return;
      }

      this.isReconnecting = true;

      // Force Unity discovery for faster reconnection
      void this.unityDiscovery
        .forceDiscovery()
        .then(() => {
          return callback();
        })
        .finally(() => {
          this.isReconnecting = false;
        });
    });
  }

  /**
   * Check if Unity is connected
   */
  isConnected(): boolean {
    return this.unityClient.connected;
  }

  /**
   * Ensure Unity connection is established
   */
  async ensureConnected(timeoutMs: number = 10000): Promise<void> {
    if (this.isConnected()) {
      return;
    }
    await this.waitForUnityConnectionWithTimeout(timeoutMs);
  }

  /**
   * Test connection (validate connection state)
   *
   * Note: Lightweight socket state check only (no ping).
   */
  testConnection(): boolean {
    return this.unityClient.testConnection();
  }

  /**
   * Disconnect from Unity
   */
  disconnect(): void {
    this.unityDiscovery.stop();
    this.unityClient.disconnect();
  }
}
