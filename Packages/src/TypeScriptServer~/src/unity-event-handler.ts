import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { UnityClient } from './unity-client.js';
import { UnityConnectionManager } from './unity-connection-manager.js';
import { ENVIRONMENT, NOTIFICATION_METHODS } from './constants.js';
import { VibeLogger } from './utils/vibe-logger.js';
import { IUnityEventService } from './application/interfaces/unity-event-service.js';
import { INotificationService } from './application/interfaces/notification-service.js';
import { IProcessControlService } from './application/interfaces/process-control-service.js';

/**
 * Unity Event Handler - Manages Unity notifications and event processing
 * Implements segregated interfaces following Interface Segregation Principle
 *
 * Design document reference: Packages/src/TypeScriptServer~/ARCHITECTURE.md
 *
 * Related classes:
 * - UnityClient: Manages communication with Unity Editor
 * - UnityConnectionManager: Manages Unity connection
 * - Server: MCP server instance for sending notifications
 * - UnityMcpServer: Main server class that uses this handler
 *
 * Key features:
 * - tools_changed notification sending
 * - Unity notification listener setup
 * - Signal handler configuration
 * - Graceful shutdown handling
 */
export class UnityEventHandler
  implements IUnityEventService, INotificationService, IProcessControlService
{
  private server: Server;
  private unityClient: UnityClient;
  private connectionManager: UnityConnectionManager;
  private readonly isDevelopment: boolean;
  private shuttingDown: boolean = false;
  private isNotifying: boolean = false;
  private hasSentListChangedNotification: boolean = false;

  constructor(server: Server, unityClient: UnityClient, connectionManager: UnityConnectionManager) {
    this.server = server;
    this.unityClient = unityClient;
    this.connectionManager = connectionManager;
    this.isDevelopment = process.env.NODE_ENV === ENVIRONMENT.NODE_ENV_DEVELOPMENT;
  }

  /**
   * Setup Unity event listener for automatic tool updates
   */
  setupUnityEventListener(onToolsChanged: () => Promise<void>): void {
    // Listen for MCP standard notifications from Unity
    this.unityClient.onNotification('notifications/tools/list_changed', (_params: unknown) => {
      VibeLogger.logInfo(
        'unity_notification_received',
        'Unity notification received: notifications/tools/list_changed',
        undefined,
        undefined,
        'Unity notified that tool list has changed',
      );

      try {
        void onToolsChanged();
      } catch (error) {
        VibeLogger.logError(
          'unity_notification_error',
          'Failed to update dynamic tools via Unity notification',
          { error: error instanceof Error ? error.message : String(error) },
          undefined,
          'Error occurred while processing Unity tool list change notification',
        );
      }
    });
  }

  /**
   * Send tools changed notification (with duplicate prevention)
   */
  sendToolsChangedNotification(): void {
    if (this.hasSentListChangedNotification) {
      if (this.isDevelopment) {
        VibeLogger.logDebug(
          'tools_notification_skipped_already_sent',
          'sendToolsChangedNotification skipped: list_changed already sent',
          undefined,
          undefined,
          'Subsequent list_changed notification suppressed',
        );
      }
      return;
    }

    if (this.isNotifying) {
      if (this.isDevelopment) {
        VibeLogger.logDebug(
          'tools_notification_skipped',
          'sendToolsChangedNotification skipped: already notifying',
          undefined,
          undefined,
          'Duplicate notification prevented',
        );
      }
      return;
    }

    this.isNotifying = true;
    try {
      void this.server.notification({
        method: NOTIFICATION_METHODS.TOOLS_LIST_CHANGED,
        params: {},
      });
      this.hasSentListChangedNotification = true;
      if (this.isDevelopment) {
        VibeLogger.logInfo(
          'tools_notification_sent',
          'tools/list_changed notification sent',
          undefined,
          undefined,
          'Successfully notified client of tool list changes',
        );
      }
    } catch (error) {
      VibeLogger.logError(
        'tools_notification_error',
        'Failed to send tools changed notification',
        { error: error instanceof Error ? error.message : String(error) },
        undefined,
        'Error occurred while sending tool list change notification',
      );
    } finally {
      this.isNotifying = false;
    }
  }

  /**
   * Setup signal handlers for graceful shutdown
   */
  setupSignalHandlers(): void {
    // Handle Ctrl+C (SIGINT)
    process.on('SIGINT', () => {
      VibeLogger.logInfo(
        'sigint_received',
        'Received SIGINT, shutting down...',
        undefined,
        undefined,
        'User pressed Ctrl+C, initiating graceful shutdown',
      );
      this.gracefulShutdown();
    });

    // Handle kill command (SIGTERM)
    process.on('SIGTERM', () => {
      VibeLogger.logInfo(
        'sigterm_received',
        'Received SIGTERM, shutting down...',
        undefined,
        undefined,
        'Process termination signal received, initiating graceful shutdown',
      );
      this.gracefulShutdown();
    });

    // Handle terminal close (SIGHUP)
    process.on('SIGHUP', () => {
      VibeLogger.logInfo(
        'sighup_received',
        'Received SIGHUP, shutting down...',
        undefined,
        undefined,
        'Terminal hangup signal received, initiating graceful shutdown',
      );
      this.gracefulShutdown();
    });

    // Handle stdin close (when parent process disconnects)
    // BUG FIX: Added STDIN monitoring to detect when Cursor/parent MCP client disconnects
    // This prevents orphaned Node processes from remaining after IDE shutdown
    process.stdin.on('close', () => {
      VibeLogger.logInfo(
        'stdin_closed',
        'STDIN closed, shutting down...',
        undefined,
        undefined,
        'Parent process disconnected, preventing orphaned process',
      );
      this.gracefulShutdown();
    });

    process.stdin.on('end', () => {
      VibeLogger.logInfo(
        'stdin_ended',
        'STDIN ended, shutting down...',
        undefined,
        undefined,
        'STDIN stream ended, initiating graceful shutdown',
      );
      this.gracefulShutdown();
    });

    // Handle uncaught exceptions
    // BUG FIX: Added comprehensive error handling to prevent hanging processes
    process.on('uncaughtException', (error) => {
      VibeLogger.logException(
        'uncaught_exception',
        error,
        undefined,
        undefined,
        'Uncaught exception occurred, shutting down safely',
      );
      this.gracefulShutdown();
    });

    process.on('unhandledRejection', (reason, promise) => {
      VibeLogger.logError(
        'unhandled_rejection',
        'Unhandled promise rejection',
        { reason: String(reason), promise: String(promise) },
        undefined,
        'Unhandled promise rejection occurred, shutting down safely',
      );
      this.gracefulShutdown();
    });
  }

  /**
   * Graceful shutdown with proper cleanup
   * BUG FIX: Enhanced shutdown process to prevent orphaned Node processes
   */
  gracefulShutdown(): void {
    // Prevent multiple shutdown attempts
    if (this.shuttingDown) {
      return;
    }

    this.shuttingDown = true;
    VibeLogger.logInfo(
      'graceful_shutdown_start',
      'Starting graceful shutdown...',
      undefined,
      undefined,
      'Initiating graceful shutdown process',
    );

    try {
      // Disconnect from Unity and stop all intervals
      // BUG FIX: Ensure polling intervals are stopped to prevent hanging event loop
      this.connectionManager.disconnect();

      // Clear any remaining timers to ensure clean exit
      // BUG FIX: Force garbage collection if available to clean up lingering references
      if (global.gc) {
        global.gc();
      }
    } catch (error) {
      VibeLogger.logError(
        'cleanup_error',
        'Error during cleanup',
        { error: error instanceof Error ? error.message : String(error) },
        undefined,
        'Error occurred during graceful shutdown cleanup',
      );
    }

    VibeLogger.logInfo(
      'graceful_shutdown_complete',
      'Graceful shutdown completed',
      undefined,
      undefined,
      'All cleanup completed, process will exit',
    );
    process.exit(0);
  }

  /**
   * Check if shutdown is in progress
   */
  isShuttingDown(): boolean {
    return this.shuttingDown;
  }
}
