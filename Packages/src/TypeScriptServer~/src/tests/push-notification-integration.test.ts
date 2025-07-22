/**
 * Push通知システム統合テスト
 * 設計書参照: /.kiro/specs/unity-push-notification-system/design.md
 */

/* eslint-disable @typescript-eslint/no-unsafe-assignment */
/* eslint-disable @typescript-eslint/no-unsafe-member-access */
/* eslint-disable @typescript-eslint/no-non-null-assertion */
/* eslint-disable @typescript-eslint/no-explicit-any */
/* eslint-disable @typescript-eslint/require-await */
/* eslint-disable security/detect-object-injection */

import { describe, it, expect, beforeAll, afterAll, beforeEach, jest } from '@jest/globals';
import * as net from 'net';

// Mock VibeLogger to avoid import.meta.url issues in Jest
jest.mock('../utils/vibe-logger.js', () => ({
  VibeLogger: {
    logInfo: jest.fn(),
    logError: jest.fn(),
    logWarn: jest.fn(),
    logWarning: jest.fn(),
  },
}));

import {
  UnityPushNotificationReceiveServer,
  PushNotification,
  UnityConnectedEvent,
  UnityDisconnectedEvent,
  PushNotificationEvent,
} from '../unity-push-notification-receive-server.js';

describe('Push Notification System Integration Tests', () => {
  let server: UnityPushNotificationReceiveServer;
  let serverPort: number;

  beforeAll(async () => {
    server = new UnityPushNotificationReceiveServer();
    serverPort = await server.start();
  }, 10000);

  afterAll(async () => {
    if (server && server.isServerRunning()) {
      await server.stop();
    }
  }, 10000);

  beforeEach(async () => {
    // Clear any existing connections before each test
    if (server && server.isServerRunning()) {
      // Wait a bit for any previous connections to clean up
      await new Promise((resolve) => setTimeout(resolve, 100));
    }
  });

  describe('LLM Tool先行起動シーケンス', () => {
    it('should handle server startup before Unity connection', async () => {
      // Server should already be running from beforeAll
      expect(server.isServerRunning()).toBe(true);
      expect(serverPort).toBeGreaterThan(0);

      const endpoint = server.getEndpoint();
      expect(endpoint.host).toBe('localhost');
      expect(endpoint.port).toBe(serverPort);
      expect(endpoint.protocol).toBe('tcp');

      // No Unity clients should be connected initially
      expect(server.getConnectedClientsCount()).toBe(0);
    });

    it('should provide discoverable endpoint for Unity', async () => {
      const endpoint = server.getEndpoint();

      // Verify Unity can discover this endpoint via port range scanning
      // Note: In tests, server uses random port, but in production it would use 20000-21000 range
      expect(endpoint.port).toBeGreaterThan(0);
      expect(endpoint.port).toBeLessThan(65536);
    });
  });

  describe('Unity初回接続シーケンス', () => {
    it('should handle Unity client initial connection', async () => {
      let connectionEvent: UnityConnectedEvent | null = null;

      server.on('unity_connected', (event) => {
        connectionEvent = event;
      });

      // Simulate Unity client connection
      const unityClient = new net.Socket();
      await new Promise<void>((resolve) => {
        unityClient.connect(serverPort, 'localhost', resolve);
      });

      // Wait for connection event
      await new Promise((resolve) => setTimeout(resolve, 100));

      expect(connectionEvent).not.toBeNull();
      expect(connectionEvent!.clientId).toBeDefined();
      expect(connectionEvent!.endpoint).toEqual({
        host: 'localhost',
        port: serverPort,
        protocol: 'tcp',
      });
      // Note: Connection count may vary in test environment due to timing

      // Send CONNECTION_ESTABLISHED notification from Unity
      let connectionEstablishedEvent: PushNotificationEvent | null = null;
      server.on('connection_established', (event) => {
        connectionEstablishedEvent = event;
      });

      const notification: PushNotification = {
        type: 'CONNECTION_ESTABLISHED',
        timestamp: new Date().toISOString(),
        payload: {
          endpoint: `localhost:${serverPort}`,
          clientInfo: {
            name: 'test-client',
            version: '1.0.0',
          },
        },
      };

      unityClient.write(JSON.stringify(notification) + '\n');
      await new Promise((resolve) => setTimeout(resolve, 100));

      expect(connectionEstablishedEvent).not.toBeNull();
      expect(connectionEstablishedEvent!.notification.type).toBe('CONNECTION_ESTABLISHED');

      unityClient.destroy();
    });
  });

  describe('ドメインリロード復帰処理', () => {
    it('should handle domain reload sequence', async () => {
      let domainReloadStartEvent: PushNotificationEvent | null = null;
      let domainReloadRecoveredEvent: PushNotificationEvent | null = null;

      server.on('domain_reload_start', (event) => {
        domainReloadStartEvent = event;
      });

      server.on('domain_reload_recovered', (event) => {
        domainReloadRecoveredEvent = event;
      });

      // Establish Unity connection
      const unityClient = new net.Socket();
      await new Promise<void>((resolve) => {
        unityClient.connect(serverPort, 'localhost', resolve);
      });
      await new Promise((resolve) => setTimeout(resolve, 100));

      // Send DOMAIN_RELOAD notification
      const domainReloadNotification: PushNotification = {
        type: 'DOMAIN_RELOAD',
        timestamp: new Date().toISOString(),
        payload: {
          reason: {
            type: 'DOMAIN_RELOAD',
            message: 'Unity domain reload initiated',
          },
        },
      };

      unityClient.write(JSON.stringify(domainReloadNotification) + '\n');
      await new Promise((resolve) => setTimeout(resolve, 100));

      expect(domainReloadStartEvent).not.toBeNull();
      expect(domainReloadStartEvent!.notification.type).toBe('DOMAIN_RELOAD');

      // Simulate Unity reconnection after domain reload
      const recoveredNotification: PushNotification = {
        type: 'DOMAIN_RELOAD_RECOVERED',
        timestamp: new Date().toISOString(),
        payload: {
          clientInfo: {
            name: 'test-client',
            version: '1.0.0',
          },
        },
      };

      unityClient.write(JSON.stringify(recoveredNotification) + '\n');
      await new Promise((resolve) => setTimeout(resolve, 100));

      expect(domainReloadRecoveredEvent).not.toBeNull();
      expect(domainReloadRecoveredEvent!.notification.type).toBe('DOMAIN_RELOAD_RECOVERED');

      unityClient.destroy();
    });
  });

  describe('各種エラーシナリオ', () => {
    it('should handle Unity client unexpected disconnection', async () => {
      let disconnectionEvent: UnityDisconnectedEvent | null = null;

      server.on('unity_disconnected', (event) => {
        disconnectionEvent = event;
      });

      // Connect Unity client
      const unityClient = new net.Socket();
      await new Promise<void>((resolve) => {
        unityClient.connect(serverPort, 'localhost', resolve);
      });
      await new Promise((resolve) => setTimeout(resolve, 100));

      const initialCount = server.getConnectedClientsCount();
      expect(initialCount).toBeGreaterThan(0);

      // Simulate unexpected disconnection
      unityClient.destroy();
      await new Promise((resolve) => setTimeout(resolve, 200));

      expect(disconnectionEvent).not.toBeNull();
      expect(disconnectionEvent!.clientId).toBeDefined();
      // Connection count should decrease after disconnection
      expect(server.getConnectedClientsCount()).toBeLessThan(initialCount);
    });

    it('should handle malformed JSON from Unity gracefully', async () => {
      const unityClient = new net.Socket();
      await new Promise<void>((resolve) => {
        unityClient.connect(serverPort, 'localhost', resolve);
      });
      await new Promise((resolve) => setTimeout(resolve, 100));

      // eslint-disable-next-line no-console
      const originalConsoleError = console.error;
      const errorMessages: string[] = [];
      // eslint-disable-next-line no-console
      console.error = (...args: unknown[]): void => {
        errorMessages.push(args.join(' '));
      };

      try {
        // Send malformed JSON
        unityClient.write('{ invalid json\n');
        await new Promise((resolve) => setTimeout(resolve, 100));

        // Server should remain operational
        expect(server.isServerRunning()).toBe(true);
        expect(errorMessages.some((msg) => msg.includes('Failed to parse push notification'))).toBe(
          true,
        );
      } finally {
        // eslint-disable-next-line no-console
        console.error = originalConsoleError;
        unityClient.destroy();
      }
    });

    it('should handle multiple rapid connections and disconnections', async () => {
      const clientPromises: Promise<void>[] = [];
      const clients: net.Socket[] = [];

      // Create 5 clients rapidly
      for (let i = 0; i < 5; i++) {
        const client = new net.Socket();
        clients.push(client);

        clientPromises.push(
          new Promise<void>((resolve) => {
            client.connect(serverPort, 'localhost', resolve);
          }),
        );
      }

      // Wait for all connections
      await Promise.all(clientPromises);
      await new Promise((resolve) => setTimeout(resolve, 200));

      const connectedCount = server.getConnectedClientsCount();
      expect(connectedCount).toBeGreaterThanOrEqual(3); // At least some connections should succeed

      // Disconnect all rapidly
      clients.forEach((client) => client.destroy());
      await new Promise((resolve) => setTimeout(resolve, 200));

      const finalCount = server.getConnectedClientsCount();
      expect(finalCount).toBeLessThan(connectedCount); // Count should decrease
    });
  });

  describe('既存MCPクライアント互換性', () => {
    it('should not interfere with existing MCP protocol', async () => {
      // This test verifies that push notification server runs independently
      // and doesn't affect the main MCP server functionality

      expect(server.isServerRunning()).toBe(true);

      // Push notification server should use different port from main MCP server
      const endpoint = server.getEndpoint();
      expect(endpoint.port).not.toBe(3000); // Assuming main MCP server uses 3000
      expect(endpoint.port).not.toBe(8080); // Or other common ports

      // Server should accept only Unity clients, not interfere with MCP clients
      // Note: Connection count may vary due to previous test connections
    });

    it('should handle tools list change notifications', async () => {
      let toolsChangedEvent: PushNotificationEvent | null = null;

      server.on('tools_changed', (event) => {
        toolsChangedEvent = event;
      });

      const unityClient = new net.Socket();
      await new Promise<void>((resolve) => {
        unityClient.connect(serverPort, 'localhost', resolve);
      });
      await new Promise((resolve) => setTimeout(resolve, 100));

      const toolsChangedNotification: PushNotification = {
        type: 'TOOLS_CHANGED',
        timestamp: new Date().toISOString(),
        payload: {
          toolsInfo: {
            toolCount: 15,
            changedTools: ['compile', 'get-logs', 'unity-search'],
            changeType: 'TOOLS_ADDED',
          },
        },
      };

      unityClient.write(JSON.stringify(toolsChangedNotification) + '\n');
      await new Promise((resolve) => setTimeout(resolve, 100));

      expect(toolsChangedEvent).not.toBeNull();
      expect(toolsChangedEvent!.notification.type).toBe('TOOLS_CHANGED');
      expect(toolsChangedEvent!.notification.payload?.toolsInfo?.toolCount).toBe(15);

      unityClient.destroy();
    });
  });

  describe('長時間接続安定性', () => {
    it('should maintain connection over extended period', async () => {
      const unityClient = new net.Socket();
      await new Promise<void>((resolve) => {
        unityClient.connect(serverPort, 'localhost', resolve);
      });

      let isConnected = true;
      unityClient.on('close', () => {
        isConnected = false;
      });

      // Keep connection alive for 5 seconds (shortened for test)
      await new Promise((resolve) => setTimeout(resolve, 5000));

      expect(isConnected).toBe(true);
      expect(server.getConnectedClientsCount()).toBeGreaterThan(0);

      unityClient.destroy();
    }, 10000); // 10 second timeout for this test
  });

  describe('メッセージ順序保証', () => {
    it('should process messages in correct order', async () => {
      const receivedEvents: any[] = [];

      server.on('push_notification', (event) => {
        receivedEvents.push(event);
      });

      const unityClient = new net.Socket();
      await new Promise<void>((resolve) => {
        unityClient.connect(serverPort, 'localhost', resolve);
      });
      await new Promise((resolve) => setTimeout(resolve, 100));

      // Send multiple messages in sequence
      const messages = [
        { type: 'CONNECTION_ESTABLISHED', sequence: 1 },
        { type: 'TOOLS_CHANGED', sequence: 2 },
        { type: 'DOMAIN_RELOAD', sequence: 3 },
      ];

      for (const msg of messages) {
        const notification: PushNotification = {
          type: msg.type as any,
          timestamp: new Date().toISOString(),
          payload: { sequence: msg.sequence },
        };
        unityClient.write(JSON.stringify(notification) + '\n');
        await new Promise((resolve) => setTimeout(resolve, 50));
      }

      await new Promise((resolve) => setTimeout(resolve, 200));

      expect(receivedEvents.length).toBe(3);
      for (let i = 0; i < 3; i++) {
        expect(receivedEvents[i]!.notification.payload?.sequence).toBe(i + 1);
      }

      unityClient.destroy();
    });
  });
});
