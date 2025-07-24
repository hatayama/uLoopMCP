/**
 * Discovery Service Interface
 *
 * Design document reference:
 * - .kiro/specs/typescript-server-ddd-refactoring/design.md#IDiscoveryService
 *
 * Related classes:
 * - UnityDiscovery (existing implementation class)
 * - DiscoveryAppService (new application service implementation)
 * - UnityClient (managed by discovery)
 * - Used by UseCase classes
 */

import { ApplicationService } from '../../domain/base-interfaces.js';

/**
 * Interface providing technical functionality for Unity discovery and polling
 *
 * Responsibilities:
 * - Unity discovery process management
 * - Connection polling and health checking
 * - Discovery callback setup
 * - Force discovery for connection recovery
 * - Provide single-purpose operations
 */
export interface IDiscoveryService extends ApplicationService {
  /**
   * Start Unity discovery polling
   */
  start(): void;

  /**
   * Stop Unity discovery polling
   */
  stop(): void;

  /**
   * Force immediate Unity discovery for connection recovery
   *
   * @returns true if Unity was discovered and connected
   */
  forceDiscovery(): Promise<boolean>;

  /**
   * Set callback for when Unity is discovered
   *
   * @param callback Callback function with discovered port
   */
  setOnDiscoveredCallback(callback: (port: number) => Promise<void>): void;

  /**
   * Set callback for when connection is lost
   *
   * @param callback Callback function
   */
  setOnConnectionLostCallback(callback: () => void): void;

  /**
   * Handle connection lost event
   *
   * Called by UnityClient when connection is lost
   */
  handleConnectionLost(): void;

  /**
   * Get debugging information about current discovery state
   *
   * @returns Debug information object
   */
  getDebugInfo(): {
    isTimerActive: boolean;
    isDiscovering: boolean;
    activeTimerCount: number;
    isConnected: boolean;
    intervalMs: number;
    hasSingleton: boolean;
  };
}
