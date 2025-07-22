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
  private readonly isDevelopment: boolean;
  private isInitialized: boolean = false;
  private isReconnecting: boolean = false;

  constructor(unityClient: UnityClient) {
    this.unityClient = unityClient;
    this.isDevelopment = process.env.NODE_ENV === ENVIRONMENT.NODE_ENV_DEVELOPMENT;

    // Initialize Unity discovery service (singleton pattern prevents duplicates)
    this.unityDiscovery = UnityDiscovery.getInstance(this.unityClient);

    // Initialize fallback handler for connection monitoring
    this.fallbackHandler = new UnityConnectionFallbackHandler(this.unityClient, this.unityDiscovery);

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
      if (!this.isInitialized) {
        this.initialize(() => Promise.resolve());
      }
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

      if (this.isDevelopment) {
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
    if (this.isInitialized) {
      return;
    }

    this.isInitialized = true;

    VibeLogger.logInfo(
      'push_notification_system_active',
      'Unity connection manager initialized with push notification system',
      {},
      undefined,
      'Push notification system active - no legacy polling',
    );

    // Setup discovery callbacks for push notification system
    this.unityDiscovery.setOnDiscoveredCallback(() => {
      void this.handleUnityDiscovered(onConnectionEstablished);
    });

    this.unityDiscovery.setOnConnectionLostCallback(() => {
      if (this.isDevelopment) {
        // Connection lost detected - ready for reconnection
      }
    });

    if (this.isDevelopment) {
      // Connection manager initialized with push notification system
    }
  }

  /**
   * Setup reconnection callback
   */
  setupReconnectionCallback(callback: () => Promise<void>): void {
    this.unityClient.setReconnectedCallback(() => {
      // Prevent duplicate reconnection handling
      if (this.isReconnecting) {
        if (this.isDevelopment) {
          // Reconnection already in progress, skipping duplicate callback
        }
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
   * Disconnect from Unity
   */
  disconnect(): void {
    this.unityDiscovery.stop();
    this.unityClient.disconnect();
  }
}
