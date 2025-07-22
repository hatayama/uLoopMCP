/**
 * TypeScript Push通知エラーハンドリング
 * 設計書参照: /.kiro/specs/unity-push-notification-system/design.md
 * 関連クラス: UnityPushNotificationReceiveServer.ts, UnityClient.ts
 *
 * 責任:
 * - Unity接続失敗時の処理
 * - Push通知受信エラー処理
 * - サーバーエラーハンドリング
 * - 復旧メカニズム
 */

import { VibeLogger } from './vibe-logger.js';

export interface ErrorContext {
  operation: string;
  clientId?: string;
  endpoint?: string;
  error: Error | string;
  timestamp: Date;
}

export interface RetryOptions {
  maxAttempts: number;
  baseDelay: number;
  maxDelay: number;
  backoffMultiplier: number;
}

interface SocketError extends Error {
  code?: string;
  errno?: string;
  syscall?: string;
}

export class PushNotificationErrorHandler {
  private static readonly DEFAULT_RETRY_OPTIONS: RetryOptions = {
    maxAttempts: 3,
    baseDelay: 1000,
    maxDelay: 10000,
    backoffMultiplier: 2,
  };

  public static handleUnityConnectionFailure(error: Error, context: ErrorContext): void {
    VibeLogger.logError(
      'unity_connection_failure',
      'Unity connection failed',
      {
        operation: context.operation,
        clientId: context.clientId,
        endpoint: context.endpoint,
        error_message: error.message,
        error_type: error.constructor.name,
        timestamp: context.timestamp.toISOString(),
      },
      undefined,
      'Unity connection could not be established - check Unity MCP bridge',
    );

    this.logErrorStatistics(error, context);
  }

  public static handlePushNotificationError(error: Error, context: ErrorContext): void {
    VibeLogger.logError(
      'push_notification_error',
      'Push notification processing failed',
      {
        operation: context.operation,
        clientId: context.clientId,
        error_message: error.message,
        error_type: error.constructor.name,
        timestamp: context.timestamp.toISOString(),
      },
      undefined,
      'Push notification could not be processed - check message format',
    );

    if (this.isJsonParseError(error)) {
      this.handleJsonParseError(error, context);
    } else if (this.isSocketError(error)) {
      this.handleSocketError(error, context);
    }
  }

  public static handleServerError(error: Error, context: ErrorContext): void {
    VibeLogger.logError(
      'push_server_error',
      'Push notification server error',
      {
        operation: context.operation,
        error_message: error.message,
        error_type: error.constructor.name,
        timestamp: context.timestamp.toISOString(),
      },
      undefined,
      'Push notification server encountered an error',
    );

    if (this.isPortConflictError(error)) {
      this.handlePortConflictError(error, context);
    }
  }

  public static async retryWithBackoff<T>(
    operation: () => Promise<T>,
    context: ErrorContext,
    options: RetryOptions = PushNotificationErrorHandler.DEFAULT_RETRY_OPTIONS,
  ): Promise<T> {
    let lastError: Error | null = null;
    let delay = options.baseDelay;

    for (let attempt = 1; attempt <= options.maxAttempts; attempt++) {
      try {
        return await operation();
      } catch (error) {
        lastError = error instanceof Error ? error : new Error(String(error));

        if (attempt === options.maxAttempts) {
          break;
        }

        VibeLogger.logWarning(
          'retry_attempt',
          `Retry attempt ${attempt}/${options.maxAttempts} failed`,
          {
            operation: context.operation,
            attempt,
            delay,
            error_message: lastError.message,
          },
          undefined,
          `Will retry after ${delay}ms delay`,
        );

        await this.delay(delay);
        delay = Math.min(delay * options.backoffMultiplier, options.maxDelay);
      }
    }

    VibeLogger.logError(
      'retry_exhausted',
      'All retry attempts exhausted',
      {
        operation: context.operation,
        attempts: options.maxAttempts,
        final_error: lastError?.message || 'Unknown error',
      },
      undefined,
      'Operation failed after all retry attempts',
    );

    throw lastError;
  }

  public static createErrorContext(
    operation: string,
    error: Error | string,
    clientId?: string,
    endpoint?: string,
  ): ErrorContext {
    return {
      operation,
      clientId,
      endpoint,
      error,
      timestamp: new Date(),
    };
  }

  private static handleJsonParseError(error: Error, context: ErrorContext): void {
    VibeLogger.logWarning(
      'json_parse_error',
      'Invalid JSON in push notification',
      {
        operation: context.operation,
        clientId: context.clientId,
        error_message: error.message,
      },
      undefined,
      'Unity sent malformed JSON - connection may be unstable',
    );
  }

  private static handleSocketError(error: Error, context: ErrorContext): void {
    const socketError = error as SocketError;

    VibeLogger.logWarning(
      'socket_error',
      'Socket connection error',
      {
        operation: context.operation,
        clientId: context.clientId,
        error_code: socketError.code,
        error_errno: socketError.errno,
        error_syscall: socketError.syscall,
      },
      undefined,
      'Socket connection issue detected - may require reconnection',
    );
  }

  private static handlePortConflictError(error: Error, context: ErrorContext): void {
    VibeLogger.logError(
      'port_conflict_error',
      'Port conflict detected',
      {
        operation: context.operation,
        error_message: error.message,
      },
      undefined,
      'Push notification server port is already in use',
    );
  }

  private static isJsonParseError(error: Error): boolean {
    return error instanceof SyntaxError && error.message.includes('JSON');
  }

  private static isSocketError(error: Error): boolean {
    const socketError = error as SocketError;
    return (
      socketError.code !== undefined ||
      socketError.errno !== undefined ||
      error.message.includes('socket') ||
      error.message.includes('ECONNRESET') ||
      error.message.includes('ECONNREFUSED')
    );
  }

  private static isPortConflictError(error: Error): boolean {
    return (
      error.message.includes('EADDRINUSE') ||
      error.message.includes('address already in use') ||
      (error.message.includes('port') && error.message.includes('use'))
    );
  }

  private static logErrorStatistics(error: Error, context: ErrorContext): void {
    const errorStats = {
      operation: context.operation,
      error_type: error.constructor.name,
      is_timeout: error.message.includes('timeout') || error.message.includes('ETIMEDOUT'),
      is_connection_refused: error.message.includes('ECONNREFUSED'),
      is_network_unreachable: error.message.includes('ENETUNREACH'),
      client_id: context.clientId,
      endpoint: context.endpoint,
    };

    VibeLogger.logInfo(
      'error_statistics',
      'Error classification and statistics',
      errorStats,
      undefined,
      'Analyze error patterns for debugging',
    );
  }

  private static delay(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }
}

export const ConnectionTimeouts = {
  CONNECTION_TIMEOUT_MS: 5000,
  PUSH_NOTIFICATION_TIMEOUT_MS: 2000,
  DISCOVERY_TIMEOUT_MS: 10000,
  RECONNECTION_DELAY_MS: 3000,
  SERVER_START_TIMEOUT_MS: 10000,
} as const;

export const RetryDefaults = {
  UNITY_CONNECTION: {
    maxAttempts: 5,
    baseDelay: 2000,
    maxDelay: 15000,
    backoffMultiplier: 1.5,
  },
  PUSH_NOTIFICATION: {
    maxAttempts: 3,
    baseDelay: 1000,
    maxDelay: 5000,
    backoffMultiplier: 2,
  },
  SERVER_START: {
    maxAttempts: 3,
    baseDelay: 5000,
    maxDelay: 15000,
    backoffMultiplier: 2,
  },
} as const;
