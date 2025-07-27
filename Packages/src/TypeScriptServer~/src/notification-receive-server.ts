import { createServer, IncomingMessage, ServerResponse, Server } from 'http';
import { URL } from 'url';
import { VibeLogger } from './utils/vibe-logger';

interface NotificationPayload {
  type: string;
  timestamp?: string;
  message?: string;
  [key: string]: unknown;
}

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
      } else if (method === 'POST' && pathname === '/api/notification') {
        void this.handleNotification(req, res);
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

  private async handleNotification(req: IncomingMessage, res: ServerResponse): Promise<void> {
    try {
      const body = await this.parseRequestBody(req);
      const notificationType = body.type;

      if (notificationType === 'domain_reload_complete') {
        this.handleDomainReloadNotification(req, res);
        return;
      }

      if (notificationType === 'server_restart_complete') {
        this.handleServerRestartNotification(req, res);
        return;
      }

      // Handle other notification types
      res.statusCode = 400;
      res.setHeader('Content-Type', 'application/json');
      res.end(JSON.stringify({ success: false, error: 'Unknown notification type' }));
    } catch (error) {
      VibeLogger.logError('notification_handler_error', 'Failed to handle notification', {
        error: error instanceof Error ? error.message : String(error),
      });
      res.statusCode = 500;
      res.setHeader('Content-Type', 'application/json');
      res.end(JSON.stringify({ success: false, error: 'Internal server error' }));
    }
  }

  private async parseRequestBody(req: IncomingMessage): Promise<NotificationPayload> {
    return new Promise((resolve, reject) => {
      let body = '';
      req.on('data', (chunk: Buffer) => {
        body += chunk.toString();
      });
      req.on('end', () => {
        try {
          const parsed = JSON.parse(body) as NotificationPayload;
          resolve(parsed);
        } catch (error) {
          reject(error);
        }
      });
      req.on('error', (error) => {
        reject(error);
      });
    });
  }

  private handleDomainReloadNotification(req: IncomingMessage, res: ServerResponse): void {
    VibeLogger.logInfo(
      'domain_reload_notification_received',
      'Received domain reload notification from Unity',
      { timestamp: new Date().toISOString() },
    );

    // Respond immediately
    res.statusCode = 200;
    res.setHeader('Content-Type', 'application/json');
    res.end(
      JSON.stringify({
        success: true,
        timestamp: new Date().toISOString(),
        message: 'Domain reload notification received',
      }),
    );

    // Trigger existing domain reload handler
    if (this.onDomainReloadComplete) {
      this.onDomainReloadComplete();
    }
  }

  private handleServerRestartNotification(req: IncomingMessage, res: ServerResponse): void {
    VibeLogger.logInfo(
      'server_restart_notification_received',
      'Received server restart notification from Unity',
      {
        timestamp: new Date().toISOString(),
        port: this.port,
      },
    );

    // Respond immediately
    res.statusCode = 200;
    res.setHeader('Content-Type', 'application/json');
    res.end(
      JSON.stringify({
        success: true,
        timestamp: new Date().toISOString(),
        message: 'Server restart notification received',
      }),
    );

    // Trigger reconnection process (same as domain reload)
    this.triggerReconnection();
  }

  private triggerReconnection(): void {
    // Use existing domain reload recovery mechanism
    VibeLogger.logInfo(
      'server_restart_reconnection_triggered',
      'Triggering reconnection after server restart',
      { timestamp: new Date().toISOString() },
    );

    // Same logic as domain reload - send setClientName to restore connection immediately
    if (this.onDomainReloadComplete) {
      this.onDomainReloadComplete();
    }
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
