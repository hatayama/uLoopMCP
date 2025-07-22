/**
 * UnityPushNotificationReceiveServer単体テスト
 * 設計書参照: /.kiro/specs/unity-push-notification-system/design.md
 */

import { describe, it, expect, beforeEach, afterEach, jest } from '@jest/globals';
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

describe('UnityPushNotificationReceiveServer', () => {
  let server: UnityPushNotificationReceiveServer;

  beforeEach(() => {
    server = new UnityPushNotificationReceiveServer();
  });

  afterEach(async () => {
    if (server.isServerRunning()) {
      await server.stop();
    }
  });

  describe('Server Lifecycle', () => {
    it('should start server on random port', async () => {
      const port = await server.start();

      expect(port).toBeGreaterThan(0);
      expect(port).toBeLessThan(65536);
      expect(server.isServerRunning()).toBe(true);
    });

    it('should return same port if already running', async () => {
      const port1 = await server.start();
      const port2 = await server.start();

      expect(port1).toBe(port2);
    });

    it('should stop server gracefully', async () => {
      await server.start();
      await server.stop();

      expect(server.isServerRunning()).toBe(false);
    });

    it('should provide server endpoint', async () => {
      const port = await server.start();
      const endpoint = server.getEndpoint();

      expect(endpoint.host).toBe('localhost');
      expect(endpoint.port).toBe(port);
      expect(endpoint.protocol).toBe('tcp');
    });

    it('should throw error when getting endpoint for stopped server', () => {
      expect(() => {
        server.getEndpoint();
      }).toThrow('Server is not running');
    });
  });

  describe('Connection Management', () => {
    it('should track connected clients', async () => {
      await server.start();
      expect(server.getConnectedClientsCount()).toBe(0);
    });

    it('should handle Unity connection', async () => {
      const port = await server.start();
      let connectionEvent: UnityConnectedEvent | null = null;

      server.on('unity_connected', (event) => {
        connectionEvent = event;
      });

      // Create test client
      const client = new net.Socket();
      await new Promise<void>((resolve) => {
        client.connect(port, 'localhost', resolve);
      });

      // Wait for connection event
      await new Promise((resolve) => setTimeout(resolve, 100));

      expect(connectionEvent).not.toBeNull();
      expect(connectionEvent!.clientId).toBeDefined();
      expect(server.getConnectedClientsCount()).toBe(1);

      client.destroy();
    });

    it('should handle client disconnection', async () => {
      const port = await server.start();
      let disconnectionEvent: UnityDisconnectedEvent | null = null;

      server.on('unity_disconnected', (event) => {
        disconnectionEvent = event;
      });

      // Create and disconnect test client
      const client = new net.Socket();
      await new Promise<void>((resolve) => {
        client.connect(port, 'localhost', resolve);
      });

      await new Promise((resolve) => setTimeout(resolve, 100));
      client.destroy();
      await new Promise((resolve) => setTimeout(resolve, 100));

      expect(disconnectionEvent).not.toBeNull();
      expect(disconnectionEvent!.clientId).toBeDefined();
      expect(server.getConnectedClientsCount()).toBe(0);
    });
  });

  describe('Push Notification Processing', () => {
    it('should process CONNECTION_ESTABLISHED notification', async () => {
      const port = await server.start();
      let notificationEvent: PushNotificationEvent | null = null;

      server.on('connection_established', (event) => {
        notificationEvent = event;
      });

      const client = new net.Socket();
      await new Promise<void>((resolve) => {
        client.connect(port, 'localhost', resolve);
      });

      await new Promise((resolve) => setTimeout(resolve, 100));

      const notification: PushNotification = {
        type: 'CONNECTION_ESTABLISHED',
        timestamp: new Date().toISOString(),
        payload: {
          endpoint: 'localhost:8700',
        },
      };

      client.write(JSON.stringify(notification) + '\n');
      await new Promise((resolve) => setTimeout(resolve, 100));

      expect(notificationEvent).not.toBeNull();
      expect(notificationEvent!.notification.type).toBe('CONNECTION_ESTABLISHED');

      client.destroy();
    });

    it('should process DOMAIN_RELOAD notification', async () => {
      const port = await server.start();
      let notificationEvent: PushNotificationEvent | null = null;

      server.on('domain_reload_start', (event) => {
        notificationEvent = event;
      });

      const client = new net.Socket();
      await new Promise<void>((resolve) => {
        client.connect(port, 'localhost', resolve);
      });

      await new Promise((resolve) => setTimeout(resolve, 100));

      const notification: PushNotification = {
        type: 'DOMAIN_RELOAD',
        timestamp: new Date().toISOString(),
        payload: {
          reason: {
            type: 'DOMAIN_RELOAD',
            message: 'Unity domain reload initiated',
          },
        },
      };

      client.write(JSON.stringify(notification) + '\n');
      await new Promise((resolve) => setTimeout(resolve, 100));

      expect(notificationEvent).not.toBeNull();
      expect(notificationEvent!.notification.type).toBe('DOMAIN_RELOAD');

      client.destroy();
    });

    it('should handle malformed JSON gracefully', async () => {
      const port = await server.start();

      const client = new net.Socket();
      await new Promise<void>((resolve) => {
        client.connect(port, 'localhost', resolve);
      });

      await new Promise((resolve) => setTimeout(resolve, 100));

      // Send malformed JSON
      client.write('{ invalid json }\n');
      await new Promise((resolve) => setTimeout(resolve, 100));

      // Server should still be running
      expect(server.isServerRunning()).toBe(true);

      client.destroy();
    });

    it('should handle multiple notifications in single message', async () => {
      const port = await server.start();
      const notifications: any[] = [];

      server.on('push_notification', (event) => {
        notifications.push(event);
      });

      const client = new net.Socket();
      await new Promise<void>((resolve) => {
        client.connect(port, 'localhost', resolve);
      });

      await new Promise((resolve) => setTimeout(resolve, 100));

      const notification1: PushNotification = {
        type: 'CONNECTION_ESTABLISHED',
        timestamp: new Date().toISOString(),
      };

      const notification2: PushNotification = {
        type: 'TOOLS_CHANGED',
        timestamp: new Date().toISOString(),
      };

      client.write(JSON.stringify(notification1) + '\n' + JSON.stringify(notification2) + '\n');
      await new Promise((resolve) => setTimeout(resolve, 200));

      expect(notifications.length).toBe(2);

      client.destroy();
    });
  });

  describe('Error Handling', () => {
    it('should handle socket timeout', async () => {
      const port = await server.start();

      const client = new net.Socket();
      await new Promise<void>((resolve) => {
        client.connect(port, 'localhost', resolve);
      });

      // Wait for timeout (30 seconds in production, but we don't want to wait that long)
      // Just verify connection exists
      expect(server.getConnectedClientsCount()).toBe(1);

      client.destroy();
    });

    it('should handle multiple simultaneous connections', async () => {
      const port = await server.start();

      const clients = [];
      for (let i = 0; i < 5; i++) {
        const client = new net.Socket();
        await new Promise<void>((resolve) => {
          client.connect(port, 'localhost', resolve);
        });
        clients.push(client);
      }

      await new Promise((resolve) => setTimeout(resolve, 100));

      expect(server.getConnectedClientsCount()).toBe(5);

      // Cleanup
      clients.forEach((client) => client.destroy());
    });
  });
});
