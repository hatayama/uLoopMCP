import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { VibeLogger } from './utils/vibe-logger.js';
import { KEEPALIVE, ENVIRONMENT } from './constants.js';
import { UnityClient } from './unity-client.js';

/**
 * MCP Keepalive Service - Prevents Cursor's idle timeout by sending periodic pings
 *
 * Design document reference: Packages/src/TypeScriptServer~/ARCHITECTURE.md
 *
 * Related classes:
 * - Server: MCP server instance for sending pings
 * - UnityMcpServer: Main server class that uses this service
 * - UnityEventHandler: Handles graceful shutdown including stopping keepalive
 *
 * Key features:
 * - Sends periodic ping requests to the MCP client (Cursor)
 * - Tracks consecutive failures and stops after threshold
 * - Can be started/stopped independently
 * - Logs ping results for debugging
 *
 * BUG WORKAROUND: Cursor disconnects MCPサーバー when idle for too long.
 * This service keeps the connection alive by sending periodic pings.
 */
export class McpKeepaliveService {
  private keepaliveInterval: NodeJS.Timeout | null = null;
  private server: Server;
  private unityClient: UnityClient | null = null;
  private consecutiveFailures: number = 0;
  private isRunning: boolean = false;
  private readonly isDevelopment: boolean;

  constructor(server: Server) {
    this.server = server;
    this.isDevelopment = process.env.NODE_ENV === ENVIRONMENT.NODE_ENV_DEVELOPMENT;
  }

  /**
   * Set UnityClient reference for diagnostic logging
   */
  setUnityClient(unityClient: UnityClient): void {
    this.unityClient = unityClient;
  }

  /**
   * Start the keepalive service
   * Should be called after MCP initialization is complete
   */
  start(): void {
    if (!KEEPALIVE.ENABLED) {
      if (this.isDevelopment) {
        VibeLogger.logDebug(
          'keepalive_disabled',
          'Keepalive is disabled in configuration',
          undefined,
          undefined,
          'Keepalive service will not start',
        );
      }
      return;
    }

    if (this.isRunning) {
      if (this.isDevelopment) {
        VibeLogger.logDebug(
          'keepalive_already_running',
          'Keepalive service is already running',
          undefined,
          undefined,
          'Ignoring duplicate start request',
        );
      }
      return;
    }

    this.isRunning = true;
    this.consecutiveFailures = 0;

    VibeLogger.logInfo(
      'keepalive_started',
      'Keepalive service started',
      { interval_ms: KEEPALIVE.INTERVAL_MS },
      undefined,
      'Will send periodic pings to prevent Cursor idle timeout',
    );

    this.keepaliveInterval = setInterval((): void => {
      void this.sendPing();
    }, KEEPALIVE.INTERVAL_MS);
  }

  /**
   * Send a ping to the MCP client
   */
  private async sendPing(): Promise<void> {
    if (!this.isRunning) {
      return;
    }

    try {
      await Promise.race([
        this.server.ping(),
        new Promise<never>((_, reject) =>
          setTimeout(
            () => reject(new Error(`Keepalive ping timed out after ${KEEPALIVE.TIMEOUT_MS} ms`)),
            KEEPALIVE.TIMEOUT_MS,
          ),
        ),
      ]);
      this.consecutiveFailures = 0;

      if (this.isDevelopment) {
        VibeLogger.logDebug(
          'keepalive_ping_success',
          'Keepalive ping succeeded',
          undefined,
          undefined,
          'Connection is alive',
        );
      }
    } catch (error) {
      this.consecutiveFailures++;

      // Include Unity connection state for diagnosis of Problem 2 (MCP OFF issue)
      const unityConnected: boolean | string = this.unityClient?.connected ?? 'unknown';

      VibeLogger.logError(
        'keepalive_ping_failed',
        'Keepalive ping failed',
        {
          error: error instanceof Error ? error.message : String(error),
          consecutive_failures: this.consecutiveFailures,
          max_failures: KEEPALIVE.MAX_CONSECUTIVE_FAILURES,
          unity_connected: unityConnected,
        },
        undefined,
        'Ping to MCP client failed',
        'If Unity is also disconnected, this may indicate a broader connection issue',
      );

      if (this.consecutiveFailures >= KEEPALIVE.MAX_CONSECUTIVE_FAILURES) {
        VibeLogger.logError(
          'keepalive_stopped_max_failures',
          'Keepalive stopped due to max consecutive failures',
          {
            consecutive_failures: this.consecutiveFailures,
            unity_connected: unityConnected,
          },
          undefined,
          'Connection may be lost - stopping keepalive to prevent log spam',
          'Check Unity connection status and MCP client (Cursor) state',
        );
        this.stop();
      }
    }
  }

  /**
   * Stop the keepalive service
   */
  stop(): void {
    if (!this.isRunning) {
      return;
    }

    this.isRunning = false;

    if (this.keepaliveInterval) {
      clearInterval(this.keepaliveInterval);
      this.keepaliveInterval = null;
    }

    VibeLogger.logInfo(
      'keepalive_stopped',
      'Keepalive service stopped',
      undefined,
      undefined,
      'Keepalive pings will no longer be sent',
    );
  }

  /**
   * Check if the keepalive service is running
   */
  isActive(): boolean {
    return this.isRunning;
  }
}
