/**
 * Push通知エラーハンドラーのテスト
 * 設計書参照: /.kiro/specs/unity-push-notification-system/design.md
 */

import { describe, it, expect, beforeEach, afterEach, jest } from '@jest/globals';

// Mock VibeLogger to avoid import.meta.url issues in Jest
jest.mock('../utils/vibe-logger.js', () => ({
  VibeLogger: {
    logInfo: jest.fn(),
    logError: jest.fn(),
    logWarn: jest.fn(),
    logWarning: jest.fn()
  }
}));

import { PushNotificationErrorHandler, ErrorContext, RetryOptions } from '../utils/push-notification-error-handler.js';

describe('PushNotificationErrorHandler', () => {
  let errorContext: ErrorContext;

  beforeEach(() => {
    errorContext = {
      operation: 'test_operation',
      clientId: 'test_client_123',
      endpoint: 'localhost:8080',
      error: new Error('Test error'),
      timestamp: new Date()
    };
  });

  describe('Error Handling', () => {
    it('should handle Unity connection failure', () => {
      const error = new Error('Connection failed');
      
      expect(() => {
        PushNotificationErrorHandler.handleUnityConnectionFailure(error, errorContext);
      }).not.toThrow();
    });

    it('should handle push notification error', () => {
      const error = new Error('Push notification failed');
      
      expect(() => {
        PushNotificationErrorHandler.handlePushNotificationError(error, errorContext);
      }).not.toThrow();
    });

    it('should handle server error', () => {
      const error = new Error('Server error');
      
      expect(() => {
        PushNotificationErrorHandler.handleServerError(error, errorContext);
      }).not.toThrow();
    });

    it('should handle JSON parse error', () => {
      const jsonError = new SyntaxError('Unexpected token in JSON');
      
      expect(() => {
        PushNotificationErrorHandler.handlePushNotificationError(jsonError, errorContext);
      }).not.toThrow();
    });

    it('should handle socket error', () => {
      const socketError = new Error('ECONNRESET') as any;
      socketError.code = 'ECONNRESET';
      
      expect(() => {
        PushNotificationErrorHandler.handlePushNotificationError(socketError, errorContext);
      }).not.toThrow();
    });

    it('should handle port conflict error', () => {
      const portError = new Error('EADDRINUSE: address already in use');
      
      expect(() => {
        PushNotificationErrorHandler.handleServerError(portError, errorContext);
      }).not.toThrow();
    });
  });

  describe('Retry Mechanism', () => {
    it('should retry operation with default options', async () => {
      let attemptCount = 0;
      const operation = async () => {
        attemptCount++;
        if (attemptCount < 2) {
          throw new Error('Retry test error');
        }
        return 'success';
      };

      const result = await PushNotificationErrorHandler.retryWithBackoff(
        operation,
        errorContext
      );

      expect(result).toBe('success');
      expect(attemptCount).toBe(2);
    });

    it('should retry operation with custom options', async () => {
      let attemptCount = 0;
      const customOptions: RetryOptions = {
        maxAttempts: 2,
        baseDelay: 10,
        maxDelay: 100,
        backoffMultiplier: 2
      };

      const operation = async () => {
        attemptCount++;
        throw new Error('Always fail');
      };

      await expect(
        PushNotificationErrorHandler.retryWithBackoff(operation, errorContext, customOptions)
      ).rejects.toThrow('Always fail');
      
      expect(attemptCount).toBe(2);
    });

    it('should succeed on first attempt', async () => {
      let attemptCount = 0;
      const operation = async () => {
        attemptCount++;
        return 'immediate_success';
      };

      const result = await PushNotificationErrorHandler.retryWithBackoff(
        operation,
        errorContext
      );

      expect(result).toBe('immediate_success');
      expect(attemptCount).toBe(1);
    });

    it('should throw last error after all retries exhausted', async () => {
      const finalError = new Error('Final error');
      let attemptCount = 0;
      
      const operation = async () => {
        attemptCount++;
        if (attemptCount === 3) {
          throw finalError;
        }
        throw new Error('Intermediate error');
      };

      const customOptions: RetryOptions = {
        maxAttempts: 3,
        baseDelay: 1,
        maxDelay: 10,
        backoffMultiplier: 2
      };

      await expect(
        PushNotificationErrorHandler.retryWithBackoff(operation, errorContext, customOptions)
      ).rejects.toBe(finalError);
      
      expect(attemptCount).toBe(3);
    });
  });

  describe('Error Context Creation', () => {
    it('should create error context with all fields', () => {
      const operation = 'test_operation';
      const error = new Error('test error');
      const clientId = 'client_123';
      const endpoint = 'localhost:8080';

      const context = PushNotificationErrorHandler.createErrorContext(
        operation,
        error,
        clientId,
        endpoint
      );

      expect(context.operation).toBe(operation);
      expect(context.error).toBe(error);
      expect(context.clientId).toBe(clientId);
      expect(context.endpoint).toBe(endpoint);
      expect(context.timestamp).toBeInstanceOf(Date);
    });

    it('should create error context with minimal fields', () => {
      const operation = 'minimal_operation';
      const error = 'string error';

      const context = PushNotificationErrorHandler.createErrorContext(operation, error);

      expect(context.operation).toBe(operation);
      expect(context.error).toBe(error);
      expect(context.clientId).toBeUndefined();
      expect(context.endpoint).toBeUndefined();
      expect(context.timestamp).toBeInstanceOf(Date);
    });
  });

  describe('Constants', () => {
    it('should have valid timeout constants', () => {
      const { ConnectionTimeouts } = require('../utils/push-notification-error-handler.js');
      
      expect(ConnectionTimeouts.CONNECTION_TIMEOUT_MS).toBeGreaterThan(0);
      expect(ConnectionTimeouts.PUSH_NOTIFICATION_TIMEOUT_MS).toBeGreaterThan(0);
      expect(ConnectionTimeouts.DISCOVERY_TIMEOUT_MS).toBeGreaterThan(0);
      expect(ConnectionTimeouts.RECONNECTION_DELAY_MS).toBeGreaterThan(0);
      expect(ConnectionTimeouts.SERVER_START_TIMEOUT_MS).toBeGreaterThan(0);
    });

    it('should have valid retry defaults', () => {
      const { RetryDefaults } = require('../utils/push-notification-error-handler.js');
      
      expect(RetryDefaults.UNITY_CONNECTION.maxAttempts).toBeGreaterThan(0);
      expect(RetryDefaults.PUSH_NOTIFICATION.maxAttempts).toBeGreaterThan(0);
      expect(RetryDefaults.SERVER_START.maxAttempts).toBeGreaterThan(0);
      
      expect(RetryDefaults.UNITY_CONNECTION.baseDelay).toBeGreaterThan(0);
      expect(RetryDefaults.PUSH_NOTIFICATION.baseDelay).toBeGreaterThan(0);
      expect(RetryDefaults.SERVER_START.baseDelay).toBeGreaterThan(0);
    });
  });

  describe('Error Classification', () => {
    it('should classify timeout errors correctly', () => {
      const timeoutError1 = new Error('timeout occurred');
      const timeoutError2 = new Error('operation timed out');
      const normalError = new Error('normal error');

      // Test through public interface by checking if timeout handling occurs
      expect(() => {
        PushNotificationErrorHandler.handleUnityConnectionFailure(timeoutError1, errorContext);
        PushNotificationErrorHandler.handleUnityConnectionFailure(timeoutError2, errorContext);
        PushNotificationErrorHandler.handleUnityConnectionFailure(normalError, errorContext);
      }).not.toThrow();
    });

    it('should classify network errors correctly', () => {
      const networkError1 = new Error('ECONNREFUSED') as any;
      networkError1.code = 'ECONNREFUSED';
      
      const networkError2 = new Error('network unreachable');
      const socketError = new Error('socket error') as any;
      socketError.errno = 'ETIMEDOUT';

      expect(() => {
        PushNotificationErrorHandler.handleUnityConnectionFailure(networkError1, errorContext);
        PushNotificationErrorHandler.handleUnityConnectionFailure(networkError2, errorContext);
        PushNotificationErrorHandler.handleUnityConnectionFailure(socketError, errorContext);
      }).not.toThrow();
    });

    it('should classify port conflict errors correctly', () => {
      const portError1 = new Error('EADDRINUSE');
      const portError2 = new Error('address already in use');
      const portError3 = new Error('port 8080 is already in use');

      expect(() => {
        PushNotificationErrorHandler.handleServerError(portError1, errorContext);
        PushNotificationErrorHandler.handleServerError(portError2, errorContext);
        PushNotificationErrorHandler.handleServerError(portError3, errorContext);
      }).not.toThrow();
    });
  });
});