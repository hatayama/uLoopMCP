import { UnityClient } from './unity-client.js';
import { UnityDiscovery } from './unity-discovery.js';
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
   * Wait for Unity connection with timeout using push notification system
   */
  async waitForUnityConnectionWithTimeout(timeoutMs: number): Promise<void> {
    return new Promise((resolve, reject) => {
      // Check if already connected
      if (this.unityClient.connected) {
        resolve();
        return;
      }

      const timeout = setTimeout(() => {
        reject(new Error(`Unity connection timeout after ${timeoutMs}ms`));
      }, timeoutMs);

      VibeLogger.logInfo(
        'unity_connection_wait_push_mode',
        'Waiting for Unity connection in push notification mode',
        { timeout_ms: timeoutMs, current_connected: this.unityClient.connected },
        undefined,
        'Push notification system - waiting for Unity connection to establish',
      );

      // Set up fallback connection monitoring for sync initialization
      let fallbackCheckCount = 0;
      const connectionCheckInterval = setInterval(() => {
        fallbackCheckCount++;
        
        if (this.unityClient.connected) {
          clearTimeout(timeout);
          clearInterval(connectionCheckInterval);

          if (fallbackCheckCount === 1) {
            // Connection was immediately available - likely from push notification
            VibeLogger.logInfo(
              'unity_connection_established_immediate',
              'Unity connection established immediately (push notification)',
              { 
                timeout_ms: timeoutMs,
                check_count: fallbackCheckCount
              },
              undefined,
              'Unity connection confirmed via push notification system',
            );
          } else {
            // Connection was detected via fallback polling
            VibeLogger.logWarning(
              'unity_connection_established_fallback',
              'Unity connection established via fallback polling',
              { 
                timeout_ms: timeoutMs,
                check_count: fallbackCheckCount,
                elapsed_approx_ms: fallbackCheckCount * 250
              },
              undefined,
              'Push notification may have failed - connection detected via fallback',
            );
          }

          resolve();
        } else if (fallbackCheckCount % 4 === 0) {
          // Log every 1 second (4 * 250ms) that we're still waiting
          VibeLogger.logInfo(
            'unity_connection_fallback_waiting',
            'Still waiting for Unity connection (fallback polling)',
            { 
              timeout_ms: timeoutMs,
              check_count: fallbackCheckCount,
              elapsed_approx_ms: fallbackCheckCount * 250
            },
            undefined,
            'Fallback polling active - waiting for Unity connection',
          );
        }
      }, 250); // Fallback check every 250ms

      // Initialize connection manager if not already done
      if (!this.isInitialized) {
        this.initialize(() => {
          return Promise.resolve();
        });
      }

      // Try force discovery as fallback, but don't wait for it
      setTimeout(() => {
        void this.unityDiscovery.forceDiscovery().catch(() => {
          // Discovery failed - connection may come via push notification instead
          VibeLogger.logInfo(
            'unity_discovery_fallback_failed',
            'Unity discovery fallback failed, relying on push notification',
            { timeout_ms: timeoutMs },
            undefined,
            'Waiting for push notification connection',
          );
        });
      }, 100);
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
