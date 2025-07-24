/**
 * Connection Service Interface
 * 
 * Design document reference:
 * - .kiro/specs/typescript-server-ddd-refactoring/design.md#IConnectionService
 * 
 * Related classes:
 * - UnityConnectionManager (existing implementation class)
 * - ConnectionAppService (new application service implementation)
 * - Used by UseCase classes
 */

import { ApplicationService } from '../../domain/base-interfaces.js';

/**
 * Interface providing technical functionality for Unity connection management
 * 
 * Responsibilities:
 * - Unity connection state management
 * - Connection establishment and disconnection
 * - Connection monitoring and reconnection
 * - Provide single-purpose operations
 */
export interface IConnectionService extends ApplicationService {
  /**
   * Check Unity connection status
   * 
   * @returns true if connected
   */
  isConnected(): boolean;

  /**
   * Ensure Unity connection is established (only if not connected)
   * 
   * @param timeoutMs Timeout in milliseconds
   * @returns Promise for connection establishment
   * @throws ConnectionError if connection fails
   */
  ensureConnected(timeoutMs?: number): Promise<void>;

  /**
   * Disconnect from Unity
   */
  disconnect(): void;

  /**
   * Test connection (validate connection state)
   * 
   * @returns true if connection is valid
   */
  testConnection(): Promise<boolean>;

  /**
   * Setup reconnection callback
   * 
   * @param callback Callback to execute on reconnection
   */
  setupReconnectionCallback(callback: () => Promise<void>): void;
}