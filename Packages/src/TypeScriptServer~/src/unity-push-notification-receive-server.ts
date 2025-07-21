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

export interface PushNotification {
  type: 'CONNECTION_ESTABLISHED' | 'DOMAIN_RELOAD' | 'DOMAIN_RELOAD_RECOVERED' | 
        'USER_DISCONNECT' | 'UNITY_SHUTDOWN' | 'TOOLS_CHANGED';
  timestamp: string;
  payload?: {
    reason?: DisconnectReason;
    endpoint?: string;
    clientInfo?: ClientInfo;
  };
}

export interface DisconnectReason {
  type: 'USER_DISCONNECT' | 'UNITY_SHUTDOWN' | 'DOMAIN_RELOAD';
  message: string;
}

export interface ClientInfo {
  unityVersion?: string;
  projectPath?: string;
  sessionId?: string;
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

      this.server.listen(0, 'localhost', () => {
        if (!this.server) return;
        
        const address = this.server.address();
        if (typeof address === 'object' && address !== null) {
          this.port = address.port;
          this.isRunning = true;
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
      this.connectedUnityClients.clear();
      
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
      host: 'localhost',
      port: this.port,
      protocol: 'tcp'
    };
  }

  public isServerRunning(): boolean {
    return this.isRunning;
  }

  public getConnectedClientsCount(): number {
    return this.connectedUnityClients.size;
  }

  private handleUnityConnection(socket: net.Socket): void {
    const clientId = this.generateClientId();
    const connection: UnityConnection = {
      socket,
      clientId,
      connectedAt: new Date()
    };

    this.connectedUnityClients.set(clientId, connection);

    socket.setEncoding('utf8');
    socket.setTimeout(30000); // 30秒タイムアウト

    socket.on('data', (data) => {
      this.handleIncomingData(clientId, data.toString());
    });

    socket.on('close', () => {
      this.handleDisconnection(clientId, { type: 'USER_DISCONNECT', message: 'Socket closed by client' });
    });

    socket.on('error', (error) => {
      console.error(`Unity client ${clientId} error:`, error);
      this.handleDisconnection(clientId, { type: 'USER_DISCONNECT', message: `Socket error: ${error.message}` });
    });

    socket.on('timeout', () => {
      console.warn(`Unity client ${clientId} timed out`);
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
      if (!line.trim()) continue;
      
      this.processMessage(clientId, line.trim());
    }
  }

  private processMessage(clientId: string, message: string): void {
    let notification: PushNotification;
    
    try {
      notification = JSON.parse(message) as PushNotification;
    } catch (error) {
      console.error(`Failed to parse push notification from ${clientId}:`, error);
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
        console.warn(`Unknown notification type: ${notification.type}`);
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
      message: 'Unknown disconnect reason' 
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
