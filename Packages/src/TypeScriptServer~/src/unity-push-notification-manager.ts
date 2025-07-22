import { VibeLogger } from './utils/vibe-logger.js';
import { UnityClient } from './unity-client.js';
import { UnityConnectionManager } from './unity-connection-manager.js';
import { UnityToolManager } from './unity-tool-manager.js';
import { UnityEventHandler } from './unity-event-handler.js';
import {
  UnityPushNotificationReceiveServer,
  UnityConnectedEvent,
  UnityDisconnectedEvent,
  PushNotificationEvent,
} from './unity-push-notification-receive-server.js';

/**
 * Unity Push Notification Manager - Manages push notification events and Unity state synchronization
 *
 * Design document reference: Packages/src/TypeScriptServer~/ARCHITECTURE.md
 *
 * Related classes:
 * - UnityPushNotificationReceiveServer: TCP server for receiving push notifications
 * - UnityClient: Manages TCP connection state synchronization
 * - UnityConnectionManager: Handles connection lifecycle management
 * - UnityToolManager: Manages dynamic tool refresh
 * - UnityEventHandler: Sends MCP notifications to clients
 *
 * Key responsibilities:
 * - Push notification event handling and routing
 * - Unity connection state synchronization
 * - Tool refresh coordination
 * - Domain reload recovery management
 */
export class UnityPushNotificationManager {
  private pushNotificationServer: UnityPushNotificationReceiveServer;
  private unityClient: UnityClient;
  private connectionManager: UnityConnectionManager;
  private toolManager: UnityToolManager;
  private eventHandler: UnityEventHandler;

  constructor(
    pushNotificationServer: UnityPushNotificationReceiveServer,
    unityClient: UnityClient,
    connectionManager: UnityConnectionManager,
    toolManager: UnityToolManager,
    eventHandler: UnityEventHandler,
  ) {
    this.pushNotificationServer = pushNotificationServer;
    this.unityClient = unityClient;
    this.connectionManager = connectionManager;
    this.toolManager = toolManager;
    this.eventHandler = eventHandler;

    this.setupPushNotificationHandlers();
  }

  /**
   * Start the push notification receive server
   */
  async startPushNotificationServer(): Promise<number> {
    const pushServerPort = await this.pushNotificationServer.start();
    
    VibeLogger.logInfo(
      'push_server_started',
      'Push notification receive server started',
      { port: pushServerPort },
      undefined,
      'TypeScript Push notification server is ready to receive Unity connections',
    );

    return pushServerPort;
  }

  /**
   * Stop the push notification receive server
   */
  async stopPushNotificationServer(): Promise<void> {
    await this.pushNotificationServer.stop();
  }

  /**
   * Setup push notification event handlers
   */
  private setupPushNotificationHandlers(): void {
    this.pushNotificationServer.on('unity_connected', this.handleUnityPushClientConnected.bind(this));
    this.pushNotificationServer.on('unity_disconnected', this.handleUnityPushClientDisconnected.bind(this));
    this.pushNotificationServer.on('connection_established', this.handleConnectionEstablished.bind(this));
    this.pushNotificationServer.on('domain_reload_start', this.handleDomainReloadStart.bind(this));
    this.pushNotificationServer.on('domain_reload_recovered', this.handleDomainReloadRecovered.bind(this));
    this.pushNotificationServer.on('tools_changed', this.handleToolsChanged.bind(this));
  }

  /**
   * Handle Unity push client connection event
   */
  private handleUnityPushClientConnected(event: UnityConnectedEvent): void {
    VibeLogger.logInfo(
      'unity_push_client_connected',
      'Unity Push client connected',
      { clientId: event.clientId, endpoint: event.endpoint },
      undefined,
      'Unity successfully connected to push notification server',
    );
  }

  /**
   * Handle Unity push client disconnection event with state synchronization
   */
  private handleUnityPushClientDisconnected(event: UnityDisconnectedEvent): void {
    VibeLogger.logInfo(
      'unity_push_client_disconnected',
      'Unity Push client disconnected',
      { clientId: event.clientId, reason: event.reason },
      undefined,
      'Unity disconnected from push notification server',
    );

    // Synchronize Unity client disconnection state
    this.unityClient.disconnect();
    
    VibeLogger.logInfo(
      'push_unity_disconnection_synced',
      'Unity disconnection state synchronized',
      {
        clientId: event.clientId,
        reason: event.reason,
        unity_connected: this.unityClient.connected,
      },
      undefined,
      'Unity disconnection state updated via push notification',
    );
  }

  /**
   * Handle connection established event with Unity state synchronization
   */
  private async handleConnectionEstablished(event: PushNotificationEvent): Promise<void> {
    VibeLogger.logInfo('push_connection_established', 'Push connection established', {
      clientId: event.clientId,
      notification: event.notification,
    });

    await this.synchronizeUnityConnectionState(event.clientId);
  }

  /**
   * Synchronize Unity connection state and refresh tools
   */
  private async synchronizeUnityConnectionState(clientId: string): Promise<void> {
    try {
      await this.unityClient.ensureConnected();
      
      // Notify connection manager about Unity connection
      this.connectionManager.handleUnityDiscovered();

      VibeLogger.logInfo(
        'push_unity_connection_synced',
        'Unity connection state synchronized with push notification',
        {
          clientId,
          unity_connected: this.unityClient.connected,
        },
        undefined,
        'Unity connection state updated via push notification',
      );

      // Refresh tools and notify clients about availability
      await this.refreshToolsAndNotifyClients();
    } catch (error) {
      VibeLogger.logError(
        'push_unity_connection_sync_failed',
        'Failed to synchronize Unity connection state',
        {
          clientId,
          error: error instanceof Error ? error.message : String(error),
        },
        undefined,
        'Unity connection synchronization failed',
      );
    }
  }

  /**
   * Handle domain reload start event
   */
  private handleDomainReloadStart(event: PushNotificationEvent): void {
    VibeLogger.logInfo(
      'push_domain_reload_start',
      'Unity domain reload started',
      { clientId: event.clientId, notification: event.notification },
      undefined,
      'Unity is performing domain reload - connection may be temporarily lost',
    );
  }

  /**
   * Handle domain reload recovery with tool refresh
   */
  private handleDomainReloadRecovered(event: PushNotificationEvent): void {
    VibeLogger.logInfo(
      'push_domain_reload_recovered',
      'Unity domain reload recovered',
      { clientId: event.clientId, notification: event.notification },
      undefined,
      'Unity has recovered from domain reload',
    );

    void this.refreshToolsAndNotifyClients();
  }

  /**
   * Handle tools changed notification with refresh
   */
  private handleToolsChanged(event: PushNotificationEvent): void {
    VibeLogger.logInfo('push_tools_changed', 'Unity tools changed notification received', {
      clientId: event.clientId,
      notification: event.notification,
    });

    void this.refreshToolsAndNotifyClients();
  }

  /**
   * Refresh dynamic tools and notify MCP clients
   */
  private async refreshToolsAndNotifyClients(): Promise<void> {
    await this.toolManager.refreshDynamicToolsSafe(() => {
      this.eventHandler.sendToolsChangedNotification();
    });
  }
}