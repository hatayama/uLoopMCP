/**
 * TypeScript Push通知受信サーバー
 * 設計書参照: /.kiro/specs/unity-push-notification-system/design.md
 * 関連クラス: UnityPushClient.cs, McpSessionManager.cs
 *
 * 責任:
 * - Unity Editorからのpush通知を受信
 * - ランダムポートでTCPサーバーを起動
 * - Unity接続状態の管理
 * - 切断理由の処理
 */

import * as net from 'net';
import { EventEmitter } from 'events';
import { VibeLogger } from './utils/vibe-logger.js';
import { ClientInfo } from './types/push-notification-types.js';

export interface PushNotification {
  type:
    | 'CONNECTION_ESTABLISHED'
    | 'DOMAIN_RELOAD'
    | 'DOMAIN_RELOAD_RECOVERED'
    | 'USER_DISCONNECT'
    | 'UNITY_SHUTDOWN'
    | 'TOOLS_CHANGED';
  timestamp: string;
  payload?: {
    reason?: DisconnectReason;
    endpoint?: string;
    clientInfo?: ClientInfo;
    toolsInfo?: ToolsInfo;
    sequence?: number;
  };
}

export interface DisconnectReason {
  type: 'USER_DISCONNECT' | 'UNITY_SHUTDOWN' | 'DOMAIN_RELOAD';
  message: string;
}

export interface ToolsInfo {
  toolCount: number;
  changedTools: string[];
  changeType: 'TOOLS_ADDED' | 'TOOLS_REMOVED' | 'TOOLS_MODIFIED';
}

export interface ServerEndpoint {
  host: string;
  port: number;
  protocol: 'tcp';
}

export interface UnityConnection {
  socket: net.Socket;
  clientId: string;
  connectedAt: Date;
  lastPingAt?: Date;
}

export interface UnityConnectedEvent {
  clientId: string;
  endpoint: ServerEndpoint;
}

export interface UnityDisconnectedEvent {
  clientId: string;
  reason: DisconnectReason;
}

export interface PushNotificationEvent {
  clientId: string;
  notification: PushNotification;
}

export class UnityPushNotificationReceiveServer extends EventEmitter {
  private server: net.Server | null = null;
  private connectedUnityClients: Map<string, UnityConnection> = new Map();
  private port: number = 0;
  private isRunning: boolean = false;

  constructor() {
    super();
  }

  public async start(): Promise<number> {
    if (this.isRunning) {
      return this.port;
    }

    return new Promise((resolve, reject) => {
      this.server = net.createServer((socket) => {
        this.handleUnityConnection(socket);
      });

      this.server.on('error', (error) => {
        reject(error);
      });

      this.server.listen(0, '127.0.0.1', () => {
        if (!this.server) {
          return;
        }

        const address = this.server.address();
        if (typeof address === 'object' && address !== null) {
          this.port = address.port;
          this.isRunning = true;

          // Log push notification server startup with endpoint information
          VibeLogger.logInfo(
            'push_notification_server_started',
            'Push notification receive server started',
            {
              server_endpoint: `127.0.0.1:${this.port}`,
              host: '127.0.0.1',
              port: this.port,
              server_address: address,
              process_id: process.pid,
            },
            undefined,
            'Push notification server endpoint - Unity will connect to this for sending notifications',
            'Track this server endpoint against Unity side push client connection logs',
          );

          resolve(this.port);
        } else {
          reject(new Error('Failed to get server address'));
        }
      });
    });
  }

  public async stop(): Promise<void> {
    if (!this.isRunning || !this.server) {
      return;
    }

    return new Promise((resolve) => {
      // Close all client sockets before clearing the map
      for (const connection of this.connectedUnityClients.values()) {
        if (!connection.socket.destroyed) {
          connection.socket.destroy();
        }
      }

      // Clear the connections map after closing all sockets
      this.connectedUnityClients.clear();

      // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
      this.server!.close(() => {
        this.isRunning = false;
        this.port = 0;
        resolve();
      });
    });
  }

  public getEndpoint(): ServerEndpoint {
    if (!this.isRunning) {
      throw new Error('Server is not running');
    }

    return {
      host: '127.0.0.1',
      port: this.port,
      protocol: 'tcp',
    };
  }

  public isServerRunning(): boolean {
    return this.isRunning;
  }

  public getConnectedClientsCount(): number {
    return this.connectedUnityClients.size;
  }

  public getCurrentPort(): number | null {
    return this.isRunning ? this.port : null;
  }

