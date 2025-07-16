import { VibeLogger } from './utils/vibe-logger.js';

/**
 * Manages TCP connection state without polling
 * Follows Single Responsibility Principle - only handles connection state monitoring
 *
 * NOTE: Polling functionality has been moved to UnityDiscovery to prevent multiple
 * concurrent timers and improve connection stability.
 *
 * Design document reference: Packages/src/TypeScriptServer~/ARCHITECTURE.md
 *
 * Related classes:
 * - UnityClient: Uses this class for connection state management
 * - UnityDiscovery: Handles connection polling (moved from this class)
 */
export class ConnectionManager {
  private onReconnectedCallback: (() => void) | null = null;
  private onConnectionLostCallback: (() => void) | null = null;

  /**
   * Set callback for when connection is restored
   */
  setReconnectedCallback(callback: () => void): void {
    this.onReconnectedCallback = callback;
  }

  /**
   * Set callback for when connection is lost
   */
  setConnectionLostCallback(callback: () => void): void {
    this.onConnectionLostCallback = callback;
  }

  /**
   * Trigger reconnection callback
   */
  triggerReconnected(): void {
    if (this.onReconnectedCallback) {
      try {
        this.onReconnectedCallback();
      } catch (error) {
        VibeLogger.logError(
          'connection_manager_reconnect_error',
          'Error in reconnection callback',
          { error },
        );
      }
    }
  }

  /**
   * Trigger connection lost callback
   */
  triggerConnectionLost(): void {
    if (this.onConnectionLostCallback) {
      try {
        this.onConnectionLostCallback();
      } catch (error) {
        VibeLogger.logError(
          'connection_manager_connection_lost_error',
          'Error in connection lost callback',
          { error },
        );
      }
    }
  }

  /**
   * Legacy method for backward compatibility - now does nothing
   * @deprecated Use UnityDiscovery for connection polling instead
   */
  startPolling(_connectFn?: () => Promise<void>): void {
    // No-op: polling is now handled by UnityDiscovery
    // This method is kept for backward compatibility
    // The _connectFn parameter is intentionally unused and kept for backward compatibility
  }

  /**
   * Legacy method for backward compatibility - now does nothing
   * @deprecated Polling is now handled by UnityDiscovery
   */
  stopPolling(): void {
    // No-op: polling is now handled by UnityDiscovery
    // This method is kept for backward compatibility
  }
}
