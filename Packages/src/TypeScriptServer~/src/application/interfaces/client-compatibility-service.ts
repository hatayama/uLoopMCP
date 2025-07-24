/**
 * Client Compatibility Service Interface
 *
 * Design document reference:
 * - .kiro/specs/typescript-server-ddd-refactoring/design.md#IClientCompatibilityService
 *
 * Related classes:
 * - McpClientCompatibility (existing implementation class)
 * - ClientCompatibilityAppService (new application service implementation)
 * - UnityClient (receives client name)
 * - Used by UseCase classes
 */

import { ApplicationService } from '../../domain/base-interfaces.js';

/**
 * Interface providing technical functionality for client compatibility management
 *
 * Responsibilities:
 * - Client name management and initialization
 * - List_changed support/unsupported detection
 * - MCP client compatibility checking
 * - Client initialization on reconnection
 * - Provide single-purpose operations
 */
export interface IClientCompatibilityService extends ApplicationService {
  /**
   * Set client name
   *
   * @param clientName Client name
   */
  setClientName(clientName: string): void;

  /**
   * Get current client name
   *
   * @returns Current client name
   */
  getClientName(): string;

  /**
   * Check if client doesn't support list_changed notifications
   *
   * @param clientName Client name to check
   * @returns true if list_changed is unsupported
   */
  isListChangedUnsupported(clientName: string): boolean;

  /**
   * Check if client supports list_changed notifications
   *
   * @param clientName Client name to check
   * @returns true if list_changed is supported
   */
  isListChangedSupported(clientName: string): boolean;

  /**
   * Initialize client with name
   *
   * @param clientName Client name
   * @throws ClientCompatibilityError if initialization fails
   */
  initializeClient(clientName: string): Promise<void>;

  /**
   * Handle client name initialization and setup
   *
   * Handles fallback to environment variable and reconnection setup
   * @throws ClientCompatibilityError if initialization fails
   */
  handleClientNameInitialization(): Promise<void>;

  /**
   * Log client compatibility information
   *
   * @param clientName Client name
   */
  logClientCompatibility(clientName: string): void;
}