  private handleUnityConnection(socket: net.Socket): void {
    const clientId = this.generateClientId();
    const connection: UnityConnection = {
      socket,
      clientId,
      connectedAt: new Date(),
    };

    this.connectedUnityClients.set(clientId, connection);

    // Log Unity push client connection with endpoint information
    VibeLogger.logInfo(
      'unity_push_client_connected',
      'Unity push client connected to notification receive server',
      {
        client_id: clientId,
        server_endpoint: `127.0.0.1:${this.port}`,
        client_remote_address: socket.remoteAddress,
        client_remote_port: socket.remotePort,
        client_local_address: socket.localAddress,
        client_local_port: socket.localPort,
        connected_at: connection.connectedAt.toISOString(),
        process_id: process.pid,
      },
      undefined,
      'Unity push client connected - this is the endpoint Unity uses for push notifications',
      'Compare this endpoint with Unity side UnityPushClient connection logs',
    );

    socket.setEncoding('utf8');
    socket.setTimeout(30000); // 30秒タイムアウト

    socket.on('data', (data) => {
      this.handleIncomingData(clientId, data.toString());
    });

    socket.on('close', () => {
      this.handleDisconnection(clientId, {
        type: 'USER_DISCONNECT',
        message: 'Socket closed by client',
      });
    });

    socket.on('error', (error) => {
      VibeLogger.logError('unity_client_error', `Unity client ${clientId} error`, {
        clientId,
        error: error.message,
      });
      this.handleDisconnection(clientId, {
        type: 'USER_DISCONNECT',
        message: `Socket error: ${error.message}`,
      });
    });

    socket.on('timeout', () => {
      VibeLogger.logWarning('unity_client_timeout', `Unity client ${clientId} timed out`, {
        clientId,
      });
      socket.destroy();
    });

    this.emit('unity_connected', { clientId, endpoint: this.getEndpoint() });
  }

  private handleIncomingData(clientId: string, data: string): void {
    const connection = this.connectedUnityClients.get(clientId);
    if (!connection) {
      return;
    }

    const lines = data.split('\n');

    for (const line of lines) {
      if (!line.trim()) {
        continue;
      }

      this.processMessage(clientId, line.trim());
    }
  }

  private processMessage(clientId: string, message: string): void {
    let notification: PushNotification;

    try {
      notification = JSON.parse(message) as PushNotification;
    } catch (error) {
      VibeLogger.logError(
        'push_notification_parse_error',
        `Failed to parse push notification from ${clientId}`,
        { clientId, error: error instanceof Error ? error.message : String(error) },
      );
      return;
    }

    this.handlePushNotification(clientId, notification);
  }

  private handlePushNotification(clientId: string, notification: PushNotification): void {
    const connection = this.connectedUnityClients.get(clientId);
    if (!connection) {
      return;
    }

    switch (notification.type) {
      case 'CONNECTION_ESTABLISHED':
        this.handleConnectionEstablished(clientId, notification);
        break;
      case 'DOMAIN_RELOAD':
        this.handleDomainReload(clientId, notification);
        break;
      case 'DOMAIN_RELOAD_RECOVERED':
        this.handleDomainReloadRecovered(clientId, notification);
        break;
      case 'USER_DISCONNECT':
      case 'UNITY_SHUTDOWN':
        this.handleDisconnectNotification(clientId, notification);
        break;
      case 'TOOLS_CHANGED':
        this.handleToolsChanged(clientId, notification);
        break;
      default:
        VibeLogger.logWarning(
          'unknown_notification_type',
          `Unknown notification type: ${String(notification.type)}`,
          { clientId, notificationType: notification.type },
        );
    }

    this.emit('push_notification', { clientId, notification });
  }

  private handleConnectionEstablished(clientId: string, notification: PushNotification): void {
    const connection = this.connectedUnityClients.get(clientId);
    if (connection) {
      connection.lastPingAt = new Date();
    }

    this.emit('connection_established', { clientId, notification });
  }

  private handleDomainReload(clientId: string, notification: PushNotification): void {
    this.emit('domain_reload_start', { clientId, notification });
  }

  private handleDomainReloadRecovered(clientId: string, notification: PushNotification): void {
    const connection = this.connectedUnityClients.get(clientId);
    if (connection) {
      connection.lastPingAt = new Date();
    }

    this.emit('domain_reload_recovered', { clientId, notification });
  }

  private handleDisconnectNotification(clientId: string, notification: PushNotification): void {
    const reason = notification.payload?.reason || {
      type: 'USER_DISCONNECT',
      message: 'Unknown disconnect reason',
    };

    this.handleDisconnection(clientId, reason);
  }

  private handleToolsChanged(clientId: string, notification: PushNotification): void {
    this.emit('tools_changed', { clientId, notification });
  }

  private handleDisconnection(clientId: string, reason: DisconnectReason): void {
    const connection = this.connectedUnityClients.get(clientId);
    if (!connection) {
      return;
    }

    // Log Unity push client disconnection with endpoint information
    VibeLogger.logInfo(
      'unity_push_client_disconnected',
      'Unity push client disconnected from notification receive server',
      {
        client_id: clientId,
        server_endpoint: `127.0.0.1:${this.port}`,
        client_remote_address: connection.socket.remoteAddress,
        client_remote_port: connection.socket.remotePort,
        client_local_address: connection.socket.localAddress,
        client_local_port: connection.socket.localPort,
        connected_at: connection.connectedAt.toISOString(),
        disconnect_reason: reason,
        process_id: process.pid,
      },
      undefined,
      'Unity push client disconnected - compare this endpoint with Unity side logs',
      'Check if Unity side detected this same endpoint during disconnect',
    );

    this.connectedUnityClients.delete(clientId);

    if (!connection.socket.destroyed) {
      connection.socket.destroy();
    }

    this.emit('unity_disconnected', { clientId, reason });
  }

  private generateClientId(): string {
    return `unity_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
  }
}
