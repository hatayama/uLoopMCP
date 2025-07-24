/**
 * Event Service Interface
 * 
 * Design document reference:
 * - .kiro/specs/typescript-server-ddd-refactoring/design.md#IEventService
 * 
 * Related classes:
 * - UnityEventHandler (existing implementation class)
 * - EventHandlingAppService (new application service implementation)
 * - Server (MCP SDK server)
 * - Used by UseCase classes
 */

import { ApplicationService } from '../../domain/base-interfaces.js';

/**
 * Interface providing technical functionality for event processing and notifications
 * 
 * Responsibilities:
 * - Setup Unity event listeners
 * - Send notifications to MCP clients
 * - Setup signal handlers
 * - Provide single-purpose operations
 */
export interface IEventService extends ApplicationService {
  /**
   * Setup Unity event listener
   * 
   * @param onToolsChanged Callback for when tools change
   */
  setupUnityEventListener(onToolsChanged: () => Promise<void>): void;

  /**
   * Send tools changed notification
   * 
   * With duplicate sending prevention
   */
  sendToolsChangedNotification(): void;

  /**
   * Setup signal handlers
   * 
   * Handles SIGINT, SIGTERM, SIGHUP, stdin close etc.
   */
  setupSignalHandlers(): void;

  /**
   * Execute graceful shutdown
   * 
   * Cleanup processing on process termination
   */
  gracefulShutdown(): void;

  /**
   * Check if shutdown is in progress
   * 
   * @returns true if shutting down
   */
  isShuttingDown(): boolean;
}