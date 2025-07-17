import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';
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
  private static readonly PROJECT_ROOT = path.resolve(
    path.dirname(fileURLToPath(import.meta.url)),
    '../../../..',
  );
  private static readonly LOG_DIRECTORY = path.join(
    VibeLogger.PROJECT_ROOT,
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
    VibeLogger.log('INFO', operation, message, context, correlationId, humanNote, aiTodo);
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
    VibeLogger.log('WARNING', operation, message, context, correlationId, humanNote, aiTodo);
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
    VibeLogger.log('ERROR', operation, message, context, correlationId, humanNote, aiTodo);
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
    VibeLogger.log('DEBUG', operation, message, context, correlationId, humanNote, aiTodo);
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

    VibeLogger.log(
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
    let filteredLogs = [...VibeLogger.memoryLogs];

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
    VibeLogger.memoryLogs = [];
  }

  /**
   * Write emergency log entry (for when main logging fails)
   * Static method to avoid circular dependency
   */
  private static writeEmergencyLog(emergencyEntry: Record<string, unknown>): void {
    try {
      // Security: Use safe path construction consistent with other logging
      const basePath = process.cwd();
      const sanitizedRoot = path.resolve(basePath, OUTPUT_DIRECTORIES.ROOT);
      if (!sanitizedRoot.startsWith(basePath)) {
        return; // Silent failure to prevent protocol interference
      }

      const emergencyLogDir = path.join(sanitizedRoot, 'EmergencyLogs');
      // eslint-disable-next-line security/detect-non-literal-fs-filename
      fs.mkdirSync(emergencyLogDir, { recursive: true });

      const emergencyLogPath = path.join(emergencyLogDir, 'vibe-logger-emergency.log');
      const emergencyLog = JSON.stringify(emergencyEntry) + '\n';
      // eslint-disable-next-line security/detect-non-literal-fs-filename
      fs.appendFileSync(emergencyLogPath, emergencyLog);
    } catch (error) {
      // Silent failure to prevent MCP protocol interference
      // If even emergency logging fails, we cannot do anything more
    }
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
    if (!VibeLogger.isDebugEnabled) {
      return;
    }

    const logEntry: VibeLogEntry = {
      timestamp: new Date().toISOString(),
      level,
      operation,
      message,
      context: VibeLogger.sanitizeContext(context),
      correlation_id: correlationId || VibeLogger.generateCorrelationId(),
      source: 'TypeScript',
      human_note: humanNote,
      ai_todo: aiTodo,
      environment: VibeLogger.getEnvironmentInfo(),
    };

    // Add to memory logs
    VibeLogger.memoryLogs.push(logEntry);

    // Rotate memory logs if too many
    if (VibeLogger.memoryLogs.length > VibeLogger.MAX_MEMORY_LOGS) {
      VibeLogger.memoryLogs.shift();
    }

    // Save to file (fire and forget to avoid blocking)
    VibeLogger.saveLogToFile(logEntry).catch((error) => {
      // File logging failed - write to emergency log instead of console
      // Critical: No console output to avoid MCP protocol interference
      VibeLogger.writeEmergencyLog({
        timestamp: new Date().toISOString(),
        level: 'EMERGENCY',
        message: 'VibeLogger saveLogToFile failed',
        original_error: error instanceof Error ? error.message : String(error),
        original_log_entry: logEntry,
      });
    });

    // VibeLogger is designed for file output only - console output removed to prevent MCP protocol interference
  }

  /**
   * Validate file name to prevent dangerous characters
   */
  private static validateFileName(fileName: string): boolean {
    // Allow only alphanumeric characters, hyphens, underscores, and dots
    const safeFileNameRegex = /^[a-zA-Z0-9._-]+$/;
    return safeFileNameRegex.test(fileName) && !fileName.includes('..');
  }

  /**
   * Validate file path to prevent directory traversal attacks
   */
  private static validateFilePath(filePath: string): boolean {
    const normalizedPath = path.normalize(filePath);
    const logDirectory = path.normalize(VibeLogger.LOG_DIRECTORY);

    // Ensure the file path is within the log directory
    return normalizedPath.startsWith(logDirectory + path.sep) || normalizedPath === logDirectory;
  }

  /**
   * Prepare and validate log directory and file path
   */
  private static prepareLogFilePath(): string {
    // Validate log directory path for security
    const logDirectory = path.normalize(VibeLogger.LOG_DIRECTORY);
    if (!VibeLogger.validateFilePath(logDirectory)) {
      throw new Error('Invalid log directory path');
    }

    // eslint-disable-next-line security/detect-non-literal-fs-filename
    if (!fs.existsSync(logDirectory)) {
      // eslint-disable-next-line security/detect-non-literal-fs-filename
      fs.mkdirSync(logDirectory, { recursive: true });
    }

    const fileName = `${VibeLogger.LOG_FILE_PREFIX}_${VibeLogger.formatDate()}.json`;

    // Validate file name for security
    if (!VibeLogger.validateFileName(fileName)) {
      throw new Error('Invalid file name detected');
    }

    const filePath = path.resolve(logDirectory, fileName);
    if (!filePath.startsWith(logDirectory)) {
      throw new Error('Resolved file path escapes the log directory');
    }

    // Validate the final file path for security
    if (!VibeLogger.validateFilePath(filePath)) {
      throw new Error('Invalid file path detected');
    }

    return filePath;
  }

  /**
   * Rotate log file if it exceeds maximum size
   */
  private static rotateLogFileIfNeeded(filePath: string): void {
    // eslint-disable-next-line security/detect-non-literal-fs-filename
    if (!fs.existsSync(filePath)) {
      return;
    }

    // eslint-disable-next-line security/detect-non-literal-fs-filename
    const stats = fs.statSync(filePath);
    if (stats.size <= VibeLogger.MAX_FILE_SIZE_MB * 1024 * 1024) {
      return;
    }

    const logDirectory = path.dirname(filePath);
    const rotatedFileName = `${VibeLogger.LOG_FILE_PREFIX}_${VibeLogger.formatDateTime()}.json`;

    // Validate rotated file name for security
    if (!VibeLogger.validateFileName(rotatedFileName)) {
      throw new Error('Invalid rotated file name detected');
    }

    const rotatedFilePath = path.resolve(logDirectory, rotatedFileName);

    // Ensure rotated file path is within the allowed directory
    if (!rotatedFilePath.startsWith(path.resolve(logDirectory))) {
      throw new Error('Rotated file path escapes the allowed directory');
    }

    const sanitizedFilePath = path.resolve(logDirectory, path.basename(filePath));
    if (!sanitizedFilePath.startsWith(path.resolve(logDirectory))) {
      throw new Error('Original file path escapes the allowed directory');
    }

    // eslint-disable-next-line security/detect-non-literal-fs-filename
    fs.renameSync(sanitizedFilePath, rotatedFilePath);
  }

  /**
   * Write log to file with retry mechanism for concurrent access
   */
  private static async writeLogWithRetry(filePath: string, jsonLog: string): Promise<void> {
    const maxRetries = 3;
    const baseDelayMs = 50;

    for (let retry = 0; retry < maxRetries; retry++) {
      try {
        // eslint-disable-next-line security/detect-non-literal-fs-filename
        fs.appendFileSync(filePath, jsonLog, { flag: 'a' });
        return; // Success - exit retry loop
      } catch (error: unknown) {
        if (VibeLogger.isFileSharingViolation(error) && retry < maxRetries - 1) {
          // Wait with exponential backoff for sharing violations
          const delayMs = baseDelayMs * Math.pow(2, retry);
          await VibeLogger.sleep(delayMs);
        } else {
          // For other errors or final retry, throw
          throw error;
        }
      }
    }
  }

  /**
   * Save log entry to file with retry mechanism for concurrent access
   */
  private static async saveLogToFile(logEntry: VibeLogEntry): Promise<void> {
    try {
      const filePath = VibeLogger.prepareLogFilePath();

      // Check file size and rotate if necessary
      VibeLogger.rotateLogFileIfNeeded(filePath);

      const jsonLog = JSON.stringify(logEntry) + '\n';
      await VibeLogger.writeLogWithRetry(filePath, jsonLog);
    } catch (error) {
      // File logging failed - try fallback logging
      VibeLogger.tryFallbackLogging(logEntry, error);
    }
  }

  /**
   * Try fallback logging when main logging fails
   */
  private static tryFallbackLogging(logEntry: VibeLogEntry, error: unknown): void {
    try {
      // Security: Use safe path construction to prevent path traversal
      const basePath = process.cwd();
      const sanitizedRoot = path.resolve(basePath, OUTPUT_DIRECTORIES.ROOT);
      if (!sanitizedRoot.startsWith(basePath)) {
        throw new Error('Invalid OUTPUT_DIRECTORIES.ROOT path');
      }

      const safeLogDir = path.join(sanitizedRoot, 'FallbackLogs');
      // eslint-disable-next-line security/detect-non-literal-fs-filename
      fs.mkdirSync(safeLogDir, { recursive: true });

      // Security: Sanitize filename components to prevent path traversal
      const safeDate = VibeLogger.formatDateTime()
        .split(' ')[0]
        .replace(/[^0-9-]/g, '');
      const safeFilename = `${VibeLogger.LOG_FILE_PREFIX}_fallback_${safeDate}.json`;
      const safeFallbackPath = path.resolve(safeLogDir, safeFilename);

      // Security: Validate that the resolved file path is within our expected directory
      if (!safeFallbackPath.startsWith(safeLogDir)) {
        throw new Error('Invalid fallback log file path');
      }

      const fallbackEntry = {
        ...logEntry,
        fallback_reason: error instanceof Error ? error.message : String(error),
        original_timestamp: logEntry.timestamp,
      };

      const jsonLog = JSON.stringify(fallbackEntry) + '\n';

      // Final validation before writing to the file
      if (!safeFallbackPath.startsWith(safeLogDir)) {
        throw new Error('Invalid fallback log file path (revalidation failed)');
      }

      // Use a safer method for file writing to comply with security rules
      // eslint-disable-next-line security/detect-non-literal-fs-filename
      const writeStream = fs.createWriteStream(safeFallbackPath, { flags: 'a' });
      writeStream.write(jsonLog);
      writeStream.end();
    } catch (fallbackError) {
      // If even fallback fails, try to write to a last-resort emergency log file
      VibeLogger.writeEmergencyLog({
        timestamp: new Date().toISOString(),
        level: 'EMERGENCY',
        message: 'VibeLogger fallback failed',
        original_error: error instanceof Error ? error.message : String(error),
        fallback_error:
          fallbackError instanceof Error ? fallbackError.message : String(fallbackError),
        original_log_entry: logEntry,
      });
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
   * Asynchronous sleep function for retry delays
   */
  private static async sleep(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
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
