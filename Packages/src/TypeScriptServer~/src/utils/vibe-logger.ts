import * as fs from 'fs';
import * as path from 'path';
import { v4 as uuidv4 } from 'uuid';
import { OUTPUT_DIRECTORIES } from '../constants.js';

/**
 * AI-friendly structured logger for TypeScript MCP Server
 *
 * Design document reference: Packages/src/TypeScriptServer~/ARCHITECTURE.md
 *
 * Related classes:
 * - log-to-file.ts: Traditional file logger that this class replaces
 * - UnityDiscovery: Uses this logger for connection discovery tracking
 * - UnityClient: Uses this logger for connection event tracking
 *
 * Key features:
 * - Structured JSON logging with operation, context, correlation_id
 * - AI-friendly format for Claude Code analysis
 * - Automatic file rotation and memory management
 * - Correlation ID tracking for related operations
 */

export interface VibeLogEntry {
  timestamp: string;
  level: string;
  operation: string;
  message: string;
  context?: unknown;
  correlation_id: string;
  source: string;
  human_note?: string;
  ai_todo?: string;
  environment: EnvironmentInfo;
}

export interface EnvironmentInfo {
  node_version: string;
  platform: string;
  process_id: number;
  memory_usage: {
    rss: number;
    heapTotal: number;
    heapUsed: number;
  };
}

export class VibeLogger {
  // Navigate from TypeScriptServer~ to project root: ../../../
  private static readonly PROJECT_ROOT = path.resolve(process.cwd(), '../../../');
  private static readonly LOG_DIRECTORY = path.join(
    this.PROJECT_ROOT,
    OUTPUT_DIRECTORIES.ROOT,
    OUTPUT_DIRECTORIES.VIBE_LOGS,
  );
  private static readonly LOG_FILE_PREFIX = 'typescript_vibe';
  private static readonly MAX_FILE_SIZE_MB = 10;
  private static readonly MAX_MEMORY_LOGS = 1000;

  private static memoryLogs: VibeLogEntry[] = [];
  private static isDebugEnabled = process.env.MCP_DEBUG === 'true';

  /**
   * Log an info level message with structured context
   */
  static logInfo(
    operation: string,
    message: string,
    context?: unknown,
    correlationId?: string,
    humanNote?: string,
    aiTodo?: string,
  ): void {
    this.log('INFO', operation, message, context, correlationId, humanNote, aiTodo);
  }

  /**
   * Log a warning level message with structured context
   */
  static logWarning(
    operation: string,
    message: string,
    context?: unknown,
    correlationId?: string,
    humanNote?: string,
    aiTodo?: string,
  ): void {
    this.log('WARNING', operation, message, context, correlationId, humanNote, aiTodo);
  }

  /**
   * Log an error level message with structured context
   */
  static logError(
    operation: string,
    message: string,
    context?: unknown,
    correlationId?: string,
    humanNote?: string,
    aiTodo?: string,
  ): void {
    this.log('ERROR', operation, message, context, correlationId, humanNote, aiTodo);
  }

  /**
   * Log a debug level message with structured context
   */
  static logDebug(
    operation: string,
    message: string,
    context?: unknown,
    correlationId?: string,
    humanNote?: string,
    aiTodo?: string,
  ): void {
    this.log('DEBUG', operation, message, context, correlationId, humanNote, aiTodo);
  }

  /**
   * Log an exception with structured context
   */
  static logException(
    operation: string,
    error: Error,
    context?: unknown,
    correlationId?: string,
    humanNote?: string,
    aiTodo?: string,
  ): void {
    const exceptionContext = {
      original_context: context,
      exception: {
        name: error.name,
        message: error.message,
        stack: error.stack,
        cause: error.cause,
      },
    } as const;

    this.log(
      'ERROR',
      operation,
      `Exception occurred: ${error.message}`,
      exceptionContext,
      correlationId,
      humanNote,
      aiTodo,
    );
  }

  /**
   * Generate a new correlation ID for tracking related operations
   */
  static generateCorrelationId(): string {
    const timestamp = new Date().toISOString().slice(11, 19).replace(/:/g, '');
    return `ts_${uuidv4().slice(0, 8)}_${timestamp}`;
  }

  /**
   * Get logs for AI analysis (formatted for Claude Code)
   * Output directory: {project_root}/uLoopMCPOutputs/VibeLogs/
   */
  static getLogsForAi(operation?: string, correlationId?: string, maxCount: number = 100): string {
    let filteredLogs = [...this.memoryLogs];

    if (operation) {
      filteredLogs = filteredLogs.filter((log) => log.operation.includes(operation));
    }

    if (correlationId) {
      filteredLogs = filteredLogs.filter((log) => log.correlation_id === correlationId);
    }

    if (filteredLogs.length > maxCount) {
      filteredLogs = filteredLogs.slice(-maxCount);
    }

    return JSON.stringify(filteredLogs, null, 2);
  }

  /**
   * Clear all memory logs
   */
  static clearMemoryLogs(): void {
    this.memoryLogs = [];
  }

