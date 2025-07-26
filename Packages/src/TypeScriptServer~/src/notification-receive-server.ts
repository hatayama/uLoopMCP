import { createServer, IncomingMessage, ServerResponse, Server } from 'http';
import { URL } from 'url';
import { VibeLogger } from './utils/vibe-logger';

/**
 * Unity側からのdomain reload完了通知を受信するHTTPサーバー
 * 固定ポートではなく動的ポートを使用してポート競合を回避
 * Node.js標準のhttpモジュールを使用して軽量化
 */
export class NotificationReceiveServer {
  private server: Server | null = null;
  private port: number = 0; // OSが動的に割り当て
  private onDomainReloadComplete?: () => void;

  constructor() {
    this.server = createServer((req, res) => {
      this.handleRequest(req, res);
    });
  }

  private handleRequest(req: IncomingMessage, res: ServerResponse): void {
    const method = req.method;
    const url = req.url;

    // CORS対応
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type');

    if (method === 'OPTIONS') {
      res.statusCode = 200;
      res.end();
      return;
    }

    try {
      const parsedUrl = new URL(url || '', `http://localhost:${this.port}`);
      const pathname = parsedUrl.pathname;

      if (method === 'POST' && pathname === '/domain-reload-complete') {
        this.handleDomainReloadComplete(req, res);
      } else if (method === 'GET' && pathname === '/health') {
        this.handleHealthCheck(res);
      } else {
        res.statusCode = 404;
        res.setHeader('Content-Type', 'application/json');
        res.end(JSON.stringify({ error: 'Not Found' }));
      }
    } catch (error) {
      res.statusCode = 500;
      res.setHeader('Content-Type', 'application/json');
      res.end(JSON.stringify({ error: 'Internal Server Error' }));
    }
  }

  private handleDomainReloadComplete(req: IncomingMessage, res: ServerResponse): void {
    VibeLogger.logInfo(
      'domain_reload_notification_received',
      'Received domain reload complete notification from Unity',
      { timestamp: new Date().toISOString() },
    );

    if (this.onDomainReloadComplete) {
      this.onDomainReloadComplete();
    }

    res.statusCode = 200;
    res.setHeader('Content-Type', 'application/json');
    res.end(JSON.stringify({ status: 'received' }));
  }

  private handleHealthCheck(res: ServerResponse): void {
    res.statusCode = 200;
    res.setHeader('Content-Type', 'application/json');
    res.end(
      JSON.stringify({
        status: 'ok',
        server: 'notification-server',
        port: this.port,
      }),
    );
  }

  public async start(): Promise<number> {
    return new Promise((resolve, reject) => {
      if (!this.server) {
        reject(new Error('Server not initialized'));
        return;
      }

      this.server.listen(0, (error?: Error) => {
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
            resolve(this.port);
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
