import { UnityClient } from './unity-client.js';
import { UnityDiscovery } from './unity-discovery.js';
import { VibeLogger } from './utils/vibe-logger.js';

/**
 * Unity Connection Fallback Handler - Manages connection fallback strategies
 *
 * Design document reference: Packages/src/TypeScriptServer~/ARCHITECTURE.md
 *
 * Related classes:
 * - UnityClient: Unity TCP connection management
 * - UnityDiscovery: Unity discovery service
 * - UnityConnectionManager: Main connection management
 *
 * Key responsibilities:
 * - Fallback connection monitoring for synchronous initialization
 * - Connection state polling when push notifications fail
 * - Detailed logging for troubleshooting connection issues
 */
export class UnityConnectionFallbackHandler {
  private unityClient: UnityClient;
  private unityDiscovery: UnityDiscovery;

  constructor(unityClient: UnityClient, unityDiscovery: UnityDiscovery) {
    this.unityClient = unityClient;
    this.unityDiscovery = unityDiscovery;
  }

  /**
   * Wait for Unity connection using fallback polling strategy
   * Used when push notification system is not available or fails
   */
  async waitForConnectionWithFallback(
    timeoutMs: number,
    onInitializeRequired: () => void,
  ): Promise<void> {
    return new Promise((resolve, reject) => {
      // Check if already connected
      if (this.unityClient.connected) {
        resolve();
        return;
      }

      const timeout = setTimeout(() => {
        clearInterval(connectionCheckInterval);
        reject(new Error(`Unity connection timeout after ${timeoutMs}ms`));
      }, timeoutMs);

      VibeLogger.logInfo(
        'unity_connection_wait_fallback_mode',
        'Waiting for Unity connection using fallback polling',
        { timeout_ms: timeoutMs, current_connected: this.unityClient.connected },
        undefined,
        'Push notification system fallback - monitoring connection state',
      );

      // Set up fallback connection monitoring
      let fallbackCheckCount = 0;
      const connectionCheckInterval = setInterval(() => {
        fallbackCheckCount++;

        if (this.unityClient.connected) {
          clearTimeout(timeout);
          clearInterval(connectionCheckInterval);

          this.logConnectionSuccess(timeoutMs, fallbackCheckCount);
          resolve();
        } else {
          this.logFallbackProgress(timeoutMs, fallbackCheckCount);
        }
      }, 250); // Fallback check every 250ms

      // Trigger initialization if required
      onInitializeRequired();

      // Try force discovery as last resort fallback
      setTimeout(() => {
        void this.attemptForceDiscovery(timeoutMs);
      }, 100);
    });
  }

  /**
   * Log connection success with fallback analysis
   */
  private logConnectionSuccess(timeoutMs: number, fallbackCheckCount: number): void {
    if (fallbackCheckCount === 1) {
      // Connection was immediately available - likely from push notification
      VibeLogger.logInfo(
        'unity_connection_established_immediate',
        'Unity connection established immediately (push notification)',
        {
          timeout_ms: timeoutMs,
          check_count: fallbackCheckCount,
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
          elapsed_approx_ms: fallbackCheckCount * 250,
        },
        undefined,
        'Push notification may have failed - connection detected via fallback',
      );
    }
  }

  /**
   * Log fallback progress periodically
   */
  private logFallbackProgress(timeoutMs: number, fallbackCheckCount: number): void {
    if (fallbackCheckCount % 4 === 0) {
      // Log every 1 second (4 * 250ms) that we're still waiting
      VibeLogger.logInfo(
        'unity_connection_fallback_waiting',
        'Still waiting for Unity connection (fallback polling)',
        {
          timeout_ms: timeoutMs,
          check_count: fallbackCheckCount,
          elapsed_approx_ms: fallbackCheckCount * 250,
        },
        undefined,
        'Fallback polling active - waiting for Unity connection',
      );
    }
  }

  /**
   * Attempt force discovery as last resort
   */
  private attemptForceDiscovery(timeoutMs: number): void {
    try {
      this.unityDiscovery.forceDiscovery();

      VibeLogger.logInfo(
        'unity_discovery_fallback_attempted',
        'Force discovery attempted as fallback',
        { timeout_ms: timeoutMs },
        undefined,
        'Fallback force discovery completed',
      );
    } catch (error) {
      VibeLogger.logInfo(
        'unity_discovery_fallback_failed',
        'Unity discovery fallback failed, relying on push notification',
        {
          timeout_ms: timeoutMs,
          error: error instanceof Error ? error.message : String(error),
        },
        undefined,
        'Waiting for push notification connection',
      );
    }
  }
}
