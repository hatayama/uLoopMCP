import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { UnityClient } from './unity-client.js';
import { UnityConnectionManager } from './unity-connection-manager.js';
import { errorToFile, debugToFile, infoToFile } from './utils/log-to-file.js';
import { ENVIRONMENT } from './constants.js';

/**
 * Unity Event Handler - Manages Unity notifications and event processing
 *
 * Design document reference: Packages/src/Editor/ARCHITECTURE.md
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
export class UnityEventHandler {
  private server: Server;
  private unityClient: UnityClient;
  private connectionManager: UnityConnectionManager;
  private readonly isDevelopment: boolean;
  private isShuttingDown: boolean = false;
  private isNotifying: boolean = false;

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
    this.unityClient.onNotification('notifications/tools/list_changed', async (params: unknown) => {
      if (this.isDevelopment) {
        const timestamp = new Date().toISOString().split('T')[1].slice(0, 12);
        debugToFile(
          `[TRACE] Unity notification received at ${timestamp}: notifications/tools/list_changed`,
        );
        debugToFile(`[TRACE] Notification params: ${JSON.stringify(params)}`);
      }

      try {
        await onToolsChanged();
      } catch (error) {
        errorToFile('[Unity Event Handler] Failed to update dynamic tools via Unity notification:', error);
      }
    });
  }

  /**
   * Send tools changed notification (with duplicate prevention)
   */
  sendToolsChangedNotification(): void {
    if (this.isNotifying) {
      if (this.isDevelopment) {
        debugToFile('[Unity Event Handler] sendToolsChangedNotification skipped: already notifying');
      }
      return;
    }

    this.isNotifying = true;
    try {
      this.server.notification({
        method: 'notifications/tools/list_changed',
        params: {},
      });
      if (this.isDevelopment) {
        debugToFile('[Unity Event Handler] tools/list_changed notification sent');
      }
    } catch (error) {
      errorToFile('[Unity Event Handler] Failed to send tools changed notification:', error);
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
      infoToFile('[Unity Event Handler] Received SIGINT, shutting down...');
      this.gracefulShutdown();
    });

    // Handle kill command (SIGTERM)
    process.on('SIGTERM', () => {
      infoToFile('[Unity Event Handler] Received SIGTERM, shutting down...');
      this.gracefulShutdown();
    });

    // Handle terminal close (SIGHUP)
    process.on('SIGHUP', () => {
      infoToFile('[Unity Event Handler] Received SIGHUP, shutting down...');
      this.gracefulShutdown();
    });

    // Handle stdin close (when parent process disconnects)
    // BUG FIX: Added STDIN monitoring to detect when Cursor/parent MCP client disconnects
    // This prevents orphaned Node processes from remaining after IDE shutdown
    process.stdin.on('close', () => {
      infoToFile('[Unity Event Handler] STDIN closed, shutting down...');
      this.gracefulShutdown();
    });

    process.stdin.on('end', () => {
      infoToFile('[Unity Event Handler] STDIN ended, shutting down...');
      this.gracefulShutdown();
    });

    // Handle uncaught exceptions
    // BUG FIX: Added comprehensive error handling to prevent hanging processes
    process.on('uncaughtException', (error) => {
      errorToFile('[Unity Event Handler] Uncaught exception:', error);
      this.gracefulShutdown();
    });

    process.on('unhandledRejection', (reason, promise) => {
      errorToFile('[Unity Event Handler] Unhandled rejection at:', promise, 'reason:', reason);
      this.gracefulShutdown();
    });
  }

  /**
   * Graceful shutdown with proper cleanup
   * BUG FIX: Enhanced shutdown process to prevent orphaned Node processes
   */
  gracefulShutdown(): void {
    // Prevent multiple shutdown attempts
    if (this.isShuttingDown) {
      return;
    }

    this.isShuttingDown = true;
    infoToFile('[Unity Event Handler] Starting graceful shutdown...');

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
      errorToFile('[Unity Event Handler] Error during cleanup:', error);
    }

    infoToFile('[Unity Event Handler] Graceful shutdown completed');
    process.exit(0);
  }

  /**
   * Check if shutdown is in progress
   */
  isShuttingDownCheck(): boolean {
    return this.isShuttingDown;
  }
}