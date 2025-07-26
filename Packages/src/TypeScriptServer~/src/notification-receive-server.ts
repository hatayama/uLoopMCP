import express from 'express';
import { Server } from 'http';
import { VibeLogger } from './utils/vibe-logger';

/**
 * Unity側からのdomain reload完了通知を受信するHTTPサーバー
 * 固定ポートではなく動的ポートを使用してポート競合を回避
 */
export class NotificationReceiveServer {
  private app: express.Application;
  private server: Server | null = null;
  private port: number = 0; // OSが動的に割り当て
  private onDomainReloadComplete?: () => void;

  constructor() {
    this.app = express();
    this.app.use(express.json());
    this.setupRoutes();
  }

  private setupRoutes(): void {
    // Domain reload完了通知
    this.app.post('/domain-reload-complete', (req, res) => {
      VibeLogger.logInfo(
        'domain_reload_notification_received',
        'Received domain reload complete notification from Unity',
        { timestamp: new Date().toISOString() },
      );

      if (this.onDomainReloadComplete) {
        this.onDomainReloadComplete();
      }

      res.status(200).json({ status: 'received' });
    });

    // ヘルスチェック
    this.app.get('/health', (req, res) => {
      res.status(200).json({
        status: 'ok',
        server: 'notification-server',
        port: this.port,
      });
    });
  }

  public async start(): Promise<number> {
    return new Promise((resolve, reject) => {
      this.server = this.app.listen(0, (error?: Error) => {
        // ポート0で動的割り当て
        if (error) {
          reject(error);
        } else {
          const address = this.server?.address();
          if (address && typeof address === 'object' && 'port' in address) {
            this.port = address.port;
            VibeLogger.logInfo(
              'notification_receive_server_started',
              `Notification receive server started on port ${this.port}`,
              { port: this.port },
            );
            resolve(this.port); // ポート番号を返す
          } else {
            reject(new Error('Failed to get server port'));
          }
        }
      });
    });
  }

  public getPort(): number {
    return this.port;
  }

  public stop(): void {
    if (this.server) {
      this.server.close();
      VibeLogger.logInfo('notification_server_stopped', 'Notification server stopped', {
        port: this.port,
      });
      this.server = null;
    }
  }

  public setDomainReloadHandler(handler: () => void): void {
    this.onDomainReloadComplete = handler;
  }
}
