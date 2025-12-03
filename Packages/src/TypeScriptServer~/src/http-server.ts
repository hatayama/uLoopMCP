/**
 * HTTP Server for MCP Streamable HTTP Transport
 *
 * This module provides an HTTP server using Node.js built-in http module
 * that handles MCP protocol communication using the Streamable HTTP transport.
 *
 * Related classes:
 * - StreamableHTTPServerTransport: MCP SDK transport for HTTP
 * - UnityMcpServer: Main server class that uses this HTTP server
 *
 * References:
 * - https://github.com/modelcontextprotocol/typescript-sdk
 * - https://modelcontextprotocol.io/specification/2025-03-26/basic/transports
 */

import { createServer, Server as HttpServer, IncomingMessage, ServerResponse } from 'http';
import { StreamableHTTPServerTransport } from '@modelcontextprotocol/sdk/server/streamableHttp.js';
import { isInitializeRequest } from '@modelcontextprotocol/sdk/types.js';
import { randomUUID } from 'crypto';
import { VibeLogger } from './utils/vibe-logger.js';

/**
 * Configuration options for McpHttpServer
 */
export interface McpHttpServerConfig {
  /** HTTP port to listen on */
  port: number;
  /** Whether to enable session management (default: true) */
  enableSessions?: boolean;
}

/**
 * HTTP Server wrapper for MCP Streamable HTTP transport
 *
 * Handles:
 * - POST /mcp - Client-to-server JSON-RPC messages
 * - GET /mcp - Server-to-client SSE notifications
 * - DELETE /mcp - Session termination
 */
export class McpHttpServer {
  private httpServer: HttpServer | null = null;
  private transports: Map<string, StreamableHTTPServerTransport> = new Map();
  private readonly config: Required<McpHttpServerConfig>;
  private onTransportCreated: ((transport: StreamableHTTPServerTransport) => Promise<void>) | null =
    null;

  constructor(config: McpHttpServerConfig) {
    this.config = {
      port: config.port,
      enableSessions: config.enableSessions ?? true,
    };
  }

  /**
   * Set callback for when a new transport is created
   * This allows the main server to connect to each transport
   */
  setTransportCreatedCallback(
    callback: (transport: StreamableHTTPServerTransport) => Promise<void>,
  ): void {
    this.onTransportCreated = callback;
  }

  /**
   * Parse JSON body from request with size limit to prevent memory exhaustion
   * @param req - The incoming HTTP request
   * @returns Parsed JSON body
   */
  private async parseJsonBody(req: IncomingMessage): Promise<unknown> {
    const MAX_BODY_SIZE = 1024 * 1024; // 1MB limit

    return new Promise((resolve, reject) => {
      let body = '';
      req.on('data', (chunk: Buffer) => {
        if (body.length + chunk.length > MAX_BODY_SIZE) {
          req.destroy();
          reject(new Error('Request body too large'));
          return;
        }
        body += chunk.toString();
      });
      req.on('end', () => {
        if (!body) {
          resolve({});
          return;
        }
        try {
          resolve(JSON.parse(body));
        } catch {
          reject(new Error('Invalid JSON'));
        }
      });
      req.on('error', reject);
    });
  }

  /**
   * Send JSON response
   */
  private sendJson(res: ServerResponse, statusCode: number, data: unknown): void {
    res.writeHead(statusCode, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify(data));
  }

  /**
   * Send JSON-RPC error response
   */
  private sendJsonRpcError(
    res: ServerResponse,
    statusCode: number,
    code: number,
    message: string,
  ): void {
    this.sendJson(res, statusCode, {
      jsonrpc: '2.0',
      error: { code, message },
      id: null,
    });
  }

  /**
   * Handle incoming HTTP requests
   */
  private async handleRequest(req: IncomingMessage, res: ServerResponse): Promise<void> {
    const url = req.url ?? '/';
    const method = req.method ?? 'GET';

    VibeLogger.logInfo('mcp_http_request', 'Incoming HTTP request', {
      method,
      url,
      session_id: req.headers['mcp-session-id'],
      accept: req.headers['accept'],
    });

    // Health check endpoint
    if (url === '/health' && method === 'GET') {
      this.sendJson(res, 200, { status: 'ok', sessions: this.transports.size });
      return;
    }

    // MCP endpoint
    if (url === '/mcp') {
      switch (method) {
        case 'POST':
          await this.handlePost(req, res);
          break;
        case 'GET':
          await this.handleGet(req, res);
          break;
        case 'DELETE':
          await this.handleDelete(req, res);
          break;
        default:
          this.sendJsonRpcError(res, 405, -32600, 'Method not allowed');
      }
      return;
    }

    // 404 for other paths
    this.sendJsonRpcError(res, 404, -32600, 'Not found');
  }

