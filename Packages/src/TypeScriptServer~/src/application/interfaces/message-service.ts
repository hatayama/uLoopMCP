/**
 * Message Service Interface
 *
 * Design document reference:
 * - /Packages/docs/ARCHITECTURE_TypeScript.md#IMessageService
 *
 * Related classes:
 * - MessageHandler (existing implementation class)
 * - MessageAppService (new application service implementation)
 * - UnityClient (uses MessageHandler for JSON-RPC communication)
 * - Used by UseCase classes
 */

import { ApplicationService } from '../../domain/base-interfaces.js';

/**
 * Interface providing technical functionality for JSON-RPC message processing
 *
 * Responsibilities:
 * - JSON-RPC message parsing and routing
 * - Notification handler registration
 * - Pending request management
 * - Content-Length framing support
 * - Provide single-purpose operations
 */
export interface IMessageService extends ApplicationService {
  /**
   * Handle incoming data with Content-Length framing
   *
   * @param data Buffer or string data from Unity
   */
  handleIncomingData(data: Buffer | string): void;

  /**
   * Create JSON-RPC request with Content-Length framing
   *
   * @param method Method name
   * @param params Request parameters
   * @param id Request ID
   * @returns Framed JSON-RPC request string
   */
  createRequest(method: string, params: Record<string, unknown>, id: string): string;

  /**
   * Register pending request for response tracking
   *
   * @param id Request ID
   * @param resolve Resolve callback
   * @param reject Reject callback
   */
  registerPendingRequest(
    id: string,
    resolve: (value: unknown) => void,
    reject: (reason: unknown) => void,
  ): void;

  /**
   * Clear all pending requests with rejection (error)
   *
   * @param reason Reason for clearing (used in error messages)
   */
  clearPendingRequests(reason: string): void;

  /**
   * Clear all pending requests with resolution (success)
   * Used when connection is temporarily lost but will recover (e.g., domain reload)
   *
   * @param message Success message to return to pending requests
   */
  clearPendingRequestsWithSuccess(message: string): void;

  /**
   * Register notification handler for specific method
   *
   * @param method Method name
   * @param handler Handler function
   */
  onNotification(method: string, handler: (params: unknown) => void): void;

  /**
   * Remove notification handler
   *
   * @param method Method name
   */
  offNotification(method: string): void;

  /**
   * Clear the dynamic buffer (for connection reset)
   */
  clearBuffer(): void;

  /**
   * Get buffer statistics for debugging
   *
   * @returns Buffer statistics
   */
  getBufferStats(): {
    size: number;
    maxSize: number;
    utilization: number;
    hasCompleteHeader: boolean;
    preview: string;
  };
}
