import { UnityClient } from './unity-client.js';
import { UnityDiscovery } from './unity-discovery.js';
import { UnityConnectionFallbackHandler } from './unity-connection-fallback-handler.js';
import { ENVIRONMENT } from './constants.js';
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
  private fallbackHandler: UnityConnectionFallbackHandler;
  private readonly enableConnectionDebugLog: boolean;
  private connectionManagerInitialized: boolean = false;
  private reconnectionInProgress: boolean = false;

  constructor(unityClient: UnityClient) {
    this.unityClient = unityClient;
    this.enableConnectionDebugLog = process.env.NODE_ENV === ENVIRONMENT.NODE_ENV_DEVELOPMENT;

    // Initialize Unity discovery service (singleton pattern prevents duplicates)
    this.unityDiscovery = UnityDiscovery.getInstance(this.unityClient);

    // Initialize fallback handler for connection monitoring
    this.fallbackHandler = new UnityConnectionFallbackHandler(
      this.unityClient,
      this.unityDiscovery,
    );

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
   * Wait for Unity connection with timeout using push notification system
   */
  async waitForUnityConnectionWithTimeout(timeoutMs: number): Promise<void> {
    return this.fallbackHandler.waitForConnectionWithFallback(timeoutMs, () => {
      // Initialize connection manager if not already done
      if (!this.connectionManagerInitialized) {
        this.initialize(() => Promise.resolve());
      }
    });
  }

  /**
   * Wait for Unity connection and tools availability by polling
   * More reliable than timeout-based waiting
   */
  async waitForUnityConnectionAndTools(toolManager: any, maxWaitMs: number = 10000): Promise<void> {
    const startTime = Date.now();
    const pollInterval = 100; // Poll every 100ms

    // Initialize connection manager if not already done
    if (!this.connectionManagerInitialized) {
      this.initialize(() => Promise.resolve());
    }

    return new Promise<void>((resolve, reject) => {
      const checkToolsAvailable = async () => {
        if (this.unityClient.connected) {
          try {
            // Try to get tools to verify Unity is ready
            await toolManager.getToolsFromUnity();
            resolve();
            return;
          } catch (error) {
            // Tools not ready yet, continue polling
          }
        }

        const elapsed = Date.now() - startTime;
        if (elapsed >= maxWaitMs) {
          reject(new Error(`Unity connection and tools timeout after ${maxWaitMs}ms`));
          return;
        }

        setTimeout(checkToolsAvailable, pollInterval);
      };

      checkToolsAvailable();
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

      if (this.enableConnectionDebugLog) {
        // Unity discovered - connection state updated
      }

      // Unity connection established via discovery

      // Execute callback if provided
      if (onConnectionEstablished) {
        await onConnectionEstablished();
      }

      // Stop discovery after successful connection
      this.unityDiscovery.stop();
    } catch (error) {
      // Failed to handle Unity discovery
    }
  }

  /**
   * Initialize connection manager with push notification system
   */
  initialize(onConnectionEstablished?: () => Promise<void>): void {
    if (this.connectionManagerInitialized) {
      return;
    }

    this.connectionManagerInitialized = true;

    VibeLogger.logInfo(
      'push_notification_system_active',
      'Unity connection manager initialized with push notification system',
      {},
      undefined,
      'Push notification system active - no legacy polling',
    );

    // Setup discovery callbacks for push notification system
    this.unityDiscovery.setOnDiscoveredCallback(async (_port: number) => {
      await this.handleUnityDiscovered(onConnectionEstablished);
    });

    this.unityDiscovery.setOnConnectionLostCallback(() => {
      if (this.enableConnectionDebugLog) {
        // Connection lost detected - ready for reconnection
      }
    });
  }

  /**
   * Setup reconnection callback
   */
  setupReconnectionCallback(callback: () => Promise<void>): void {
    this.unityClient.setReconnectedCallback(() => {
      // Prevent duplicate reconnection handling
      if (this.reconnectionInProgress) {
        if (this.enableConnectionDebugLog) {
          // Reconnection already in progress, skipping duplicate callback
        }
        return;
      }

      this.reconnectionInProgress = true;

      // Force Unity discovery for faster reconnection
      void this.unityDiscovery
        .forceDiscovery()
        .then(() => {
          return callback();
        })
        .finally(() => {
          this.reconnectionInProgress = false;
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
   * Disconnect from Unity
   */
  disconnect(): void {
    this.unityDiscovery.stop();
    this.unityClient.disconnect();
  }
}
