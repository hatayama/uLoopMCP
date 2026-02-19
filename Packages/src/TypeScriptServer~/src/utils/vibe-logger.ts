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
  private static readonly PROJECT_ROOT = VibeLogger.findUnityProjectRoot();
  private static readonly LOG_DIRECTORY = path.join(
    VibeLogger.PROJECT_ROOT,
    OUTPUT_DIRECTORIES.ROOT,
    OUTPUT_DIRECTORIES.VIBE_LOGS,
  );
  private static readonly LOG_FILE_PREFIX = 'ts_vibe';
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
    includeStackTrace?: boolean,
  ): void {
    VibeLogger.log(
      'INFO',
      operation,
      message,
      context,
      correlationId,
      humanNote,
      aiTodo,
      includeStackTrace,
    );
  }

  /**
   * Log a warning level message with structured context and optional stack trace
   */
  static logWarning(
    operation: string,
    message: string,
    context?: unknown,
    correlationId?: string,
    humanNote?: string,
    aiTodo?: string,
    includeStackTrace: boolean = true,
  ): void {
    VibeLogger.log(
      'WARNING',
      operation,
      message,
      context,
      correlationId,
      humanNote,
      aiTodo,
      includeStackTrace,
    );
  }

  /**
   * Log an error level message with structured context and optional stack trace
   */
  static logError(
    operation: string,
    message: string,
    context?: unknown,
    correlationId?: string,
    humanNote?: string,
    aiTodo?: string,
    includeStackTrace: boolean = true,
  ): void {
    VibeLogger.log(
      'ERROR',
      operation,
      message,
      context,
      correlationId,
      humanNote,
      aiTodo,
      includeStackTrace,
    );
  }

  /**
   * Log a debug level message with structured context and optional stack trace
   */
  static logDebug(
    operation: string,
    message: string,
    context?: unknown,
    correlationId?: string,
    humanNote?: string,
    aiTodo?: string,
    includeStackTrace?: boolean,
  ): void {
    VibeLogger.log(
      'DEBUG',
      operation,
      message,
      context,
      correlationId,
      humanNote,
      aiTodo,
      includeStackTrace,
    );
  }

  /**
   * Log an exception with structured context and optional additional stack trace
   */
  static logException(
    operation: string,
    error: Error,
    context?: unknown,
    correlationId?: string,
    humanNote?: string,
    aiTodo?: string,
    includeStackTrace?: boolean,
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
      includeStackTrace,
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
   * Get resolved Unity project root path.
   */
  static getProjectRoot(): string {
    return VibeLogger.PROJECT_ROOT;
  }

  /**
   * Get logs for AI analysis (formatted for Claude Code)
   * Output directory: {project_root}/.uloop/outputs/VibeLogs/
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
      if (!VibeLogger.validateWithin(basePath, sanitizedRoot)) {
        return; // Silent failure to prevent protocol interference
      }

      const emergencyLogDir = path.resolve(sanitizedRoot, 'EmergencyLogs');
      if (!VibeLogger.validateWithin(sanitizedRoot, emergencyLogDir)) {
        return; // Path traversal guard
      }

      // SECURITY: Path validated with validateWithin() above - safe from path traversal
      // eslint-disable-next-line security/detect-non-literal-fs-filename -- Path sanitized by validateWithin()
      fs.mkdirSync(emergencyLogDir, { recursive: true });

      const emergencyLogPath = path.resolve(emergencyLogDir, 'vibe-logger-emergency.log');
      if (!VibeLogger.validateWithin(emergencyLogDir, emergencyLogPath)) {
        return; // Path traversal guard
      }

      const emergencyLog = JSON.stringify(emergencyEntry) + '\n';
      // SECURITY: Path validated with validateWithin() above - safe from path traversal
      // eslint-disable-next-line security/detect-non-literal-fs-filename -- Path sanitized by validateWithin()
      fs.appendFileSync(emergencyLogPath, emergencyLog);
    } catch {
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
    includeStackTrace?: boolean,
  ): void {
    // Only log when MCP_DEBUG is enabled
    if (!VibeLogger.isDebugEnabled) {
      return;
    }

    // Handle stack trace option
    let finalContext = context;
    if (includeStackTrace) {
      finalContext = {
        original_context: context,
        stack: VibeLogger.cleanStackTrace(new Error().stack),
      } as const;
    }

    const logEntry: VibeLogEntry = {
      timestamp: VibeLogger.formatTimestamp(),
      level,
      operation,
      message,
      context: VibeLogger.sanitizeContext(finalContext),
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
        timestamp: VibeLogger.formatTimestamp(),
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
   * Validate that target path is within base directory
   */
  private static validateWithin(base: string, target: string): boolean {
    const resolvedBase = path.resolve(base);
    const resolvedTarget = path.resolve(target);
    return resolvedTarget.startsWith(resolvedBase);
  }

  /**
   * Safe wrapper for fs.existsSync with path validation
   */
  private static safeExistsSync(filePath: string): boolean {
    try {
      const absolutePath = path.resolve(filePath);
      const expectedDir = path.resolve(VibeLogger.LOG_DIRECTORY);

      // Validate path is within expected directory
      if (!VibeLogger.validateWithin(expectedDir, absolutePath)) {
        return false;
      }

      // eslint-disable-next-line security/detect-non-literal-fs-filename -- Path sanitized by validateWithin()
      return fs.existsSync(absolutePath);
    } catch {
      return false;
    }
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

    if (!VibeLogger.safeExistsSync(logDirectory)) {
      // Security: Ensure directory path is absolute and within expected bounds
      const absoluteLogDir = path.resolve(logDirectory);
      const expectedBaseDir = path.resolve(VibeLogger.PROJECT_ROOT);

      if (!VibeLogger.validateWithin(expectedBaseDir, absoluteLogDir)) {
        throw new Error('Log directory path escapes project root');
      }

      // eslint-disable-next-line security/detect-non-literal-fs-filename -- Path sanitized by validateWithin()
      fs.mkdirSync(absoluteLogDir, { recursive: true });
    }

    const fileName = `${VibeLogger.LOG_FILE_PREFIX}_${VibeLogger.formatDate()}.json`;

    // Validate file name for security
    if (!VibeLogger.validateFileName(fileName)) {
      throw new Error('Invalid file name detected');
    }

    const filePath = path.resolve(logDirectory, fileName);
    if (!VibeLogger.validateWithin(logDirectory, filePath)) {
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
    if (!VibeLogger.safeExistsSync(filePath)) {
      return;
    }

    // Security: Use absolute path for file operations
    const absoluteFilePath = path.resolve(filePath);
    // SECURITY: Using absolute path with security validation - safe from path traversal
    // eslint-disable-next-line security/detect-non-literal-fs-filename -- Path sanitized by validateWithin()
    const stats = fs.statSync(absoluteFilePath);
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
    if (!VibeLogger.validateWithin(logDirectory, rotatedFilePath)) {
      throw new Error('Rotated file path escapes the allowed directory');
    }

    const sanitizedFilePath = path.resolve(logDirectory, path.basename(filePath));
    if (!VibeLogger.validateWithin(logDirectory, sanitizedFilePath)) {
      throw new Error('Original file path escapes the allowed directory');
    }

    // SECURITY: Both paths validated with validateWithin() above - safe from path traversal
    // eslint-disable-next-line security/detect-non-literal-fs-filename -- Both paths sanitized by validateWithin()
    fs.renameSync(sanitizedFilePath, rotatedFilePath);
  }

  /**
   * Write log to file with retry mechanism for concurrent access
   */
  private static async writeLogWithRetry(filePath: string, jsonLog: string): Promise<void> {
    const maxRetries = 3;
    const baseDelayMs = 50;

    // Security: Use absolute path and validate before writing
    const absoluteFilePath = path.resolve(filePath);
    const expectedLogDir = path.resolve(VibeLogger.LOG_DIRECTORY);

    if (!VibeLogger.validateWithin(expectedLogDir, absoluteFilePath)) {
      throw new Error('File path escapes log directory');
    }

    for (let retry = 0; retry < maxRetries; retry++) {
      try {
        // Use fs.promises.open for safer file handling

        // eslint-disable-next-line security/detect-non-literal-fs-filename
        const fileHandle = await fs.promises.open(absoluteFilePath, 'a');
        try {
          await fileHandle.writeFile(jsonLog, { encoding: 'utf8' });
        } finally {
          await fileHandle.close();
        }
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
      await VibeLogger.tryFallbackLogging(logEntry, error);
    }
  }

  /**
   * Try fallback logging when main logging fails
   */
  private static async tryFallbackLogging(logEntry: VibeLogEntry, error: unknown): Promise<void> {
    try {
      // Security: Use safe path construction to prevent path traversal
      const basePath = process.cwd();
      const sanitizedRoot = path.resolve(basePath, OUTPUT_DIRECTORIES.ROOT);
      if (!VibeLogger.validateWithin(basePath, sanitizedRoot)) {
        throw new Error('Invalid OUTPUT_DIRECTORIES.ROOT path');
      }

      const safeLogDir = path.resolve(sanitizedRoot, 'FallbackLogs');
      if (!VibeLogger.validateWithin(sanitizedRoot, safeLogDir)) {
        throw new Error('Fallback log directory path traversal detected');
      }

      // SECURITY: Path validated with validateWithin() above - safe from path traversal
      // eslint-disable-next-line security/detect-non-literal-fs-filename -- Path sanitized by validateWithin()
      fs.mkdirSync(safeLogDir, { recursive: true });

      // Security: Sanitize filename components to prevent path traversal
      const safeDate = VibeLogger.formatDateTime()
        .split(' ')[0]
        .replace(/[^0-9-]/g, '');
      const safeFilename = `${VibeLogger.LOG_FILE_PREFIX}_fallback_${safeDate}.json`;
      const safeFallbackPath = path.resolve(safeLogDir, safeFilename);

      // Security: Validate that the resolved file path is within our expected directory
      if (!VibeLogger.validateWithin(safeLogDir, safeFallbackPath)) {
        throw new Error('Invalid fallback log file path');
      }

      const fallbackEntry = {
        ...logEntry,
        fallback_reason: error instanceof Error ? error.message : String(error),
        original_timestamp: logEntry.timestamp,
      };

      const jsonLog = JSON.stringify(fallbackEntry) + '\n';

      // Use fs.promises.open for safer file writing

      // eslint-disable-next-line security/detect-non-literal-fs-filename
      const fileHandle = await fs.promises.open(safeFallbackPath, 'a');
      try {
        await fileHandle.writeFile(jsonLog, { encoding: 'utf8' });
      } finally {
        await fileHandle.close();
      }
    } catch (fallbackError) {
      // If even fallback fails, try to write to a last-resort emergency log file
      VibeLogger.writeEmergencyLog({
        timestamp: VibeLogger.formatTimestamp(),
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
    } catch {
      return {
        error: 'Failed to serialize context',
        original_type: typeof context,
        circular_reference: true,
      };
    }
  }

  /**
   * Format timestamp for log entries (local timezone)
   */
  private static formatTimestamp(): string {
    const now = new Date();
    const offset = now.getTimezoneOffset();
    const offsetHours = String(Math.floor(Math.abs(offset) / 60)).padStart(2, '0');
    const offsetMinutes = String(Math.abs(offset) % 60).padStart(2, '0');
    const offsetSign = offset <= 0 ? '+' : '-';

    return now.toISOString().replace('Z', `${offsetSign}${offsetHours}:${offsetMinutes}`);
  }

  /**
   * Format date for file naming (local timezone)
   */
  private static formatDate(): string {
    const now = new Date();
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, '0');
    const day = String(now.getDate()).padStart(2, '0');
    return `${year}${month}${day}`;
  }

  /**
   * Format datetime for file rotation
   */
  private static formatDateTime(): string {
    const now = new Date();
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, '0');
    const day = String(now.getDate()).padStart(2, '0');
    const hours = String(now.getHours()).padStart(2, '0');
    const minutes = String(now.getMinutes()).padStart(2, '0');
    const seconds = String(now.getSeconds()).padStart(2, '0');
    return `${year}${month}${day}_${hours}${minutes}${seconds}`;
  }

  /**
   * Clean stack trace by removing VibeLogger internal calls and Error prefix
   */
  private static cleanStackTrace(stack: string | undefined): string {
    if (!stack) {
      return '';
    }

    const lines = stack.split('\n');
    const cleanedLines: string[] = [];

    for (const line of lines) {
      // Skip the "Error" line at the beginning
      if (line.trim() === 'Error') {
        continue;
      }

      // Skip VibeLogger internal calls
      if (line.includes('vibe-logger.ts') || line.includes('VibeLogger')) {
        continue;
      }

      cleanedLines.push(line);
    }

    return cleanedLines.join('\n').trim();
  }

  /**
   * Find Unity project root by searching for Assets folder
   * Traverses parent directories from bundle location until Assets folder is found
   */
  private static findUnityProjectRoot(): string {
    let currentDir: string = path.dirname(fileURLToPath(import.meta.url));

    for (let i = 0; i < 20; i++) {
      const assetsPath: string = path.join(currentDir, 'Assets');
      // eslint-disable-next-line security/detect-non-literal-fs-filename -- Safe: path is constructed from module's own directory, only traverses upward
      if (fs.existsSync(assetsPath) && fs.statSync(assetsPath).isDirectory()) {
        return currentDir;
      }

      const parentDir: string = path.dirname(currentDir);
      if (parentDir === currentDir) {
        break;
      }
      currentDir = parentDir;
    }

    return path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');
  }
}

// Export singleton instance for convenience
export const vibeLogger = VibeLogger;
