import { UnityClient } from './unity-client.js';
import { UnityDiscovery } from './unity-discovery.js';
import { ENVIRONMENT, POLLING } from './constants.js';
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
   * Initialize connection manager
   */
  initialize(onConnectionEstablished?: () => Promise<void>): void {
    if (this.isInitialized) {
      return;
    }

    this.isInitialized = true;

    // Check if polling is disabled
    if (!POLLING.ENABLED) {
      VibeLogger.logInfo(
        'polling_disabled',
        'Unity polling disabled - Push notification system active',
        { polling_enabled: POLLING.ENABLED },
        undefined,
        'Legacy polling system disabled in favor of Push notifications'
      );
      
      // Still set up callbacks for potential fallback, but don't start discovery
      this.unityDiscovery.setOnDiscoveredCallback(() => {
        void this.handleUnityDiscovered(onConnectionEstablished);
      });

      this.unityDiscovery.setOnConnectionLostCallback(() => {
        if (this.isDevelopment) {
          // Connection lost detected - ready for reconnection
        }
      });
      
      return;
    }

    // Legacy polling mode (fallback)
    VibeLogger.logWarning(
      'polling_fallback_active',
      'Using legacy polling mode as fallback',
      { polling_enabled: POLLING.ENABLED },
      undefined,
      'Push notification system may not be available - using polling fallback'
    );

    // Setup discovery callback
    this.unityDiscovery.setOnDiscoveredCallback(() => {
      // eslint-disable-next-line @typescript-eslint/no-floating-promises
      void this.handleUnityDiscovered(onConnectionEstablished);
    });

    // Setup connection lost callback for connection recovery
    this.unityDiscovery.setOnConnectionLostCallback(() => {
      if (this.isDevelopment) {
        // Connection lost detected - ready for reconnection
      }
    });

    // Start Unity discovery immediately
    this.unityDiscovery.start();

    if (this.isDevelopment) {
      // Connection manager initialized with polling
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