  /**
   * Handle POST requests - client-to-server messages
   */
  private async handlePost(req: IncomingMessage, res: ServerResponse): Promise<void> {
    const sessionId = req.headers['mcp-session-id'] as string | undefined;

    try {
      const body = await this.parseJsonBody(req);
      let transport: StreamableHTTPServerTransport;

      const existingTransport = sessionId ? this.transports.get(sessionId) : undefined;
      if (existingTransport) {
        // Reuse existing transport for this session
        transport = existingTransport;
      } else if (!sessionId && isInitializeRequest(body)) {
        // New initialization request - create new transport
        transport = new StreamableHTTPServerTransport({
          sessionIdGenerator: this.config.enableSessions ? (): string => randomUUID() : undefined,
        });

        // Connect the server to this transport
        if (this.onTransportCreated) {
          await this.onTransportCreated(transport);
        }

        // Store transport if sessions are enabled
        if (this.config.enableSessions) {
          transport.onclose = (): void => {
            // Cleanup session when transport closes
            for (const [id, t] of this.transports.entries()) {
              if (t === transport) {
                this.transports.delete(id);
                VibeLogger.logInfo('mcp_session_closed', 'MCP session closed', {
                  session_id: id,
                });
                break;
              }
            }
          };
        }

        VibeLogger.logInfo('mcp_transport_created', 'New MCP transport created', {
          has_session: this.config.enableSessions,
        });
      } else if (sessionId && !this.transports.has(sessionId)) {
        // Invalid session ID
        this.sendJsonRpcError(res, 400, -32000, 'Bad Request: Invalid session ID');
        return;
      } else {
        // No session ID and not an initialize request
        this.sendJsonRpcError(
          res,
          400,
          -32000,
          'Bad Request: No valid session. Send an initialize request first.',
        );
        return;
      }

      // Handle the request through the transport
      await transport.handleRequest(req, res, body);

      // Store session ID after successful initialization
      if (!sessionId && isInitializeRequest(body) && this.config.enableSessions) {
        const newSessionId = res.getHeader('mcp-session-id') as string;
        if (newSessionId) {
          this.transports.set(newSessionId, transport);
          VibeLogger.logInfo('mcp_session_created', 'New MCP session created', {
            session_id: newSessionId,
          });
        }
      }
    } catch (error) {
      VibeLogger.logError('mcp_http_post_error', 'Error handling POST request', {
        error_message: error instanceof Error ? error.message : String(error),
        session_id: sessionId,
      });

      if (!res.writableEnded) {
        this.sendJsonRpcError(res, 500, -32603, 'Internal server error');
      }
    }
  }

  /**
   * Handle GET requests - SSE stream for server-to-client notifications
   */
  private async handleGet(req: IncomingMessage, res: ServerResponse): Promise<void> {
    const sessionId = req.headers['mcp-session-id'] as string | undefined;

    const transport = sessionId ? this.transports.get(sessionId) : undefined;
    if (!transport) {
      this.sendJsonRpcError(res, 400, -32000, 'Bad Request: Mcp-Session-Id header is required');
      return;
    }

    try {
      await transport.handleRequest(req, res);
    } catch (error) {
      VibeLogger.logError('mcp_http_get_error', 'Error handling GET request', {
        error_message: error instanceof Error ? error.message : String(error),
        session_id: sessionId,
      });
    }
  }

  /**
   * Handle DELETE requests - session termination
   */
  private async handleDelete(req: IncomingMessage, res: ServerResponse): Promise<void> {
    const sessionId = req.headers['mcp-session-id'] as string | undefined;

    const transport = sessionId ? this.transports.get(sessionId) : undefined;
    if (!transport) {
      this.sendJsonRpcError(res, 400, -32000, 'Bad Request: Mcp-Session-Id header is required');
      return;
    }

    try {
      await transport.handleRequest(req, res);
      if (sessionId) {
        this.transports.delete(sessionId);
        VibeLogger.logInfo('mcp_session_deleted', 'MCP session deleted', {
          session_id: sessionId,
        });
      }
    } catch (error) {
      VibeLogger.logError('mcp_http_delete_error', 'Error handling DELETE request', {
        error_message: error instanceof Error ? error.message : String(error),
        session_id: sessionId,
      });
    }
  }

  /**
   * Start the HTTP server
   */
  async start(): Promise<void> {
    return new Promise((resolve, reject) => {
      try {
        this.httpServer = createServer((req, res) => {
          void this.handleRequest(req, res);
        });

        this.httpServer.on('error', (error: NodeJS.ErrnoException) => {
          if (error.code === 'EADDRINUSE') {
            VibeLogger.logError('mcp_http_port_in_use', `HTTP port ${this.config.port} is in use`, {
              port: this.config.port,
            });
            reject(new Error(`Port ${this.config.port} is already in use`));
          } else {
            VibeLogger.logError('mcp_http_server_error', 'HTTP server error', {
              error_message: error.message,
            });
            reject(error);
          }
        });

        this.httpServer.listen(this.config.port, () => {
          VibeLogger.logInfo('mcp_http_server_started', 'MCP HTTP server started', {
            port: this.config.port,
            sessions_enabled: this.config.enableSessions,
          });
          resolve();
        });
      } catch (error) {
        reject(error instanceof Error ? error : new Error(String(error)));
      }
    });
  }

  /**
   * Stop the HTTP server
   */
  async stop(): Promise<void> {
    // Close all transports
    for (const [sessionId, transport] of this.transports.entries()) {
      try {
        await transport.close();
        VibeLogger.logInfo('mcp_transport_closed', 'Transport closed', {
          session_id: sessionId,
        });
      } catch (error) {
        VibeLogger.logWarning('mcp_transport_close_error', 'Error closing transport', {
          session_id: sessionId,
          error_message: error instanceof Error ? error.message : String(error),
        });
      }
    }
    this.transports.clear();

    // Close HTTP server
    const server = this.httpServer;
    if (server) {
      return new Promise((resolve) => {
        server.close(() => {
          VibeLogger.logInfo('mcp_http_server_stopped', 'MCP HTTP server stopped');
          this.httpServer = null;
          resolve();
        });
      });
    }
  }

  /**
   * Get the number of active sessions
   */
  getSessionCount(): number {
    return this.transports.size;
  }

  /**
   * Check if server is running
   */
  isRunning(): boolean {
    return this.httpServer !== null && this.httpServer.listening;
  }
}