  /**
   * Core logging method
   * Only logs when MCP_DEBUG environment variable is set to 'true'
   */
  private static log(
    level: string,
    operation: string,
    message: string,
    context?: unknown,
    correlationId?: string,
    humanNote?: string,
    aiTodo?: string,
  ): void {
    // Only log when MCP_DEBUG is enabled
    if (!this.isDebugEnabled) {
      return;
    }

    const logEntry: VibeLogEntry = {
      timestamp: new Date().toISOString(),
      level,
      operation,
      message,
      context: this.sanitizeContext(context),
      correlation_id: correlationId || this.generateCorrelationId(),
      source: 'TypeScript',
      human_note: humanNote,
      ai_todo: aiTodo,
      environment: this.getEnvironmentInfo(),
    };

    // Add to memory logs
    this.memoryLogs.push(logEntry);

    // Rotate memory logs if too many
    if (this.memoryLogs.length > this.MAX_MEMORY_LOGS) {
      this.memoryLogs.shift();
    }

    // Save to file
    this.saveLogToFile(logEntry);

    // Also output to console when debugging
    console.log(`[VibeLogger] ${level} | ${operation} | ${message}`);
  }

  /**
   * Save log entry to file with retry mechanism for concurrent access
   */
  private static saveLogToFile(logEntry: VibeLogEntry): void {
    try {
      if (!fs.existsSync(this.LOG_DIRECTORY)) {
        fs.mkdirSync(this.LOG_DIRECTORY, { recursive: true });
      }

      const fileName = `${this.LOG_FILE_PREFIX}_${this.formatDate()}.json`;
      const filePath = path.join(this.LOG_DIRECTORY, fileName);

      // Check file size and rotate if necessary
      if (fs.existsSync(filePath)) {
        const stats = fs.statSync(filePath);
        if (stats.size > this.MAX_FILE_SIZE_MB * 1024 * 1024) {
          const rotatedFileName = `${this.LOG_FILE_PREFIX}_${this.formatDateTime()}.json`;
          const rotatedFilePath = path.join(this.LOG_DIRECTORY, rotatedFileName);
          fs.renameSync(filePath, rotatedFilePath);
        }
      }

      const jsonLog = JSON.stringify(logEntry) + '\n';

      // Use retry mechanism with exponential backoff for file writing
      const maxRetries = 3;
      const baseDelayMs = 50;

      for (let retry = 0; retry < maxRetries; retry++) {
        try {
          fs.appendFileSync(filePath, jsonLog, { flag: 'a' });
          return; // Success - exit retry loop
        } catch (error: unknown) {
          if (this.isFileSharingViolation(error) && retry < maxRetries - 1) {
            // Wait with exponential backoff for sharing violations
            const delayMs = baseDelayMs * Math.pow(2, retry);
            this.sleep(delayMs);
          } else {
            // For other errors or final retry, throw
            throw error;
          }
        }
      }
    } catch (error) {
      // Fallback to console if file logging fails
      console.error(
        `[VibeLogger] Failed to save log to file: ${error instanceof Error ? error.message : String(error)}`,
      );
      console.log(`[VibeLogger] ${logEntry.level} | ${logEntry.operation} | ${logEntry.message}`);
    }
  }

  /**
   * Check if error is a file sharing violation
   */
  private static isFileSharingViolation(error: unknown): boolean {
    if (!error) {
      return false;
    }

    // Check for common file sharing violation error codes
    const sharingViolationCodes = [
      'EBUSY', // Resource busy or locked
      'EACCES', // Permission denied (can indicate file in use)
      'EPERM', // Operation not permitted
      'EMFILE', // Too many open files
      'ENFILE', // File table overflow
    ];

    return (
      error &&
      typeof error === 'object' &&
      'code' in error &&
      typeof error.code === 'string' &&
      sharingViolationCodes.includes(error.code)
    );
  }

  /**
   * Synchronous sleep function for retry delays
   */
  private static sleep(ms: number): void {
    const start = Date.now();
    while (Date.now() - start < ms) {
      // Busy wait for specified duration
    }
  }

  /**
   * Get current environment information
   */
  private static getEnvironmentInfo(): EnvironmentInfo {
    const memUsage = process.memoryUsage();
    return {
      node_version: process.version,
      platform: process.platform,
      process_id: process.pid,
      memory_usage: {
        rss: memUsage.rss,
        heapTotal: memUsage.heapTotal,
        heapUsed: memUsage.heapUsed,
      },
    };
  }

  /**
   * Sanitize context to prevent circular references
   */
  private static sanitizeContext(context: unknown): unknown {
    if (!context) {
      return undefined;
    }

    try {
      return JSON.parse(JSON.stringify(context));
    } catch (error) {
      return {
        error: 'Failed to serialize context',
        original_type: typeof context,
        circular_reference: true,
      };
    }
  }

  /**
   * Format date for file naming
   */
  private static formatDate(): string {
    const now = new Date();
    return now.toISOString().slice(0, 10).replace(/-/g, '');
  }

  /**
   * Format datetime for file rotation
   */
  private static formatDateTime(): string {
    const now = new Date();
    return now.toISOString().slice(0, 19).replace(/[-:]/g, '').replace('T', '_');
  }
}

// Export singleton instance for convenience
export const vibeLogger = VibeLogger;
