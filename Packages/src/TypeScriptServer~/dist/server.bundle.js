#!/usr/bin/env node

// src/server.ts
import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
  InitializeRequestSchema
} from "@modelcontextprotocol/sdk/types.js";

// src/unity-client.ts
import * as net from "net";

// src/constants.ts
var MCP_PROTOCOL_VERSION = "2024-11-05";
var MCP_SERVER_NAME = "uloopmcp-server";
var TOOLS_LIST_CHANGED_CAPABILITY = true;
var NOTIFICATION_METHODS = {
  TOOLS_LIST_CHANGED: "notifications/tools/list_changed"
};
var UNITY_CONNECTION = {
  DEFAULT_PORT: "8700",
  DEFAULT_HOST: "127.0.0.1",
  CONNECTION_TEST_MESSAGE: "connection_test"
};
var JSONRPC = {
  VERSION: "2.0"
};
var PARAMETER_SCHEMA = {
  TYPE_PROPERTY: "Type",
  DESCRIPTION_PROPERTY: "Description",
  DEFAULT_VALUE_PROPERTY: "DefaultValue",
  ENUM_PROPERTY: "Enum",
  PROPERTIES_PROPERTY: "Properties",
  REQUIRED_PROPERTY: "Required"
};
var TIMEOUTS = {
  NETWORK: 12e4
  // 2分 - ネットワークレベルのタイムアウト（Unity側のタイムアウトより長く設定）
};
var DEFAULT_CLIENT_NAME = "";
var ENVIRONMENT = {
  NODE_ENV_DEVELOPMENT: "development",
  NODE_ENV_PRODUCTION: "production"
};
var ERROR_MESSAGES = {
  NOT_CONNECTED: "Unity MCP Bridge is not connected",
  CONNECTION_FAILED: "Unity connection failed",
  TIMEOUT: "timeout",
  INVALID_RESPONSE: "Invalid response from Unity"
};
var POLLING = {
  INTERVAL_MS: 1e3,
  // Reduced from 3000ms to 1000ms for better responsiveness
  BUFFER_SECONDS: 15,
  // Increased for safer Unity startup timing
  // Adaptive polling configuration
  INITIAL_ATTEMPTS: 1,
  // Number of initial attempts with fast polling
  INITIAL_INTERVAL_MS: 1e3,
  // Fast polling interval for initial attempts
  EXTENDED_INTERVAL_MS: 1e4
  // Slower polling interval after initial attempts
};
var LIST_CHANGED_SUPPORTED_CLIENTS = ["cursor", "mcp-inspector"];
var OUTPUT_DIRECTORIES = {
  ROOT: "uLoopMCPOutputs",
  VIBE_LOGS: "VibeLogs"
};

// src/utils/safe-timer.ts
var SafeTimer = class _SafeTimer {
  static activeTimers = /* @__PURE__ */ new Set();
  static cleanupHandlersInstalled = false;
  timerId = null;
  isActive = false;
  callback;
  delay;
  isInterval;
  constructor(callback, delay, isInterval = false) {
    this.callback = callback;
    this.delay = delay;
    this.isInterval = isInterval;
    _SafeTimer.installCleanupHandlers();
    this.start();
  }
  /**
   * Start the timer
   */
  start() {
    if (this.isActive) {
      return;
    }
    if (this.isInterval) {
      this.timerId = setInterval(this.callback, this.delay);
    } else {
      this.timerId = setTimeout(this.callback, this.delay);
    }
    this.isActive = true;
    _SafeTimer.activeTimers.add(this);
  }
  /**
   * Stop and clean up the timer
   */
  stop() {
    if (!this.isActive || !this.timerId) {
      return;
    }
    if (this.isInterval) {
      clearInterval(this.timerId);
    } else {
      clearTimeout(this.timerId);
    }
    this.timerId = null;
    this.isActive = false;
    _SafeTimer.activeTimers.delete(this);
  }
  /**
   * Check if timer is currently active
   */
  get active() {
    return this.isActive;
  }
  /**
   * Install global cleanup handlers to ensure all timers are cleaned up
   */
  static installCleanupHandlers() {
    if (_SafeTimer.cleanupHandlersInstalled) {
      return;
    }
    const cleanup = () => {
      _SafeTimer.cleanupAll();
    };
    process.on("exit", cleanup);
    process.on("SIGINT", cleanup);
    process.on("SIGTERM", cleanup);
    process.on("SIGHUP", cleanup);
    process.on("uncaughtException", cleanup);
    process.on("unhandledRejection", cleanup);
    _SafeTimer.cleanupHandlersInstalled = true;
  }
  /**
   * Clean up all active timers
   */
  static cleanupAll() {
    for (const timer of _SafeTimer.activeTimers) {
      timer.stop();
    }
    _SafeTimer.activeTimers.clear();
  }
  /**
   * Get count of active timers (for debugging)
   */
  static getActiveTimerCount() {
    return _SafeTimer.activeTimers.size;
  }
};
function safeSetTimeout(callback, delay) {
  return new SafeTimer(callback, delay, false);
}
function stopSafeTimer(timer) {
  if (!timer) {
    return;
  }
  timer.stop();
}

// src/utils/vibe-logger.ts
import * as fs from "fs";
import * as path from "path";
import { fileURLToPath } from "url";
import { v4 as uuidv4 } from "uuid";
var VibeLogger = class _VibeLogger {
  // Navigate from TypeScriptServer~ to project root: ../../../
  static PROJECT_ROOT = path.resolve(
    path.dirname(fileURLToPath(import.meta.url)),
    "../../../.."
  );
  static LOG_DIRECTORY = path.join(
    _VibeLogger.PROJECT_ROOT,
    OUTPUT_DIRECTORIES.ROOT,
    OUTPUT_DIRECTORIES.VIBE_LOGS
  );
  static LOG_FILE_PREFIX = "ts_vibe";
  static MAX_FILE_SIZE_MB = 10;
  static MAX_MEMORY_LOGS = 1e3;
  static memoryLogs = [];
  static isDebugEnabled = process.env.MCP_DEBUG === "true";
  /**
   * Log an info level message with structured context
   */
  static logInfo(operation, message, context, correlationId, humanNote, aiTodo, includeStackTrace) {
    _VibeLogger.log(
      "INFO",
      operation,
      message,
      context,
      correlationId,
      humanNote,
      aiTodo,
      includeStackTrace
    );
  }
  /**
   * Log a warning level message with structured context and optional stack trace
   */
  static logWarning(operation, message, context, correlationId, humanNote, aiTodo, includeStackTrace = true) {
    _VibeLogger.log(
      "WARNING",
      operation,
      message,
      context,
      correlationId,
      humanNote,
      aiTodo,
      includeStackTrace
    );
  }
  /**
   * Log an error level message with structured context and optional stack trace
   */
  static logError(operation, message, context, correlationId, humanNote, aiTodo, includeStackTrace = true) {
    _VibeLogger.log(
      "ERROR",
      operation,
      message,
      context,
      correlationId,
      humanNote,
      aiTodo,
      includeStackTrace
    );
  }
  /**
   * Log a debug level message with structured context and optional stack trace
   */
  static logDebug(operation, message, context, correlationId, humanNote, aiTodo, includeStackTrace) {
    _VibeLogger.log(
      "DEBUG",
      operation,
      message,
      context,
      correlationId,
      humanNote,
      aiTodo,
      includeStackTrace
    );
  }
  /**
   * Log an exception with structured context and optional additional stack trace
   */
  static logException(operation, error, context, correlationId, humanNote, aiTodo, includeStackTrace) {
    const exceptionContext = {
      original_context: context,
      exception: {
        name: error.name,
        message: error.message,
        stack: error.stack,
        cause: error.cause
      }
    };
    _VibeLogger.log(
      "ERROR",
      operation,
      `Exception occurred: ${error.message}`,
      exceptionContext,
      correlationId,
      humanNote,
      aiTodo,
      includeStackTrace
    );
  }
  /**
   * Generate a new correlation ID for tracking related operations
   */
  static generateCorrelationId() {
    const timestamp = (/* @__PURE__ */ new Date()).toISOString().slice(11, 19).replace(/:/g, "");
    return `ts_${uuidv4().slice(0, 8)}_${timestamp}`;
  }
  /**
   * Get logs for AI analysis (formatted for Claude Code)
   * Output directory: {project_root}/uLoopMCPOutputs/VibeLogs/
   */
  static getLogsForAi(operation, correlationId, maxCount = 100) {
    let filteredLogs = [..._VibeLogger.memoryLogs];
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
  static clearMemoryLogs() {
    _VibeLogger.memoryLogs = [];
  }
  /**
   * Write emergency log entry (for when main logging fails)
   * Static method to avoid circular dependency
   */
  static writeEmergencyLog(emergencyEntry) {
    try {
      const basePath = process.cwd();
      const sanitizedRoot = path.resolve(basePath, OUTPUT_DIRECTORIES.ROOT);
      if (!_VibeLogger.validateWithin(basePath, sanitizedRoot)) {
        return;
      }
      const emergencyLogDir = path.resolve(sanitizedRoot, "EmergencyLogs");
      if (!_VibeLogger.validateWithin(sanitizedRoot, emergencyLogDir)) {
        return;
      }
      fs.mkdirSync(emergencyLogDir, { recursive: true });
      const emergencyLogPath = path.resolve(emergencyLogDir, "vibe-logger-emergency.log");
      if (!_VibeLogger.validateWithin(emergencyLogDir, emergencyLogPath)) {
        return;
      }
      const emergencyLog = JSON.stringify(emergencyEntry) + "\n";
      fs.appendFileSync(emergencyLogPath, emergencyLog);
    } catch (error) {
    }
  }
  /**
   * Core logging method
   * Only logs when MCP_DEBUG environment variable is set to 'true'
   */
  static log(level, operation, message, context, correlationId, humanNote, aiTodo, includeStackTrace) {
    if (!_VibeLogger.isDebugEnabled) {
      return;
    }
    let finalContext = context;
    if (includeStackTrace) {
      finalContext = {
        original_context: context,
        stack: _VibeLogger.cleanStackTrace(new Error().stack)
      };
    }
    const logEntry = {
      timestamp: _VibeLogger.formatTimestamp(),
      level,
      operation,
      message,
      context: _VibeLogger.sanitizeContext(finalContext),
      correlation_id: correlationId || _VibeLogger.generateCorrelationId(),
      source: "TypeScript",
      human_note: humanNote,
      ai_todo: aiTodo,
      environment: _VibeLogger.getEnvironmentInfo()
    };
    _VibeLogger.memoryLogs.push(logEntry);
    if (_VibeLogger.memoryLogs.length > _VibeLogger.MAX_MEMORY_LOGS) {
      _VibeLogger.memoryLogs.shift();
    }
    _VibeLogger.saveLogToFile(logEntry).catch((error) => {
      _VibeLogger.writeEmergencyLog({
        timestamp: _VibeLogger.formatTimestamp(),
        level: "EMERGENCY",
        message: "VibeLogger saveLogToFile failed",
        original_error: error instanceof Error ? error.message : String(error),
        original_log_entry: logEntry
      });
    });
  }
  /**
   * Validate file name to prevent dangerous characters
   */
  static validateFileName(fileName) {
    const safeFileNameRegex = /^[a-zA-Z0-9._-]+$/;
    return safeFileNameRegex.test(fileName) && !fileName.includes("..");
  }
  /**
   * Validate file path to prevent directory traversal attacks
   */
  static validateFilePath(filePath) {
    const normalizedPath = path.normalize(filePath);
    const logDirectory = path.normalize(_VibeLogger.LOG_DIRECTORY);
    return normalizedPath.startsWith(logDirectory + path.sep) || normalizedPath === logDirectory;
  }
  /**
   * Validate that target path is within base directory
   */
  static validateWithin(base, target) {
    const resolvedBase = path.resolve(base);
    const resolvedTarget = path.resolve(target);
    return resolvedTarget.startsWith(resolvedBase);
  }
  /**
   * Safe wrapper for fs.existsSync with path validation
   */
  static safeExistsSync(filePath) {
    try {
      const absolutePath = path.resolve(filePath);
      const expectedDir = path.resolve(_VibeLogger.LOG_DIRECTORY);
      if (!_VibeLogger.validateWithin(expectedDir, absolutePath)) {
        return false;
      }
      return fs.existsSync(absolutePath);
    } catch (error) {
      return false;
    }
  }
  /**
   * Prepare and validate log directory and file path
   */
  static prepareLogFilePath() {
    const logDirectory = path.normalize(_VibeLogger.LOG_DIRECTORY);
    if (!_VibeLogger.validateFilePath(logDirectory)) {
      throw new Error("Invalid log directory path");
    }
    if (!_VibeLogger.safeExistsSync(logDirectory)) {
      const absoluteLogDir = path.resolve(logDirectory);
      const expectedBaseDir = path.resolve(_VibeLogger.PROJECT_ROOT);
      if (!_VibeLogger.validateWithin(expectedBaseDir, absoluteLogDir)) {
        throw new Error("Log directory path escapes project root");
      }
      fs.mkdirSync(absoluteLogDir, { recursive: true });
    }
    const fileName = `${_VibeLogger.LOG_FILE_PREFIX}_${_VibeLogger.formatDate()}.json`;
    if (!_VibeLogger.validateFileName(fileName)) {
      throw new Error("Invalid file name detected");
    }
    const filePath = path.resolve(logDirectory, fileName);
    if (!_VibeLogger.validateWithin(logDirectory, filePath)) {
      throw new Error("Resolved file path escapes the log directory");
    }
    if (!_VibeLogger.validateFilePath(filePath)) {
      throw new Error("Invalid file path detected");
    }
    return filePath;
  }
  /**
   * Rotate log file if it exceeds maximum size
   */
  static rotateLogFileIfNeeded(filePath) {
    if (!_VibeLogger.safeExistsSync(filePath)) {
      return;
    }
    const absoluteFilePath = path.resolve(filePath);
    const stats = fs.statSync(absoluteFilePath);
    if (stats.size <= _VibeLogger.MAX_FILE_SIZE_MB * 1024 * 1024) {
      return;
    }
    const logDirectory = path.dirname(filePath);
    const rotatedFileName = `${_VibeLogger.LOG_FILE_PREFIX}_${_VibeLogger.formatDateTime()}.json`;
    if (!_VibeLogger.validateFileName(rotatedFileName)) {
      throw new Error("Invalid rotated file name detected");
    }
    const rotatedFilePath = path.resolve(logDirectory, rotatedFileName);
    if (!_VibeLogger.validateWithin(logDirectory, rotatedFilePath)) {
      throw new Error("Rotated file path escapes the allowed directory");
    }
    const sanitizedFilePath = path.resolve(logDirectory, path.basename(filePath));
    if (!_VibeLogger.validateWithin(logDirectory, sanitizedFilePath)) {
      throw new Error("Original file path escapes the allowed directory");
    }
    fs.renameSync(sanitizedFilePath, rotatedFilePath);
  }
  /**
   * Write log to file with retry mechanism for concurrent access
   */
  static async writeLogWithRetry(filePath, jsonLog) {
    const maxRetries = 3;
    const baseDelayMs = 50;
    const absoluteFilePath = path.resolve(filePath);
    const expectedLogDir = path.resolve(_VibeLogger.LOG_DIRECTORY);
    if (!_VibeLogger.validateWithin(expectedLogDir, absoluteFilePath)) {
      throw new Error("File path escapes log directory");
    }
    for (let retry = 0; retry < maxRetries; retry++) {
      try {
        const fileHandle = await fs.promises.open(absoluteFilePath, "a");
        try {
          await fileHandle.writeFile(jsonLog, { encoding: "utf8" });
        } finally {
          await fileHandle.close();
        }
        return;
      } catch (error) {
        if (_VibeLogger.isFileSharingViolation(error) && retry < maxRetries - 1) {
          const delayMs = baseDelayMs * Math.pow(2, retry);
          await _VibeLogger.sleep(delayMs);
        } else {
          throw error;
        }
      }
    }
  }
  /**
   * Save log entry to file with retry mechanism for concurrent access
   */
  static async saveLogToFile(logEntry) {
    try {
      const filePath = _VibeLogger.prepareLogFilePath();
      _VibeLogger.rotateLogFileIfNeeded(filePath);
      const jsonLog = JSON.stringify(logEntry) + "\n";
      await _VibeLogger.writeLogWithRetry(filePath, jsonLog);
    } catch (error) {
      await _VibeLogger.tryFallbackLogging(logEntry, error);
    }
  }
  /**
   * Try fallback logging when main logging fails
   */
  static async tryFallbackLogging(logEntry, error) {
    try {
      const basePath = process.cwd();
      const sanitizedRoot = path.resolve(basePath, OUTPUT_DIRECTORIES.ROOT);
      if (!_VibeLogger.validateWithin(basePath, sanitizedRoot)) {
        throw new Error("Invalid OUTPUT_DIRECTORIES.ROOT path");
      }
      const safeLogDir = path.resolve(sanitizedRoot, "FallbackLogs");
      if (!_VibeLogger.validateWithin(sanitizedRoot, safeLogDir)) {
        throw new Error("Fallback log directory path traversal detected");
      }
      fs.mkdirSync(safeLogDir, { recursive: true });
      const safeDate = _VibeLogger.formatDateTime().split(" ")[0].replace(/[^0-9-]/g, "");
      const safeFilename = `${_VibeLogger.LOG_FILE_PREFIX}_fallback_${safeDate}.json`;
      const safeFallbackPath = path.resolve(safeLogDir, safeFilename);
      if (!_VibeLogger.validateWithin(safeLogDir, safeFallbackPath)) {
        throw new Error("Invalid fallback log file path");
      }
      const fallbackEntry = {
        ...logEntry,
        fallback_reason: error instanceof Error ? error.message : String(error),
        original_timestamp: logEntry.timestamp
      };
      const jsonLog = JSON.stringify(fallbackEntry) + "\n";
      const fileHandle = await fs.promises.open(safeFallbackPath, "a");
      try {
        await fileHandle.writeFile(jsonLog, { encoding: "utf8" });
      } finally {
        await fileHandle.close();
      }
    } catch (fallbackError) {
      _VibeLogger.writeEmergencyLog({
        timestamp: _VibeLogger.formatTimestamp(),
        level: "EMERGENCY",
        message: "VibeLogger fallback failed",
        original_error: error instanceof Error ? error.message : String(error),
        fallback_error: fallbackError instanceof Error ? fallbackError.message : String(fallbackError),
        original_log_entry: logEntry
      });
    }
  }
  /**
   * Check if error is a file sharing violation
   */
  static isFileSharingViolation(error) {
    if (!error) {
      return false;
    }
    const sharingViolationCodes = [
      "EBUSY",
      // Resource busy or locked
      "EACCES",
      // Permission denied (can indicate file in use)
      "EPERM",
      // Operation not permitted
      "EMFILE",
      // Too many open files
      "ENFILE"
      // File table overflow
    ];
    return error && typeof error === "object" && "code" in error && typeof error.code === "string" && sharingViolationCodes.includes(error.code);
  }
  /**
   * Asynchronous sleep function for retry delays
   */
  static async sleep(ms) {
    return new Promise((resolve2) => setTimeout(resolve2, ms));
  }
  /**
   * Get current environment information
   */
  static getEnvironmentInfo() {
    const memUsage = process.memoryUsage();
    return {
      node_version: process.version,
      platform: process.platform,
      process_id: process.pid,
      memory_usage: {
        rss: memUsage.rss,
        heapTotal: memUsage.heapTotal,
        heapUsed: memUsage.heapUsed
      }
    };
  }
  /**
   * Sanitize context to prevent circular references
   */
  static sanitizeContext(context) {
    if (!context) {
      return void 0;
    }
    try {
      return JSON.parse(JSON.stringify(context));
    } catch (error) {
      return {
        error: "Failed to serialize context",
        original_type: typeof context,
        circular_reference: true
      };
    }
  }
  /**
   * Format timestamp for log entries (local timezone)
   */
  static formatTimestamp() {
    const now = /* @__PURE__ */ new Date();
    const offset = now.getTimezoneOffset();
    const offsetHours = String(Math.floor(Math.abs(offset) / 60)).padStart(2, "0");
    const offsetMinutes = String(Math.abs(offset) % 60).padStart(2, "0");
    const offsetSign = offset <= 0 ? "+" : "-";
    return now.toISOString().replace("Z", `${offsetSign}${offsetHours}:${offsetMinutes}`);
  }
  /**
   * Format date for file naming (local timezone)
   */
  static formatDate() {
    const now = /* @__PURE__ */ new Date();
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, "0");
    const day = String(now.getDate()).padStart(2, "0");
    return `${year}${month}${day}`;
  }
  /**
   * Format datetime for file rotation
   */
  static formatDateTime() {
    const now = /* @__PURE__ */ new Date();
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, "0");
    const day = String(now.getDate()).padStart(2, "0");
    const hours = String(now.getHours()).padStart(2, "0");
    const minutes = String(now.getMinutes()).padStart(2, "0");
    const seconds = String(now.getSeconds()).padStart(2, "0");
    return `${year}${month}${day}_${hours}${minutes}${seconds}`;
  }
  /**
   * Clean stack trace by removing VibeLogger internal calls and Error prefix
   */
  static cleanStackTrace(stack) {
    if (!stack) {
      return "";
    }
    const lines = stack.split("\n");
    const cleanedLines = [];
    for (const line of lines) {
      if (line.trim() === "Error") {
        continue;
      }
      if (line.includes("vibe-logger.ts") || line.includes("VibeLogger")) {
        continue;
      }
      cleanedLines.push(line);
    }
    return cleanedLines.join("\n").trim();
  }
};

// src/connection-manager.ts
var ConnectionManager = class {
  onReconnectedCallback = null;
  onConnectionLostCallback = null;
  /**
   * Set callback for when connection is restored
   */
  setReconnectedCallback(callback) {
    this.onReconnectedCallback = callback;
  }
  /**
   * Set callback for when connection is lost
   */
  setConnectionLostCallback(callback) {
    this.onConnectionLostCallback = callback;
  }
  /**
   * Trigger reconnection callback
   */
  triggerReconnected() {
    if (this.onReconnectedCallback) {
      try {
        this.onReconnectedCallback();
      } catch (error) {
        VibeLogger.logError(
          "connection_manager_reconnect_error",
          "Error in reconnection callback",
          { error }
        );
      }
    }
  }
  /**
   * Trigger connection lost callback
   */
  triggerConnectionLost() {
    if (this.onConnectionLostCallback) {
      try {
        this.onConnectionLostCallback();
      } catch (error) {
        VibeLogger.logError(
          "connection_manager_connection_lost_error",
          "Error in connection lost callback",
          { error }
        );
      }
    }
  }
  /**
   * Legacy method for backward compatibility - now does nothing
   * @deprecated Use UnityDiscovery for connection polling instead
   */
  startPolling(_connectFn) {
  }
  /**
   * Legacy method for backward compatibility - now does nothing
   * @deprecated Polling is now handled by UnityDiscovery
   */
  stopPolling() {
  }
};

// src/utils/content-length-framer.ts
var ContentLengthFramer = class _ContentLengthFramer {
  static CONTENT_LENGTH_HEADER = "Content-Length:";
  static HEADER_SEPARATOR = "\r\n\r\n";
  static LINE_SEPARATOR = "\r\n";
  static MAX_MESSAGE_SIZE = 1024 * 1024;
  // 1MB
  static ENCODING_UTF8 = "utf8";
  static LOG_PREFIX = "[ContentLengthFramer]";
  static ERROR_MESSAGE_JSON_EMPTY = "JSON content cannot be empty";
  static DEBUG_PREVIEW_LENGTH = 50;
  static PREVIEW_SUFFIX = "...";
  /**
   * Creates a Content-Length framed message from JSON content.
   * @param jsonContent The JSON content to frame
   * @returns The framed message with Content-Length header
   */
  static createFrame(jsonContent) {
    if (!jsonContent) {
      throw new Error(_ContentLengthFramer.ERROR_MESSAGE_JSON_EMPTY);
    }
    const contentLength = Buffer.byteLength(jsonContent, _ContentLengthFramer.ENCODING_UTF8);
    if (contentLength > _ContentLengthFramer.MAX_MESSAGE_SIZE) {
      throw new Error(
        `Message size ${contentLength} exceeds maximum allowed size ${_ContentLengthFramer.MAX_MESSAGE_SIZE}`
      );
    }
    return `${_ContentLengthFramer.CONTENT_LENGTH_HEADER} ${contentLength}${_ContentLengthFramer.HEADER_SEPARATOR}${jsonContent}`;
  }
  /**
   * Parses a Content-Length header from incoming data.
   * @param data The incoming data string
   * @returns Parse result with content length, header length, and completion status
   */
  static parseFrame(data) {
    if (!data) {
      return {
        contentLength: -1,
        headerLength: -1,
        isComplete: false
      };
    }
    try {
      const separatorIndex = data.indexOf(_ContentLengthFramer.HEADER_SEPARATOR);
      if (separatorIndex === -1) {
        return {
          contentLength: -1,
          headerLength: -1,
          isComplete: false
        };
      }
      const headerSection = data.substring(0, separatorIndex);
      const headerLength = separatorIndex + _ContentLengthFramer.HEADER_SEPARATOR.length;
      const contentLength = _ContentLengthFramer.parseContentLength(headerSection);
      if (contentLength === -1) {
        return {
          contentLength: -1,
          headerLength: -1,
          isComplete: false
        };
      }
      const expectedTotalLength = headerLength + contentLength;
      const actualByteLength = Buffer.byteLength(data, _ContentLengthFramer.ENCODING_UTF8);
      const isComplete = actualByteLength >= expectedTotalLength;
      VibeLogger.logDebug(
        "frame_analysis",
        `Frame analysis: dataLength=${data.length}, actualByteLength=${actualByteLength}, contentLength=${contentLength}, headerLength=${headerLength}, expectedTotal=${expectedTotalLength}, isComplete=${isComplete}`,
        {
          dataLength: data.length,
          actualByteLength,
          contentLength,
          headerLength,
          expectedTotalLength,
          isComplete
        }
      );
      return {
        contentLength,
        headerLength,
        isComplete
      };
    } catch (error) {
      VibeLogger.logError(
        "parse_frame_error",
        `Error parsing frame: ${error instanceof Error ? error.message : String(error)}`,
        {
          error: error instanceof Error ? {
            name: error.name,
            message: error.message,
            stack: error.stack
          } : { raw: String(error) }
        }
      );
      return {
        contentLength: -1,
        headerLength: -1,
        isComplete: false
      };
    }
  }
  /**
   * Parses a Content-Length header from incoming Buffer data.
   * @param data The incoming data Buffer
   * @returns Parse result with content length, header length, and completion status
   */
  static parseFrameFromBuffer(data) {
    if (!data || data.length === 0) {
      return {
        contentLength: -1,
        headerLength: -1,
        isComplete: false
      };
    }
    try {
      const separatorBuffer = Buffer.from(
        _ContentLengthFramer.HEADER_SEPARATOR,
        _ContentLengthFramer.ENCODING_UTF8
      );
      const separatorIndex = data.indexOf(separatorBuffer);
      if (separatorIndex === -1) {
        return {
          contentLength: -1,
          headerLength: -1,
          isComplete: false
        };
      }
      const headerSection = data.subarray(0, separatorIndex).toString(_ContentLengthFramer.ENCODING_UTF8);
      const headerLength = separatorIndex + separatorBuffer.length;
      const contentLength = _ContentLengthFramer.parseContentLength(headerSection);
      if (contentLength === -1) {
        return {
          contentLength: -1,
          headerLength: -1,
          isComplete: false
        };
      }
      const expectedTotalLength = headerLength + contentLength;
      const actualByteLength = data.length;
      const isComplete = actualByteLength >= expectedTotalLength;
      VibeLogger.logDebug(
        "frame_analysis_buffer",
        `Frame analysis: dataLength=${data.length}, actualByteLength=${actualByteLength}, contentLength=${contentLength}, headerLength=${headerLength}, expectedTotal=${expectedTotalLength}, isComplete=${isComplete}`,
        {
          dataLength: data.length,
          actualByteLength,
          contentLength,
          headerLength,
          expectedTotalLength,
          isComplete
        }
      );
      return {
        contentLength,
        headerLength,
        isComplete
      };
    } catch (error) {
      VibeLogger.logError(
        "parse_frame_buffer_error",
        `Error parsing frame: ${error instanceof Error ? error.message : String(error)}`,
        {
          error: error instanceof Error ? {
            name: error.name,
            message: error.message,
            stack: error.stack
          } : { raw: String(error) }
        }
      );
      return {
        contentLength: -1,
        headerLength: -1,
        isComplete: false
      };
    }
  }
  /**
   * Extracts a complete frame from the data buffer.
   * @param data The data buffer containing the frame
   * @param contentLength The expected content length
   * @param headerLength The header length including separators
   * @returns The extracted JSON content and remaining data
   */
  static extractFrame(data, contentLength, headerLength) {
    if (!data || contentLength < 0 || headerLength < 0) {
      return {
        jsonContent: null,
        remainingData: data || ""
      };
    }
    try {
      const expectedTotalLength = headerLength + contentLength;
      const actualByteLength = Buffer.byteLength(data, _ContentLengthFramer.ENCODING_UTF8);
      if (actualByteLength < expectedTotalLength) {
        return {
          jsonContent: null,
          remainingData: data
        };
      }
      const dataBuffer = Buffer.from(data, _ContentLengthFramer.ENCODING_UTF8);
      const jsonContent = dataBuffer.subarray(headerLength, headerLength + contentLength).toString(_ContentLengthFramer.ENCODING_UTF8);
      const remainingData = dataBuffer.subarray(expectedTotalLength).toString(_ContentLengthFramer.ENCODING_UTF8);
      return {
        jsonContent,
        remainingData
      };
    } catch (error) {
      VibeLogger.logError(
        "extract_frame_error",
        `Error extracting frame: ${error instanceof Error ? error.message : String(error)}`,
        {
          error: error instanceof Error ? {
            name: error.name,
            message: error.message,
            stack: error.stack
          } : { raw: String(error) }
        }
      );
      return {
        jsonContent: null,
        remainingData: data
      };
    }
  }
  /**
   * Extracts a complete frame from the Buffer data.
   * @param data The Buffer containing the frame
   * @param contentLength The expected content length
   * @param headerLength The header length including separators
   * @returns The extracted JSON content and remaining data
   */
  static extractFrameFromBuffer(data, contentLength, headerLength) {
    if (!data || data.length === 0 || contentLength < 0 || headerLength < 0) {
      return {
        jsonContent: null,
        remainingData: data || Buffer.alloc(0)
      };
    }
    try {
      const expectedTotalLength = headerLength + contentLength;
      if (data.length < expectedTotalLength) {
        return {
          jsonContent: null,
          remainingData: data
        };
      }
      const jsonContent = data.subarray(headerLength, headerLength + contentLength).toString(_ContentLengthFramer.ENCODING_UTF8);
      const remainingData = data.subarray(expectedTotalLength);
      return {
        jsonContent,
        remainingData
      };
    } catch (error) {
      VibeLogger.logError(
        "extract_frame_buffer_error",
        `Error extracting frame: ${error instanceof Error ? error.message : String(error)}`,
        {
          error: error instanceof Error ? {
            name: error.name,
            message: error.message,
            stack: error.stack
          } : { raw: String(error) }
        }
      );
      return {
        jsonContent: null,
        remainingData: data
      };
    }
  }
  /**
   * Validates that the Content-Length value is within acceptable limits.
   * @param contentLength The content length to validate
   * @returns True if the content length is valid, false otherwise
   */
  static isValidContentLength(contentLength) {
    return contentLength >= 0 && contentLength <= _ContentLengthFramer.MAX_MESSAGE_SIZE;
  }
  /**
   * Parses the Content-Length value from the header section.
   * @param headerSection The header section string
   * @returns The parsed content length, or -1 if parsing failed
   */
  static parseContentLength(headerSection) {
    if (!headerSection) {
      return -1;
    }
    const lines = headerSection.split(/\r?\n/);
    for (const line of lines) {
      const trimmedLine = line.trim();
      const lowerLine = trimmedLine.toLowerCase();
      if (lowerLine.startsWith("content-length:")) {
        const colonIndex = trimmedLine.indexOf(":");
        if (colonIndex === -1 || colonIndex >= trimmedLine.length - 1) {
          continue;
        }
        const valueString = trimmedLine.substring(colonIndex + 1).trim();
        const parsedValue = parseInt(valueString, 10);
        if (isNaN(parsedValue)) {
          return -1;
        }
        if (!_ContentLengthFramer.isValidContentLength(parsedValue)) {
          return -1;
        }
        return parsedValue;
      }
    }
    return -1;
  }
};

// src/utils/dynamic-buffer.ts
var DynamicBuffer = class _DynamicBuffer {
  // 定数定義
  static ENCODING_UTF8 = "utf8";
  static HEADER_SEPARATOR = "\r\n\r\n";
  static CONTENT_LENGTH_HEADER = "content-length:";
  static LOG_PREFIX = "[DynamicBuffer]";
  static PREVIEW_SUFFIX = "...";
  static PREVIEW_LENGTH_FRAME_ERROR = 200;
  static LARGE_BUFFER_THRESHOLD = 1024;
  static BUFFER_UTILIZATION_THRESHOLD = 0.8;
  static DEFAULT_PREVIEW_LENGTH = 100;
  static STATS_PREVIEW_LENGTH = 50;
  buffer = Buffer.alloc(0);
  maxBufferSize;
  initialBufferSize;
  constructor(maxBufferSize = 1024 * 1024, initialBufferSize = 4096) {
    this.maxBufferSize = maxBufferSize;
    this.initialBufferSize = initialBufferSize;
  }
  /**
   * Appends new data to the buffer.
   * @param data The data to append (Buffer or string)
   * @throws Error if buffer would exceed maximum size
   */
  append(data) {
    if (!data) {
      return;
    }
    const dataBuffer = Buffer.isBuffer(data) ? data : Buffer.from(data, _DynamicBuffer.ENCODING_UTF8);
    const newSize = this.buffer.length + dataBuffer.length;
    if (newSize > this.maxBufferSize) {
      throw new Error(
        `Buffer size would exceed maximum allowed size: ${newSize} > ${this.maxBufferSize}`
      );
    }
    this.buffer = Buffer.concat([this.buffer, dataBuffer]);
  }
  /**
   * Attempts to extract a complete frame from the buffer.
   * @returns The extracted frame and whether extraction was successful
   */
  extractFrame() {
    if (!this.buffer) {
      return { frame: null, extracted: false };
    }
    try {
      const parseResult = ContentLengthFramer.parseFrameFromBuffer(this.buffer);
      if (!parseResult.isComplete) {
        return { frame: null, extracted: false };
      }
      const extractionResult = ContentLengthFramer.extractFrameFromBuffer(
        this.buffer,
        parseResult.contentLength,
        parseResult.headerLength
      );
      if (!extractionResult.jsonContent) {
        return { frame: null, extracted: false };
      }
      this.buffer = Buffer.isBuffer(extractionResult.remainingData) ? extractionResult.remainingData : Buffer.from(extractionResult.remainingData, _DynamicBuffer.ENCODING_UTF8);
      return {
        frame: extractionResult.jsonContent,
        extracted: true
      };
    } catch (error) {
      return { frame: null, extracted: false };
    }
  }
  /**
   * Extracts all available complete frames from the buffer.
   * @returns Array of extracted JSON frames
   */
  extractAllFrames() {
    const frames = [];
    while (this.buffer.length > 0) {
      const result = this.extractFrame();
      if (!result.extracted || !result.frame) {
        break;
      }
      frames.push(result.frame);
    }
    return frames;
  }
  /**
   * Checks if the buffer has any data.
   * @returns True if buffer contains data, false otherwise
   */
  hasData() {
    return this.buffer.length > 0;
  }
  /**
   * Gets the current buffer size.
   * @returns The current buffer size in characters
   */
  getSize() {
    return this.buffer.length;
  }
  /**
   * Checks if a complete frame might be available.
   * This is a quick check without full parsing.
   * @returns True if header separator is found, false otherwise
   */
  hasCompleteFrameHeader() {
    return this.buffer.includes(
      Buffer.from(_DynamicBuffer.HEADER_SEPARATOR, _DynamicBuffer.ENCODING_UTF8)
    );
  }
  /**
   * Clears the buffer.
   */
  clear() {
    this.buffer = Buffer.alloc(0);
  }
  /**
   * Gets a preview of the buffer content for debugging.
   * @param maxLength Maximum length of preview (default: 100)
   * @returns Truncated buffer content for debugging
   */
  getPreview(maxLength = _DynamicBuffer.DEFAULT_PREVIEW_LENGTH) {
    if (this.buffer.length <= maxLength) {
      return this.buffer.toString(_DynamicBuffer.ENCODING_UTF8);
    }
    return this.buffer.subarray(0, maxLength).toString(_DynamicBuffer.ENCODING_UTF8) + _DynamicBuffer.PREVIEW_SUFFIX;
  }
  /**
   * Validates the buffer state and performs cleanup if necessary.
   * @returns True if buffer is in valid state, false if cleanup was performed
   */
  validateAndCleanup() {
    if (this.buffer.length > this.maxBufferSize * _DynamicBuffer.BUFFER_UTILIZATION_THRESHOLD && !this.hasCompleteFrameHeader()) {
      this.clear();
      return false;
    }
    const headerSeparatorBuffer = Buffer.from(
      _DynamicBuffer.HEADER_SEPARATOR,
      _DynamicBuffer.ENCODING_UTF8
    );
    const headerSeparatorIndex = this.buffer.indexOf(headerSeparatorBuffer);
    if (headerSeparatorIndex === -1 && this.buffer.length > _DynamicBuffer.LARGE_BUFFER_THRESHOLD) {
      const contentLengthBuffer = Buffer.from(
        _DynamicBuffer.CONTENT_LENGTH_HEADER,
        _DynamicBuffer.ENCODING_UTF8
      );
      const contentLengthIndex = this.buffer.indexOf(contentLengthBuffer);
      if (contentLengthIndex === -1) {
        this.clear();
        return false;
      }
    }
    return true;
  }
  /**
   * Gets buffer statistics for monitoring and debugging.
   * @returns Buffer statistics object
   */
  getStats() {
    return {
      size: this.buffer.length,
      maxSize: this.maxBufferSize,
      utilization: this.buffer.length / this.maxBufferSize * 100,
      hasCompleteHeader: this.hasCompleteFrameHeader(),
      preview: this.getPreview(_DynamicBuffer.STATS_PREVIEW_LENGTH)
    };
  }
};

// src/message-handler.ts
var JsonRpcErrorTypes = {
  SECURITY_BLOCKED: "security_blocked",
  INTERNAL_ERROR: "internal_error"
};
var isJsonRpcNotification = (msg) => {
  return typeof msg === "object" && msg !== null && "method" in msg && typeof msg.method === "string" && !("id" in msg);
};
var isJsonRpcResponse = (msg) => {
  return typeof msg === "object" && msg !== null && "id" in msg && typeof msg.id === "string" && !("method" in msg);
};
var hasValidId = (msg) => {
  return typeof msg === "object" && msg !== null && "id" in msg && typeof msg.id === "string";
};
var MessageHandler = class {
  notificationHandlers = /* @__PURE__ */ new Map();
  pendingRequests = /* @__PURE__ */ new Map();
  // Content-Length framing components
  dynamicBuffer = new DynamicBuffer();
  /**
   * Register notification handler for specific method
   */
  onNotification(method, handler) {
    this.notificationHandlers.set(method, handler);
  }
  /**
   * Remove notification handler
   */
  offNotification(method) {
    this.notificationHandlers.delete(method);
  }
  /**
   * Register a pending request
   */
  registerPendingRequest(id, resolve2, reject) {
    this.pendingRequests.set(id, { resolve: resolve2, reject, timestamp: Date.now() });
  }
  /**
   * Handle incoming data from Unity using Content-Length framing
   */
  handleIncomingData(data) {
    this.dynamicBuffer.append(data);
    const frames = this.dynamicBuffer.extractAllFrames();
    for (const frame of frames) {
      if (!frame || frame.trim() === "") {
        continue;
      }
      try {
        const message = JSON.parse(frame);
        if (isJsonRpcNotification(message)) {
          this.handleNotification(message);
        } else if (isJsonRpcResponse(message)) {
          this.handleResponse(message);
        } else if (hasValidId(message)) {
          this.handleResponse(message);
        }
      } catch (parseError) {
        VibeLogger.logError(
          "json_parse_error",
          "Error parsing JSON frame",
          {
            error: parseError instanceof Error ? parseError.message : String(parseError),
            frame: frame.substring(0, 200)
            // Truncate for security
          },
          void 0,
          "Invalid JSON received from Unity, frame may be corrupted"
        );
      }
    }
  }
  /**
   * Handle notification from Unity
   */
  handleNotification(notification) {
    const { method, params } = notification;
    const handler = this.notificationHandlers.get(method);
    if (handler) {
      try {
        handler(params);
      } catch (error) {
        VibeLogger.logError(
          "notification_handler_error",
          `Error in notification handler for ${method}`,
          { error: error instanceof Error ? error.message : String(error) },
          void 0,
          "Exception occurred while processing notification"
        );
      }
    }
  }
  /**
   * Handle response from Unity
   */
  handleResponse(response) {
    const { id } = response;
    const pending = this.pendingRequests.get(id);
    if (pending) {
      this.pendingRequests.delete(id);
      if (response.error) {
        let errorMessage = response.error.message || "Unknown error";
        if (response.error.data?.type === JsonRpcErrorTypes.SECURITY_BLOCKED) {
          const data = response.error.data;
          errorMessage = `${data.reason || errorMessage}`;
          if (data.command) {
            errorMessage += ` (Command: ${data.command})`;
          }
          errorMessage += " To use this feature, enable the corresponding option in Unity menu: Window > uLoopMCP > Security Settings";
        }
        pending.reject(new Error(errorMessage));
      } else {
        pending.resolve(response);
      }
    } else {
      const activeRequestIds = Array.from(this.pendingRequests.keys()).join(", ");
      const currentTime = Date.now();
      VibeLogger.logWarning(
        "unknown_request_response",
        `Received response for unknown request ID: ${id}`,
        {
          unknown_request_id: id,
          active_request_ids: activeRequestIds,
          current_time: currentTime
        },
        void 0,
        "This may be a delayed response from before reconnection",
        "Monitor if this pattern indicates connection stability issues"
      );
    }
  }
  /**
   * Clear all pending requests (used during disconnect)
   */
  clearPendingRequests(reason) {
    for (const [, pending] of this.pendingRequests) {
      pending.reject(new Error(reason));
    }
    this.pendingRequests.clear();
  }
  /**
   * Create JSON-RPC request with Content-Length framing
   */
  createRequest(method, params, id) {
    const request = {
      jsonrpc: JSONRPC.VERSION,
      id,
      method,
      params
    };
    const jsonContent = JSON.stringify(request);
    return ContentLengthFramer.createFrame(jsonContent);
  }
  /**
   * Clear the dynamic buffer (for connection reset)
   */
  clearBuffer() {
    this.dynamicBuffer.clear();
  }
  /**
   * Get buffer statistics for debugging
   */
  getBufferStats() {
    return this.dynamicBuffer.getStats();
  }
};

// src/unity-client.ts
var createSafeTimeout = (callback, delay) => {
  return safeSetTimeout(callback, delay);
};
var UnityClient = class _UnityClient {
  static MAX_COUNTER = 9999;
  static COUNTER_PADDING = 4;
  static instance = null;
  socket = null;
  _connected = false;
  port;
  host = UNITY_CONNECTION.DEFAULT_HOST;
  reconnectHandlers = /* @__PURE__ */ new Set();
  connectionManager = new ConnectionManager();
  messageHandler = new MessageHandler();
  unityDiscovery = null;
  // Reference to UnityDiscovery for connection loss handling
  requestIdCounter = 0;
  // Will be incremented to 1 on first use
  processId = process.pid;
  randomSeed = Math.floor(Math.random() * 1e3);
  storedClientName = null;
  isConnecting = false;
  connectingPromise = null;
  constructor() {
    const unityTcpPort = process.env.UNITY_TCP_PORT;
    if (!unityTcpPort) {
      throw new Error("UNITY_TCP_PORT environment variable is required but not set");
    }
    const parsedPort = parseInt(unityTcpPort, 10);
    if (isNaN(parsedPort) || parsedPort <= 0 || parsedPort > 65535) {
      throw new Error(`UNITY_TCP_PORT must be a valid port number (1-65535), got: ${unityTcpPort}`);
    }
    this.port = parsedPort;
  }
  /**
   * Get the singleton instance of UnityClient
   */
  static getInstance() {
    if (!_UnityClient.instance) {
      _UnityClient.instance = new _UnityClient();
    }
    return _UnityClient.instance;
  }
  /**
   * Reset the singleton instance (for testing purposes)
   */
  static resetInstance() {
    if (_UnityClient.instance) {
      _UnityClient.instance.disconnect();
      _UnityClient.instance.storedClientName = null;
      _UnityClient.instance = null;
    }
  }
  /**
   * Update Unity connection port (for discovery)
   */
  updatePort(newPort) {
    this.port = newPort;
  }
  /**
   * Set Unity Discovery reference for connection loss handling
   */
  setUnityDiscovery(unityDiscovery) {
    this.unityDiscovery = unityDiscovery;
  }
  get connected() {
    return this._connected && this.socket !== null && !this.socket.destroyed;
  }
  /**
   * Register notification handler for specific method
   */
  onNotification(method, handler) {
    this.messageHandler.onNotification(method, handler);
  }
  /**
   * Remove notification handler
   */
  offNotification(method) {
    this.messageHandler.offNotification(method);
  }
  /**
   * Register reconnect handler
   */
  onReconnect(handler) {
    this.reconnectHandlers.add(handler);
  }
  /**
   * Remove reconnect handler
   */
  offReconnect(handler) {
    this.reconnectHandlers.delete(handler);
  }
  /**
   * Lightweight connection health check
   * Tests socket state without creating new connections
   */
  async testConnection() {
    if (!this._connected || this.socket === null || this.socket.destroyed) {
      return false;
    }
    if (!this.socket.readable || !this.socket.writable) {
      this._connected = false;
      return false;
    }
    let timeoutTimer = null;
    try {
      await Promise.race([
        this.ping(UNITY_CONNECTION.CONNECTION_TEST_MESSAGE),
        new Promise((_resolve, reject) => {
          timeoutTimer = createSafeTimeout(() => {
            reject(new Error("Health check timeout"));
          }, 1e3);
        })
      ]);
    } catch (error) {
      return false;
    } finally {
      stopSafeTimer(timeoutTimer);
      timeoutTimer = null;
    }
    return true;
  }
  /**
   * Ensure connection to Unity (singleton-safe reconnection)
   * Properly manages single connection instance
   */
  async ensureConnected() {
    if (this._connected && this.socket && !this.socket.destroyed) {
      try {
        if (await this.testConnection()) {
          return;
        }
      } catch (error) {
      }
    }
    if (this.connectingPromise) {
      await this.connectingPromise;
      return;
    }
    this.connectingPromise = (async () => {
      this.isConnecting = true;
      this.disconnect();
      await this.connect();
    })().catch((error) => {
      VibeLogger.logError("unity_connect_failed", "Unity connect attempt failed", {
        message: error instanceof Error ? error.message : String(error)
      });
      throw error;
    }).finally(() => {
      this.isConnecting = false;
      this.connectingPromise = null;
    });
    await this.connectingPromise;
  }
  /**
   * Connect to Unity
   * Creates a new socket connection (should only be called after disconnect)
   */
  async connect() {
    if (this._connected && this.socket && !this.socket.destroyed) {
      return;
    }
    return new Promise((resolve2, reject) => {
      this.socket = new net.Socket();
      const currentSocket = this.socket;
      let connectionEstablished = false;
      let promiseSettled = false;
      const finalizeInitialFailure = (error, logCode, logMessage) => {
        if (promiseSettled) {
          return;
        }
        promiseSettled = true;
        VibeLogger.logError(logCode, logMessage, { message: error.message });
        if (!currentSocket.destroyed) {
          currentSocket.destroy();
        }
        if (this.socket === currentSocket) {
          this.socket = null;
        }
        reject(error);
      };
      currentSocket.connect(this.port, this.host, () => {
        this._connected = true;
        connectionEstablished = true;
        promiseSettled = true;
        this.reconnectHandlers.forEach((handler) => {
          try {
            handler();
          } catch (error) {
            VibeLogger.logError(
              "unity_reconnect_handler_error",
              "Unity reconnect handler threw an error",
              {
                message: error instanceof Error ? error.message : String(error),
                stack: error instanceof Error ? error.stack : void 0
              }
            );
          }
        });
        resolve2();
      });
      currentSocket.on("error", (error) => {
        this._connected = false;
        if (!connectionEstablished) {
          finalizeInitialFailure(
            new Error(`Unity connection failed: ${error.message}`),
            "unity_connect_attempt_failed",
            "Unity socket error during connection attempt"
          );
          return;
        }
        VibeLogger.logError("unity_socket_error", "Unity socket error", {
          message: error.message
        });
        this.handleConnectionLoss();
      });
      currentSocket.on("close", () => {
        this._connected = false;
        if (!connectionEstablished) {
          finalizeInitialFailure(
            new Error("Unity connection closed before being established"),
            "unity_connect_closed_pre_handshake",
            "Unity socket closed during connection attempt"
          );
          return;
        }
        this.handleConnectionLoss();
      });
      currentSocket.on("end", () => {
        this._connected = false;
        if (!connectionEstablished) {
          finalizeInitialFailure(
            new Error("Unity connection ended before being established"),
            "unity_connect_end_pre_handshake",
            "Unity socket ended during connection attempt"
          );
          return;
        }
        this.handleConnectionLoss();
      });
      currentSocket.on("data", (data) => {
        this.messageHandler.handleIncomingData(data);
      });
    });
  }
  /**
   * Detect client name from stored value, environment variables, or default
   */
  detectClientName() {
    if (this.storedClientName) {
      return this.storedClientName;
    }
    return process.env.MCP_CLIENT_NAME || DEFAULT_CLIENT_NAME;
  }
  /**
   * Send client name to Unity for identification
   */
  async setClientName(clientName) {
    if (!this.connected) {
      return;
    }
    if (clientName) {
      this.storedClientName = clientName;
    }
    const finalClientName = clientName || this.detectClientName();
    const request = {
      jsonrpc: JSONRPC.VERSION,
      id: this.generateId(),
      method: "set-client-name",
      params: {
        ClientName: finalClientName
      }
    };
    try {
      const response = await this.sendRequest(request);
      if (response.error) {
      }
    } catch (error) {
    }
  }
  /**
   * Send ping to Unity
   */
  async ping(message) {
    if (!this.connected) {
      throw new Error("Not connected to Unity");
    }
    const request = {
      jsonrpc: JSONRPC.VERSION,
      id: this.generateId(),
      method: "ping",
      params: {
        Message: message
        // Updated to match PingSchema property name
      }
    };
    const response = await this.sendRequest(request, 1e3);
    if (response.error) {
      throw new Error(`Unity error: ${response.error.message}`);
    }
    return response.result || { Message: "Unity pong" };
  }
  /**
   * Get available tools from Unity
   */
  async getAvailableTools() {
    await this.ensureConnected();
    const request = {
      jsonrpc: JSONRPC.VERSION,
      id: this.generateId(),
      method: "getAvailableTools",
      params: {}
    };
    const response = await this.sendRequest(request);
    if (response.error) {
      throw new Error(`Failed to get available tools: ${response.error.message}`);
    }
    return response.result || [];
  }
  /**
   * Get tool details from Unity
   */
  async getToolDetails(includeDevelopmentOnly = false) {
    await this.ensureConnected();
    const request = {
      jsonrpc: JSONRPC.VERSION,
      id: this.generateId(),
      method: "get-tool-details",
      params: { IncludeDevelopmentOnly: includeDevelopmentOnly }
    };
    const response = await this.sendRequest(request);
    if (response.error) {
      throw new Error(`Failed to get tool details: ${response.error.message}`);
    }
    return response.result || [];
  }
  /**
   * Execute any Unity tool dynamically
   */
  async executeTool(toolName, params = {}) {
    if (!this.connected) {
      return this.getOsSpecificReconnectMessage();
    }
    await this.setClientName();
    const request = {
      jsonrpc: JSONRPC.VERSION,
      id: this.generateId(),
      method: toolName,
      params
    };
    const timeoutMs = TIMEOUTS.NETWORK;
    try {
      const response = await this.sendRequest(request, timeoutMs);
      return this.handleToolResponse(response);
    } catch (error) {
      if (error instanceof Error && error.message.includes("timed out")) {
      }
      throw error;
    }
  }
  /**
   * Build a guidance message for temporary disconnection after compile.
   */
  getOsSpecificReconnectMessage() {
    const baseMessage = "Waiting for Unity to be ready (normal during compilation). Wait 3 seconds then retry. If still not ready after several attempts, increase wait time (5 \u2192 10 seconds). Report as error only after 1+ minute of failures.";
    const platform = typeof process !== "undefined" && typeof process.platform === "string" ? process.platform : "unknown";
    if (platform === "win32") {
      return `${baseMessage} Example: Start-Sleep -Seconds 3`;
    }
    return `${baseMessage} Example: sleep 3`;
  }
  handleToolResponse(response) {
    if (response.error) {
      throw new Error(response.error.message);
    }
    return response.result;
  }
  /**
   * Generate unique request ID as string
   * Uses timestamp + process ID + random seed + counter for guaranteed uniqueness across processes
   */
  generateId() {
    if (this.requestIdCounter >= _UnityClient.MAX_COUNTER) {
      this.requestIdCounter = 1;
    } else {
      this.requestIdCounter++;
    }
    const timestamp = Date.now();
    const processId = this.processId;
    const randomSeed = this.randomSeed;
    const counter = this.requestIdCounter.toString().padStart(_UnityClient.COUNTER_PADDING, "0");
    return `ts_${timestamp}_${processId}_${randomSeed}_${counter}`;
  }
  /**
   * Send request and wait for response
   */
  async sendRequest(request, timeoutMs) {
    return new Promise((resolve2, reject) => {
      const timeout_duration = timeoutMs || TIMEOUTS.NETWORK;
      const timeoutTimer = safeSetTimeout(() => {
        this.messageHandler.clearPendingRequests(`Request ${ERROR_MESSAGES.TIMEOUT}`);
        reject(new Error(`Request ${ERROR_MESSAGES.TIMEOUT}`));
      }, timeout_duration);
      this.messageHandler.registerPendingRequest(
        request.id,
        (response) => {
          stopSafeTimer(timeoutTimer);
          resolve2(response);
        },
        (error) => {
          stopSafeTimer(timeoutTimer);
          reject(error);
        }
      );
      const requestStr = this.messageHandler.createRequest(
        request.method,
        request.params,
        request.id
      );
      if (this.socket) {
        this.socket.write(requestStr);
      }
    });
  }
  /**
   * Disconnect
   *
   * IMPORTANT: Always clean up timers when disconnecting!
   * Failure to properly clean up timers can cause orphaned processes
   * that prevent Node.js from exiting gracefully.
   */
  disconnect() {
    this.connectionManager.stopPolling();
    this.messageHandler.clearPendingRequests("Connection closed");
    this.messageHandler.clearBuffer();
    this.requestIdCounter = 0;
    if (this.socket) {
      this.socket.destroy();
      this.socket = null;
    }
    this._connected = false;
  }
  /**
   * Handle connection loss by delegating to UnityDiscovery
   */
  handleConnectionLoss() {
    this.connectionManager.triggerConnectionLost();
    if (this.unityDiscovery) {
      this.unityDiscovery.handleConnectionLost();
    }
  }
  /**
   * Set callback for when connection is restored
   */
  setReconnectedCallback(callback) {
    this.connectionManager.setReconnectedCallback(callback);
  }
  /**
   * Fetch tool details from Unity with development mode support
   */
  async fetchToolDetailsFromUnity(includeDevelopmentOnly = false) {
    const params = { IncludeDevelopmentOnly: includeDevelopmentOnly };
    const toolDetailsResponse = await this.executeTool("get-tool-details", params);
    const toolDetails = toolDetailsResponse?.Tools || toolDetailsResponse;
    if (!Array.isArray(toolDetails)) {
      return null;
    }
    return toolDetails;
  }
  /**
   * Check if Unity is available on specific port
   * Performs low-level TCP connection test with short timeout
   */
  static async isUnityAvailable(port) {
    return new Promise((resolve2) => {
      const socket = new net.Socket();
      const timeout = 500;
      const timer = setTimeout(() => {
        socket.destroy();
        resolve2(false);
      }, timeout);
      socket.connect(port, UNITY_CONNECTION.DEFAULT_HOST, () => {
        clearTimeout(timer);
        socket.destroy();
        resolve2(true);
      });
      socket.on("error", () => {
        clearTimeout(timer);
        resolve2(false);
      });
    });
  }
};

// src/unity-discovery.ts
var UnityDiscovery = class _UnityDiscovery {
  discoveryInterval = null;
  unityClient;
  onDiscoveredCallback = null;
  onConnectionLostCallback = null;
  isDiscovering = false;
  isDevelopment = false;
  discoveryAttemptCount = 0;
  // Singleton pattern to prevent multiple instances
  static instance = null;
  static activeTimerCount = 0;
  // Track active timers for debugging
  constructor(unityClient) {
    this.unityClient = unityClient;
    this.isDevelopment = process.env.NODE_ENV === "development";
  }
  /**
   * Get singleton instance of UnityDiscovery
   */
  static getInstance(unityClient) {
    if (!_UnityDiscovery.instance) {
      _UnityDiscovery.instance = new _UnityDiscovery(unityClient);
    }
    return _UnityDiscovery.instance;
  }
  /**
   * Set callback for when Unity is discovered
   */
  setOnDiscoveredCallback(callback) {
    this.onDiscoveredCallback = callback;
  }
  /**
   * Set callback for when connection is lost
   */
  setOnConnectionLostCallback(callback) {
    this.onConnectionLostCallback = callback;
  }
  /**
   * Check if discovery is currently running
   */
  getIsDiscovering() {
    return this.isDiscovering;
  }
  /**
   * Get current polling interval based on attempt count
   */
  getCurrentPollingInterval() {
    const currentInterval = this.discoveryAttemptCount <= POLLING.INITIAL_ATTEMPTS ? POLLING.INITIAL_INTERVAL_MS : POLLING.EXTENDED_INTERVAL_MS;
    if (this.discoveryAttemptCount === POLLING.INITIAL_ATTEMPTS + 1) {
      VibeLogger.logInfo(
        "unity_discovery_polling_switch",
        "Switching from initial to extended polling interval",
        {
          previous_interval_ms: POLLING.INITIAL_INTERVAL_MS,
          new_interval_ms: POLLING.EXTENDED_INTERVAL_MS,
          discovery_attempt_count: this.discoveryAttemptCount,
          switch_threshold: POLLING.INITIAL_ATTEMPTS
        },
        void 0,
        "Polling interval changed to reduce CPU usage after initial attempts",
        "Monitor if this reduces system load while maintaining connection reliability"
      );
    }
    return currentInterval;
  }
  /**
   * Schedule next discovery attempt with adaptive interval
   */
  scheduleNextDiscovery() {
    const currentInterval = this.getCurrentPollingInterval();
    const isInitialPolling = this.discoveryAttemptCount <= POLLING.INITIAL_ATTEMPTS;
    VibeLogger.logDebug(
      "unity_discovery_scheduling_next",
      "Scheduling next discovery attempt",
      {
        next_interval_ms: currentInterval,
        discovery_attempt_count: this.discoveryAttemptCount,
        is_initial_polling: isInitialPolling,
        polling_type: isInitialPolling ? "initial_fast" : "extended_slow"
      },
      void 0,
      `Next discovery scheduled in ${currentInterval}ms`
    );
    this.discoveryInterval = setTimeout(() => {
      void this.unifiedDiscoveryAndConnectionCheck();
      if (this.discoveryInterval) {
        this.scheduleNextDiscovery();
      }
    }, currentInterval);
  }
  /**
   * Start Unity discovery polling with unified connection management
   */
  start() {
    if (this.discoveryInterval) {
      return;
    }
    if (this.isDiscovering) {
      return;
    }
    this.discoveryAttemptCount = 0;
    void this.unifiedDiscoveryAndConnectionCheck();
    this.scheduleNextDiscovery();
    _UnityDiscovery.activeTimerCount++;
  }
  /**
   * Stop Unity discovery polling
   */
  stop() {
    if (this.discoveryInterval) {
      clearTimeout(this.discoveryInterval);
      this.discoveryInterval = null;
    }
    this.isDiscovering = false;
    this.discoveryAttemptCount = 0;
    _UnityDiscovery.activeTimerCount = Math.max(0, _UnityDiscovery.activeTimerCount - 1);
  }
  /**
   * Force reset discovery state (for debugging and recovery)
   */
  forceResetDiscoveryState() {
    this.isDiscovering = false;
    VibeLogger.logWarning(
      "unity_discovery_state_force_reset",
      "Discovery state forcibly reset",
      { was_discovering: true },
      void 0,
      "Manual recovery from stuck discovery state"
    );
  }
  /**
   * Unified discovery and connection checking
   * Handles both Unity discovery and connection health monitoring
   */
  async unifiedDiscoveryAndConnectionCheck() {
    const correlationId = VibeLogger.generateCorrelationId();
    if (this.isDiscovering) {
      VibeLogger.logDebug(
        "unity_discovery_skip_in_progress",
        "Discovery already in progress - skipping",
        { is_discovering: true, active_timer_count: _UnityDiscovery.activeTimerCount },
        correlationId,
        "Another discovery cycle is already running - this is normal behavior."
      );
      return;
    }
    this.logDiscoveryCycleStart(correlationId);
    try {
      const shouldContinueDiscovery = await this.handleConnectionHealthCheck(correlationId);
      if (!shouldContinueDiscovery) {
        return;
      }
      await this.executeUnityDiscovery(correlationId);
    } catch (error) {
      VibeLogger.logError(
        "unity_discovery_cycle_error",
        "Discovery cycle encountered error",
        {
          error_message: error instanceof Error ? error.message : String(error),
          is_discovering: this.isDiscovering,
          correlation_id: correlationId
        },
        correlationId,
        "Discovery cycle failed - forcing state reset to prevent hang"
      );
    } finally {
      this.finalizeCycleWithCleanup(correlationId);
    }
  }
  /**
   * Log discovery cycle initialization
   */
  logDiscoveryCycleStart(correlationId) {
    this.isDiscovering = true;
    this.discoveryAttemptCount++;
    const currentInterval = this.getCurrentPollingInterval();
    VibeLogger.logDebug(
      "unity_discovery_state_set",
      "Discovery state set to true",
      { is_discovering: true, correlation_id: correlationId },
      correlationId,
      "Discovery state flag set - starting cycle"
    );
    VibeLogger.logInfo(
      "unity_discovery_cycle_start",
      "Starting unified discovery and connection check cycle",
      {
        unity_connected: this.unityClient.connected,
        polling_interval_ms: currentInterval,
        discovery_attempt_count: this.discoveryAttemptCount,
        is_initial_polling: this.discoveryAttemptCount <= POLLING.INITIAL_ATTEMPTS,
        active_timer_count: _UnityDiscovery.activeTimerCount
      },
      correlationId,
      "This cycle checks connection health and attempts Unity discovery if needed."
    );
  }
  /**
   * Handle connection health check and determine if discovery should continue
   */
  async handleConnectionHealthCheck(correlationId) {
    if (this.unityClient.connected) {
      const isConnectionHealthy = await this.checkConnectionHealth();
      if (isConnectionHealthy) {
        VibeLogger.logInfo(
          "unity_discovery_connection_healthy",
          "Connection is healthy - stopping discovery",
          { connection_healthy: true },
          correlationId
        );
        this.stop();
        return false;
      } else {
        VibeLogger.logWarning(
          "unity_discovery_connection_unhealthy",
          "Connection appears unhealthy - continuing discovery without assuming loss",
          { connection_healthy: false },
          correlationId,
          "Connection health check failed. Will continue discovery but not assume complete loss.",
          "Connection may recover on next cycle. Monitor for persistent issues."
        );
      }
    }
    return true;
  }
  /**
   * Execute Unity discovery with timeout protection
   */
  async executeUnityDiscovery(_correlationId) {
    await Promise.race([
      this.discoverUnityOnPorts(),
      new Promise(
        (_, reject) => setTimeout(() => reject(new Error("Unity discovery timeout - 5 seconds")), 5e3)
      )
    ]);
  }
  /**
   * Finalize discovery cycle with cleanup and logging
   */
  finalizeCycleWithCleanup(correlationId) {
    VibeLogger.logDebug(
      "unity_discovery_cycle_end",
      "Discovery cycle completed - resetting state",
      {
        is_discovering_before: this.isDiscovering,
        is_discovering_after: false,
        correlation_id: correlationId
      },
      correlationId,
      "Discovery cycle finished and state reset to prevent hang",
      void 0,
      true
    );
    this.isDiscovering = false;
  }
  /**
   * Check if the current connection is healthy with timeout protection
   */
  async checkConnectionHealth() {
    try {
      const healthCheck = await Promise.race([
        this.unityClient.testConnection(),
        new Promise(
          (_, reject) => setTimeout(() => reject(new Error("Connection health check timeout")), 1e3)
        )
      ]);
      return healthCheck;
    } catch (error) {
      return false;
    }
  }
  /**
   * Discover Unity by checking specified port
   */
  async discoverUnityOnPorts() {
    const correlationId = VibeLogger.generateCorrelationId();
    const unityTcpPort = process.env.UNITY_TCP_PORT;
    if (!unityTcpPort) {
      throw new Error("UNITY_TCP_PORT environment variable is required but not set");
    }
    const port = parseInt(unityTcpPort, 10);
    if (isNaN(port) || port <= 0 || port > 65535) {
      throw new Error(`UNITY_TCP_PORT must be a valid port number (1-65535), got: ${unityTcpPort}`);
    }
    VibeLogger.logInfo(
      "unity_discovery_port_scan_start",
      "Starting Unity port discovery scan",
      {
        target_port: port
      },
      correlationId,
      "Checking specified port for Unity MCP server."
    );
    try {
      if (await UnityClient.isUnityAvailable(port)) {
        VibeLogger.logInfo(
          "unity_discovery_success",
          "Unity discovered and connection established",
          {
            discovered_port: port
          },
          correlationId,
          "Unity MCP server found and connection established successfully.",
          "Monitor for tools/list_changed notifications after this discovery."
        );
        this.unityClient.updatePort(port);
        if (this.onDiscoveredCallback) {
          await this.onDiscoveredCallback(port);
        }
        return;
      }
    } catch (error) {
      VibeLogger.logDebug(
        "unity_discovery_port_check_failed",
        "Unity availability check failed for specified port",
        {
          target_port: port,
          error_message: error instanceof Error ? error.message : String(error),
          error_type: error instanceof Error ? error.constructor.name : typeof error
        },
        correlationId,
        "Expected failure when Unity is not running on this port. Will continue polling.",
        "This is normal during Unity startup or when Unity is not running."
      );
    }
    VibeLogger.logWarning(
      "unity_discovery_no_unity_found",
      "No Unity server found on specified port",
      {
        target_port: port
      },
      correlationId,
      "Unity MCP server not found on the specified port. Unity may not be running or using a different port.",
      "Check Unity console for MCP server status and verify port configuration."
    );
  }
  /**
   * Force immediate Unity discovery for connection recovery
   */
  async forceDiscovery() {
    if (this.unityClient.connected) {
      return true;
    }
    await this.unifiedDiscoveryAndConnectionCheck();
    return this.unityClient.connected;
  }
  /**
   * Handle connection lost event (called by UnityClient)
   */
  handleConnectionLost() {
    const correlationId = VibeLogger.generateCorrelationId();
    VibeLogger.logInfo(
      "unity_discovery_connection_lost_handler",
      "Handling connection lost event",
      {
        was_discovering: this.isDiscovering,
        has_discovery_interval: this.discoveryInterval !== null,
        active_timer_count: _UnityDiscovery.activeTimerCount
      },
      correlationId,
      "Connection lost event received - preparing for recovery"
    );
    this.isDiscovering = false;
    this.discoveryAttemptCount = 0;
    setTimeout(() => {
      VibeLogger.logInfo(
        "unity_discovery_restart_after_connection_lost",
        "Restarting discovery after connection lost delay",
        {
          has_discovery_interval: this.discoveryInterval !== null
        },
        correlationId,
        "Starting discovery with delay to allow Unity server restart"
      );
      if (!this.discoveryInterval) {
        this.start();
      }
    }, 2e3);
    if (this.onConnectionLostCallback) {
      this.onConnectionLostCallback();
    }
  }
  /**
   * Get debugging information about current timer state
   */
  getDebugInfo() {
    return {
      isTimerActive: this.discoveryInterval !== null,
      isDiscovering: this.isDiscovering,
      activeTimerCount: _UnityDiscovery.activeTimerCount,
      isConnected: this.unityClient.connected,
      intervalMs: this.getCurrentPollingInterval(),
      discoveryAttemptCount: this.discoveryAttemptCount,
      isInitialPolling: this.discoveryAttemptCount <= POLLING.INITIAL_ATTEMPTS,
      hasSingleton: _UnityDiscovery.instance !== null
    };
  }
};

// src/unity-connection-manager.ts
var UnityConnectionManager = class {
  unityClient;
  unityDiscovery;
  isDevelopment;
  isInitialized = false;
  isReconnecting = false;
  constructor(unityClient) {
    this.unityClient = unityClient;
    this.isDevelopment = process.env.NODE_ENV === ENVIRONMENT.NODE_ENV_DEVELOPMENT;
    this.unityDiscovery = UnityDiscovery.getInstance(this.unityClient);
    this.unityClient.setUnityDiscovery(this.unityDiscovery);
  }
  /**
   * Get Unity discovery instance
   */
  getUnityDiscovery() {
    return this.unityDiscovery;
  }
  /**
   * Wait for Unity connection with timeout
   */
  async waitForUnityConnectionWithTimeout(timeoutMs) {
    return new Promise((resolve2, reject) => {
      const timeout = setTimeout(() => {
        reject(new Error(`Unity connection timeout after ${timeoutMs}ms`));
      }, timeoutMs);
      const checkConnection = () => {
        if (this.unityClient.connected) {
          clearTimeout(timeout);
          resolve2();
          return;
        }
        if (this.isInitialized) {
          const connectionInterval = setInterval(() => {
            if (this.unityClient.connected) {
              clearTimeout(timeout);
              clearInterval(connectionInterval);
              resolve2();
            }
          }, 100);
          return;
        }
        this.initialize(() => {
          return new Promise((resolveCallback) => {
            clearTimeout(timeout);
            resolve2();
            resolveCallback();
          });
        });
      };
      void checkConnection();
    });
  }
  /**
   * Handle Unity discovery and establish connection
   */
  async handleUnityDiscovered(onConnectionEstablished) {
    try {
      await this.unityClient.ensureConnected();
      if (onConnectionEstablished) {
        await onConnectionEstablished();
      }
      this.unityDiscovery.stop();
    } catch (error) {
    }
  }
  /**
   * Initialize connection manager
   */
  initialize(onConnectionEstablished) {
    if (this.isInitialized) {
      return;
    }
    this.isInitialized = true;
    this.unityDiscovery.setOnDiscoveredCallback(async (_port) => {
      await this.handleUnityDiscovered(onConnectionEstablished);
    });
    this.unityDiscovery.start();
  }
  /**
   * Setup reconnection callback
   */
  setupReconnectionCallback(callback) {
    this.unityClient.setReconnectedCallback(() => {
      if (this.isReconnecting) {
        return;
      }
      this.isReconnecting = true;
      void this.unityDiscovery.forceDiscovery().then(() => {
        return callback();
      }).finally(() => {
        this.isReconnecting = false;
      });
    });
  }
  /**
   * Check if Unity is connected
   */
  isConnected() {
    return this.unityClient.connected;
  }
  /**
   * Ensure Unity connection is established
   */
  async ensureConnected(timeoutMs = 1e4) {
    if (this.isConnected()) {
      return;
    }
    await this.waitForUnityConnectionWithTimeout(timeoutMs);
  }
  /**
   * Test connection (validate connection state)
   */
  async testConnection() {
    try {
      return await this.unityClient.testConnection();
    } catch {
      return false;
    }
  }
  /**
   * Disconnect from Unity
   */
  disconnect() {
    this.unityDiscovery.stop();
    this.unityClient.disconnect();
  }
};

// src/tools/base-tool.ts
var BaseTool = class {
  context;
  constructor(context) {
    this.context = context;
  }
  /**
   * Main method for tool execution
   */
  async handle(args) {
    try {
      const validatedArgs = this.validateArgs(args);
      const result = await this.execute(validatedArgs);
      return this.formatResponse(result);
    } catch (error) {
      return this.formatErrorResponse(error);
    }
  }
  /**
   * Format success response (can be overridden in subclass)
   */
  formatResponse(result) {
    if (typeof result === "object" && result !== null && "content" in result) {
      return result;
    }
    return {
      content: [
        {
          type: "text",
          text: typeof result === "string" ? result : JSON.stringify(result, null, 2)
        }
      ]
    };
  }
  /**
   * Format error response
   */
  formatErrorResponse(error) {
    const errorMessage = error instanceof Error ? error.message : "Unknown error";
    return {
      content: [
        {
          type: "text",
          text: `Error in ${this.name}: ${errorMessage}`
        }
      ]
    };
  }
};

// src/tools/dynamic-unity-command-tool.ts
var DynamicUnityCommandTool = class extends BaseTool {
  name;
  description;
  inputSchema;
  toolName;
  constructor(context, toolName, description, parameterSchema) {
    super(context);
    this.toolName = toolName;
    this.name = toolName;
    this.description = description;
    this.inputSchema = this.generateInputSchema(parameterSchema);
  }
  generateInputSchema(parameterSchema) {
    if (this.hasNoParameters(parameterSchema)) {
      return {
        type: "object",
        properties: {},
        additionalProperties: false
      };
    }
    const properties = {};
    const required = [];
    if (!parameterSchema) {
      throw new Error("Parameter schema is undefined");
    }
    const propertiesObj = parameterSchema[PARAMETER_SCHEMA.PROPERTIES_PROPERTY];
    for (const [propName, propInfo] of Object.entries(propertiesObj)) {
      const info = propInfo;
      const property = {
        type: this.convertType(String(info[PARAMETER_SCHEMA.TYPE_PROPERTY] || "string")),
        description: String(
          info[PARAMETER_SCHEMA.DESCRIPTION_PROPERTY] || `Parameter: ${propName}`
        )
      };
      const defaultValue = info[PARAMETER_SCHEMA.DEFAULT_VALUE_PROPERTY];
      if (defaultValue !== void 0 && defaultValue !== null) {
        property.default = defaultValue;
      }
      const enumValues = info[PARAMETER_SCHEMA.ENUM_PROPERTY];
      if (enumValues && Array.isArray(enumValues) && enumValues.length > 0) {
        property.enum = enumValues;
      }
      if (info[PARAMETER_SCHEMA.TYPE_PROPERTY] === "array" && defaultValue && Array.isArray(defaultValue)) {
        property.items = {
          type: "string"
        };
        property.default = defaultValue;
      }
      Object.defineProperty(properties, propName, {
        value: property,
        writable: true,
        enumerable: true,
        configurable: true
      });
    }
    if (!parameterSchema) {
      throw new Error("Parameter schema is undefined");
    }
    const requiredParams = parameterSchema[PARAMETER_SCHEMA.REQUIRED_PROPERTY];
    if (requiredParams && Array.isArray(requiredParams)) {
      required.push(...requiredParams);
    }
    const schema = {
      type: "object",
      properties,
      required: required.length > 0 ? required : void 0
    };
    return schema;
  }
  convertType(unityType) {
    switch (unityType?.toLowerCase()) {
      case "string":
        return "string";
      case "number":
      case "int":
      case "float":
      case "double":
        return "number";
      case "boolean":
      case "bool":
        return "boolean";
      case "array":
        return "array";
      default:
        return "string";
    }
  }
  hasNoParameters(parameterSchema) {
    if (!parameterSchema) {
      return true;
    }
    const properties = parameterSchema[PARAMETER_SCHEMA.PROPERTIES_PROPERTY];
    if (!properties || typeof properties !== "object") {
      return true;
    }
    return Object.keys(properties).length === 0;
  }
  validateArgs(args) {
    if (!this.inputSchema.properties || Object.keys(this.inputSchema.properties).length === 0) {
      return {};
    }
    return args || {};
  }
  async execute(args) {
    try {
      const actualArgs = this.validateArgs(args);
      const result = await this.context.unityClient.executeTool(this.toolName, actualArgs);
      return {
        content: [
          {
            type: "text",
            text: typeof result === "string" ? result : JSON.stringify(result, null, 2)
          }
        ]
      };
    } catch (error) {
      return this.formatErrorResponse(error);
    }
  }
  formatErrorResponse(error) {
    const errorMessage = error instanceof Error ? error.message : "Unknown error";
    return {
      content: [
        {
          type: "text",
          text: errorMessage
        }
      ],
      isError: true
    };
  }
};

// src/domain/errors.ts
var DomainError = class extends Error {
  constructor(message, details) {
    super(message);
    this.details = details;
    this.name = this.constructor.name;
    Object.setPrototypeOf(this, new.target.prototype);
  }
};
var ConnectionError = class extends DomainError {
  code = "CONNECTION_ERROR";
};
var ToolExecutionError = class extends DomainError {
  code = "TOOL_EXECUTION_ERROR";
};
var ValidationError = class extends DomainError {
  code = "VALIDATION_ERROR";
};
var DiscoveryError = class extends DomainError {
  code = "DISCOVERY_ERROR";
};
var ClientCompatibilityError = class extends DomainError {
  code = "CLIENT_COMPATIBILITY_ERROR";
};

// src/infrastructure/errors.ts
var InfrastructureError = class extends Error {
  constructor(message, technicalDetails, originalError) {
    super(message);
    this.technicalDetails = technicalDetails;
    this.originalError = originalError;
    this.name = this.constructor.name;
    Object.setPrototypeOf(this, new.target.prototype);
  }
  /**
   * 技術的詳細を含む完全なエラー情報を取得
   */
  getFullErrorInfo() {
    return {
      message: this.message,
      category: this.category,
      technicalDetails: this.technicalDetails,
      originalError: this.originalError?.message,
      stack: this.stack
    };
  }
};
var UnityCommunicationError = class extends InfrastructureError {
  constructor(message, unityEndpoint, requestData, originalError) {
    super(message, { unityEndpoint, requestData }, originalError);
    this.unityEndpoint = unityEndpoint;
    this.requestData = requestData;
  }
  category = "UNITY_COMMUNICATION";
};
var ToolManagementError = class extends InfrastructureError {
  constructor(message, toolName, toolData, originalError) {
    super(message, { toolName, toolData }, originalError);
    this.toolName = toolName;
    this.toolData = toolData;
  }
  category = "TOOL_MANAGEMENT";
};

// src/application/error-converter.ts
var ErrorConverter = class {
  /**
   * Infrastructure層のエラーをDomain層のエラーに変換
   *
   * @param error 変換対象のエラー
   * @param operation 操作名（ログ用）
   * @param correlationId 相関ID
   * @returns 変換されたDomainError
   */
  static convertToDomainError(error, operation, correlationId) {
    if (error instanceof DomainError) {
      return error;
    }
    if (error instanceof InfrastructureError) {
      return this.convertInfrastructureError(error, operation, correlationId);
    }
    if (error instanceof Error) {
      return this.convertGenericError(error, operation, correlationId);
    }
    return this.convertUnknownError(error, operation, correlationId);
  }
  /**
   * Infrastructure層エラーをDomain層エラーに変換
   */
  static convertInfrastructureError(error, operation, correlationId) {
    VibeLogger.logError(
      `${operation}_infrastructure_error`,
      `Infrastructure error during ${operation}`,
      error.getFullErrorInfo(),
      correlationId,
      "Error Converter logging technical details before domain conversion"
    );
    switch (error.category) {
      case "UNITY_COMMUNICATION":
        return new ConnectionError(`Unity communication failed: ${error.message}`, {
          original_category: error.category,
          unity_endpoint: error.unityEndpoint
        });
      case "TOOL_MANAGEMENT":
        return new ToolExecutionError(`Tool management failed: ${error.message}`, {
          original_category: error.category,
          tool_name: error.toolName
        });
      case "SERVICE_RESOLUTION":
        return new ValidationError(`Service resolution failed: ${error.message}`, {
          original_category: error.category,
          service_token: error.serviceToken
        });
      case "NETWORK":
        return new DiscoveryError(`Network operation failed: ${error.message}`, {
          original_category: error.category,
          endpoint: error.endpoint,
          port: error.port
        });
      case "MCP_PROTOCOL":
        return new ClientCompatibilityError(`MCP protocol error: ${error.message}`, {
          original_category: error.category,
          protocol_version: error.protocolVersion
        });
      default:
        return new ToolExecutionError(`Infrastructure error: ${error.message}`, {
          original_category: error.category
        });
    }
  }
  /**
   * 一般的なErrorをDomain層エラーに変換
   */
  static convertGenericError(error, operation, correlationId) {
    VibeLogger.logError(
      `${operation}_generic_error`,
      `Generic error during ${operation}`,
      {
        error_name: error.name,
        error_message: error.message,
        stack: error.stack
      },
      correlationId,
      "Error Converter handling generic Error instance"
    );
    const message = error.message.toLowerCase();
    if (message.includes("connection") || message.includes("connect")) {
      return new ConnectionError(`Connection error: ${error.message}`);
    }
    if (message.includes("tool") || message.includes("execute")) {
      return new ToolExecutionError(`Tool execution error: ${error.message}`);
    }
    if (message.includes("validation") || message.includes("invalid")) {
      return new ValidationError(`Validation error: ${error.message}`);
    }
    if (message.includes("discovery") || message.includes("network")) {
      return new DiscoveryError(`Discovery error: ${error.message}`);
    }
    return new ToolExecutionError(`Unexpected error: ${error.message}`);
  }
  /**
   * 不明なエラーオブジェクトをDomain層エラーに変換
   */
  static convertUnknownError(error, operation, correlationId) {
    const errorString = typeof error === "string" ? error : JSON.stringify(error);
    VibeLogger.logError(
      `${operation}_unknown_error`,
      `Unknown error type during ${operation}`,
      { error_value: error, error_type: typeof error },
      correlationId,
      "Error Converter handling unknown error type"
    );
    return new ToolExecutionError(`Unknown error occurred: ${errorString}`);
  }
  /**
   * エラーが回復可能かどうかを判定
   *
   * @param error 判定対象のエラー
   * @returns 回復可能な場合true
   */
  static isRecoverable(error) {
    switch (error.code) {
      case "CONNECTION_ERROR":
      case "DISCOVERY_ERROR":
        return true;
      // 接続・発見エラーは再試行可能
      case "VALIDATION_ERROR":
      case "CLIENT_COMPATIBILITY_ERROR":
        return false;
      // 検証・互換性エラーは回復不可能
      case "TOOL_EXECUTION_ERROR":
        return true;
      // ツール実行エラーは状況によって再試行可能
      default:
        return false;
    }
  }
};

// src/domain/use-cases/refresh-tools-use-case.ts
var RefreshToolsUseCase = class {
  connectionService;
  toolManagementService;
  toolQueryService;
  constructor(connectionService, toolManagementService, toolQueryService) {
    this.connectionService = connectionService;
    this.toolManagementService = toolManagementService;
    this.toolQueryService = toolQueryService;
  }
  /**
   * Execute the tool refresh workflow
   *
   * @param request Tool refresh request
   * @returns Tool refresh response
   */
  async execute(request) {
    const correlationId = VibeLogger.generateCorrelationId();
    VibeLogger.logInfo(
      "refresh_tools_use_case_start",
      "Starting tool refresh workflow",
      { include_development: request.includeDevelopmentOnly },
      correlationId,
      "UseCase orchestrating tool refresh workflow for domain reload recovery"
    );
    try {
      await this.ensureUnityConnection(correlationId);
      await this.refreshToolsFromUnity(correlationId);
      const refreshedTools = this.toolQueryService.getAllTools();
      const response = {
        tools: refreshedTools,
        refreshedAt: (/* @__PURE__ */ new Date()).toISOString()
      };
      VibeLogger.logInfo(
        "refresh_tools_use_case_success",
        "Tool refresh workflow completed successfully",
        {
          tool_count: refreshedTools.length,
          refreshed_at: response.refreshedAt
        },
        correlationId,
        "Tool refresh completed successfully - Unity tools updated after domain reload"
      );
      return response;
    } catch (error) {
      return this.handleRefreshError(error, request, correlationId);
    }
  }
  /**
   * Ensure Unity connection is established
   *
   * @param correlationId Correlation ID for logging
   * @throws ConnectionError if connection cannot be established
   */
  async ensureUnityConnection(correlationId) {
    if (!this.connectionService.isConnected()) {
      VibeLogger.logWarning(
        "refresh_tools_unity_not_connected",
        "Unity not connected during tool refresh, attempting to establish connection",
        { connected: false },
        correlationId,
        "Unity connection required for tool refresh after domain reload"
      );
      try {
        await this.connectionService.ensureConnected(1e4);
      } catch (error) {
        const domainError = ErrorConverter.convertToDomainError(
          error,
          "refresh_tools_connection_ensure",
          correlationId
        );
        throw domainError;
      }
    }
    VibeLogger.logDebug(
      "refresh_tools_connection_verified",
      "Unity connection verified for tool refresh",
      { connected: true },
      correlationId,
      "Connection ready for tool refresh after domain reload"
    );
  }
  /**
   * Refresh tools from Unity by re-initializing dynamic tools
   *
   * @param correlationId Correlation ID for logging
   */
  async refreshToolsFromUnity(correlationId) {
    try {
      VibeLogger.logDebug(
        "refresh_tools_initializing",
        "Re-initializing dynamic tools from Unity",
        {},
        correlationId,
        "Fetching latest tool definitions from Unity after domain reload"
      );
      await this.toolManagementService.initializeTools();
      VibeLogger.logInfo(
        "refresh_tools_initialized",
        "Dynamic tools re-initialized successfully from Unity",
        { tool_count: this.toolQueryService.getToolsCount() },
        correlationId,
        "Tool definitions updated from Unity after domain reload"
      );
    } catch (error) {
      const domainError = ErrorConverter.convertToDomainError(
        error,
        "refresh_tools_initialization",
        correlationId
      );
      throw domainError;
    }
  }
  /**
   * Handle tool refresh errors
   *
   * @param error Error that occurred
   * @param request Original request
   * @param correlationId Correlation ID for logging
   * @returns RefreshToolsResponse with error state
   */
  handleRefreshError(error, request, correlationId) {
    const errorMessage = error instanceof Error ? error.message : "Unknown error";
    VibeLogger.logError(
      "refresh_tools_use_case_error",
      "Tool refresh workflow failed",
      {
        include_development: request.includeDevelopmentOnly,
        error_message: errorMessage,
        error_type: error instanceof Error ? error.constructor.name : typeof error
      },
      correlationId,
      "UseCase workflow failed - returning empty tools list to prevent client errors"
    );
    return {
      tools: [],
      refreshedAt: (/* @__PURE__ */ new Date()).toISOString()
    };
  }
};

// src/unity-tool-manager.ts
var UnityToolManager = class {
  unityClient;
  isDevelopment;
  dynamicTools = /* @__PURE__ */ new Map();
  isRefreshing = false;
  clientName = "";
  connectionManager;
  // Will be injected for UseCase
  constructor(unityClient) {
    this.unityClient = unityClient;
    this.isDevelopment = process.env.NODE_ENV === ENVIRONMENT.NODE_ENV_DEVELOPMENT;
  }
  /**
   * Set connection manager for UseCase integration (Phase 3.2)
   */
  setConnectionManager(connectionManager) {
    this.connectionManager = connectionManager;
  }
  /**
   * Set client name for Unity communication
   */
  setClientName(clientName) {
    this.clientName = clientName;
  }
  /**
   * Get dynamic tools map
   */
  getDynamicTools() {
    return this.dynamicTools;
  }
  /**
   * Get tools from Unity
   */
  async getToolsFromUnity() {
    if (!this.unityClient.connected) {
      return [];
    }
    try {
      const toolDetails = await this.unityClient.fetchToolDetailsFromUnity(this.isDevelopment);
      if (!toolDetails) {
        throw new UnityCommunicationError(
          "Unity returned no tool details",
          "Unity Editor tools endpoint",
          { development_mode: this.isDevelopment }
        );
      }
      this.createDynamicToolsFromTools(toolDetails);
      const tools = [];
      for (const [toolName, dynamicTool] of this.dynamicTools) {
        tools.push({
          name: toolName,
          description: dynamicTool.description,
          inputSchema: this.convertToMcpSchema(dynamicTool.inputSchema)
        });
      }
      return tools;
    } catch (error) {
      if (error instanceof UnityCommunicationError) {
        throw error;
      }
      throw new UnityCommunicationError(
        "Failed to retrieve tools from Unity",
        "Unity Editor tools endpoint",
        { development_mode: this.isDevelopment },
        error instanceof Error ? error : void 0
      );
    }
  }
  /**
   * Initialize dynamic Unity tools
   */
  async initializeDynamicTools() {
    try {
      await this.unityClient.ensureConnected();
      const toolDetails = await this.unityClient.fetchToolDetailsFromUnity(this.isDevelopment);
      if (!toolDetails) {
        throw new UnityCommunicationError(
          "Unity returned no tool details during initialization",
          "Unity Editor tools endpoint",
          { development_mode: this.isDevelopment }
        );
      }
      this.createDynamicToolsFromTools(toolDetails);
    } catch (error) {
      if (error instanceof UnityCommunicationError) {
        throw error;
      }
      throw new ToolManagementError(
        "Failed to initialize dynamic tools",
        void 0,
        { development_mode: this.isDevelopment },
        error instanceof Error ? error : void 0
      );
    }
  }
  /**
   * Create dynamic tools from Unity tool details
   */
  createDynamicToolsFromTools(toolDetails) {
    this.dynamicTools.clear();
    const toolContext = { unityClient: this.unityClient };
    for (const toolInfo of toolDetails) {
      const toolName = toolInfo.name;
      const description = toolInfo.description || `Execute Unity tool: ${toolName}`;
      const parameterSchema = toolInfo.parameterSchema;
      const displayDevelopmentOnly = toolInfo.displayDevelopmentOnly || false;
      if (displayDevelopmentOnly && !this.isDevelopment) {
        continue;
      }
      const finalToolName = toolName;
      const dynamicTool = new DynamicUnityCommandTool(
        toolContext,
        toolName,
        description,
        parameterSchema
        // Type assertion for schema compatibility
      );
      this.dynamicTools.set(finalToolName, dynamicTool);
    }
  }
  /**
   * Refresh dynamic tools by re-fetching from Unity
   * This method can be called to update the tool list when Unity tools change
   *
   * IMPORTANT: This method is critical for domain reload recovery
   */
  async refreshDynamicTools(sendNotification) {
    try {
      if (this.connectionManager && this.connectionManager.isConnected()) {
        await this.refreshToolsWithUseCase(sendNotification);
        return;
      }
      await this.refreshToolsFallback(sendNotification);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : String(error);
      VibeLogger.logError(
        "unity_tool_manager_refresh_failed",
        "Failed to refresh dynamic tools",
        { error: errorMessage },
        void 0,
        "Tool refresh failed - domain reload recovery may be impacted"
      );
      await this.executeUltimateToolsFallback(sendNotification);
    }
  }
  /**
   * Refresh tools using RefreshToolsUseCase (preferred method)
   */
  async refreshToolsWithUseCase(sendNotification) {
    if (!this.connectionManager) {
      throw new Error("ConnectionManager is required for UseCase-based refresh");
    }
    const refreshToolsUseCase = new RefreshToolsUseCase(this.connectionManager, this, this);
    const result = await refreshToolsUseCase.execute({
      includeDevelopmentOnly: this.isDevelopment
    });
    VibeLogger.logInfo(
      "unity_tool_manager_refresh_completed",
      "Dynamic tools refreshed successfully via UseCase",
      { tool_count: result.tools.length, refreshed_at: result.refreshedAt },
      void 0,
      "Tool refresh completed - ready for domain reload recovery"
    );
    if (sendNotification) {
      sendNotification();
    }
  }
  /**
   * Refresh tools using direct initialization (fallback method)
   */
  async refreshToolsFallback(sendNotification) {
    VibeLogger.logDebug(
      "unity_tool_manager_fallback_refresh",
      "Using fallback refresh method (connectionManager not available)",
      {},
      void 0,
      "Direct initialization fallback for domain reload recovery"
    );
    await this.initializeDynamicTools();
    if (sendNotification) {
      sendNotification();
    }
  }
  /**
   * Execute ultimate fallback for tool refresh (last resort)
   */
  async executeUltimateToolsFallback(sendNotification) {
    try {
      await this.initializeDynamicTools();
      if (sendNotification) {
        sendNotification();
      }
    } catch (fallbackError) {
      VibeLogger.logError(
        "unity_tool_manager_fallback_failed",
        "Fallback tool initialization also failed",
        { error: fallbackError instanceof Error ? fallbackError.message : String(fallbackError) },
        void 0,
        "Both UseCase and fallback failed - domain reload recovery compromised"
      );
    }
  }
  /**
   * Safe version of refreshDynamicTools that prevents duplicate execution
   */
  async refreshDynamicToolsSafe(sendNotification) {
    if (this.isRefreshing) {
      return;
    }
    this.isRefreshing = true;
    try {
      await this.refreshDynamicTools(sendNotification);
    } finally {
      this.isRefreshing = false;
    }
  }
  /**
   * Check if tool exists
   */
  hasTool(toolName) {
    return this.dynamicTools.has(toolName);
  }
  /**
   * Get tool by name
   */
  getTool(toolName) {
    return this.dynamicTools.get(toolName);
  }
  /**
   * Get all tools as array
   */
  getAllTools() {
    const tools = [];
    for (const [toolName, dynamicTool] of this.dynamicTools) {
      tools.push({
        name: toolName,
        description: dynamicTool.description,
        inputSchema: this.convertToMcpSchema(dynamicTool.inputSchema)
      });
    }
    return tools;
  }
  /**
   * Get tools count
   */
  getToolsCount() {
    return this.dynamicTools.size;
  }
  // IToolService interface compatibility methods
  /**
   * Initialize dynamic tools (IToolService interface)
   */
  async initializeTools() {
    return this.initializeDynamicTools();
  }
  /**
   * Refresh dynamic tools (IToolService interface)
   */
  async refreshTools() {
    return this.initializeDynamicTools();
  }
  /**
   * Convert input schema to MCP-compatible format safely
   */
  convertToMcpSchema(inputSchema) {
    if (!inputSchema || typeof inputSchema !== "object") {
      return { type: "object" };
    }
    const schema = inputSchema;
    const result = { type: "object" };
    if (schema.properties && typeof schema.properties === "object") {
      result.properties = schema.properties;
    }
    if (Array.isArray(schema.required)) {
      result.required = schema.required;
    }
    return result;
  }
};

// src/mcp-client-compatibility.ts
var McpClientCompatibility = class {
  unityClient;
  clientName = DEFAULT_CLIENT_NAME;
  constructor(unityClient) {
    this.unityClient = unityClient;
  }
  /**
   * Set client name
   */
  setClientName(clientName) {
    this.clientName = clientName;
  }
  /**
   * Setup client compatibility configuration with logging
   */
  setupClientCompatibility(clientName) {
    this.setClientName(clientName);
    this.logClientCompatibility(clientName);
  }
  /**
   * Get client name
   */
  getClientName() {
    return this.clientName;
  }
  /**
   * Check if client doesn't support list_changed notifications
   */
  isListChangedUnsupported(clientName) {
    return !this.isListChangedSupported(clientName);
  }
  /**
   * Handle client name initialization and setup
   */
  async handleClientNameInitialization() {
    if (!this.clientName) {
      const fallbackName = process.env.MCP_CLIENT_NAME;
      if (fallbackName) {
        this.clientName = fallbackName;
        await this.unityClient.setClientName(fallbackName);
      } else {
      }
    } else {
      await this.unityClient.setClientName(this.clientName);
    }
    this.unityClient.onReconnect(() => {
      void this.unityClient.setClientName(this.clientName);
    });
  }
  /**
   * Initialize client with name
   */
  async initializeClient(clientName) {
    this.setClientName(clientName);
    await this.handleClientNameInitialization();
  }
  /**
   * Check if client supports list_changed notifications
   */
  isListChangedSupported(clientName) {
    if (!clientName) {
      return false;
    }
    const normalizedName = clientName.toLowerCase();
    return LIST_CHANGED_SUPPORTED_CLIENTS.some((supported) => normalizedName.includes(supported));
  }
  /**
   * Log client compatibility information
   */
  logClientCompatibility(clientName) {
    const isSupported = this.isListChangedSupported(clientName);
    if (!isSupported) {
    } else {
    }
  }
};

// src/unity-event-handler.ts
var UnityEventHandler = class {
  server;
  unityClient;
  connectionManager;
  httpServer = null;
  isDevelopment;
  shuttingDown = false;
  isNotifying = false;
  hasSentListChangedNotification = false;
  constructor(server2, unityClient, connectionManager) {
    this.server = server2;
    this.unityClient = unityClient;
    this.connectionManager = connectionManager;
    this.isDevelopment = process.env.NODE_ENV === ENVIRONMENT.NODE_ENV_DEVELOPMENT;
  }
  /**
   * Set the HTTP server reference for graceful shutdown
   */
  setHttpServer(httpServer) {
    this.httpServer = httpServer;
  }
  /**
   * Setup Unity event listener for automatic tool updates
   */
  setupUnityEventListener(onToolsChanged) {
    this.unityClient.onNotification("notifications/tools/list_changed", (_params) => {
      VibeLogger.logInfo(
        "unity_notification_received",
        "Unity notification received: notifications/tools/list_changed",
        void 0,
        void 0,
        "Unity notified that tool list has changed"
      );
      try {
        void onToolsChanged();
      } catch (error) {
        VibeLogger.logError(
          "unity_notification_error",
          "Failed to update dynamic tools via Unity notification",
          { error: error instanceof Error ? error.message : String(error) },
          void 0,
          "Error occurred while processing Unity tool list change notification"
        );
      }
    });
  }
  /**
   * Send tools changed notification (with duplicate prevention)
   */
  sendToolsChangedNotification() {
    if (this.hasSentListChangedNotification) {
      if (this.isDevelopment) {
        VibeLogger.logDebug(
          "tools_notification_skipped_already_sent",
          "sendToolsChangedNotification skipped: list_changed already sent",
          void 0,
          void 0,
          "Subsequent list_changed notification suppressed"
        );
      }
      return;
    }
    if (this.isNotifying) {
      if (this.isDevelopment) {
        VibeLogger.logDebug(
          "tools_notification_skipped",
          "sendToolsChangedNotification skipped: already notifying",
          void 0,
          void 0,
          "Duplicate notification prevented"
        );
      }
      return;
    }
    this.isNotifying = true;
    try {
      void this.server.notification({
        method: NOTIFICATION_METHODS.TOOLS_LIST_CHANGED,
        params: {}
      });
      this.hasSentListChangedNotification = true;
      if (this.isDevelopment) {
        VibeLogger.logInfo(
          "tools_notification_sent",
          "tools/list_changed notification sent",
          void 0,
          void 0,
          "Successfully notified client of tool list changes"
        );
      }
    } catch (error) {
      VibeLogger.logError(
        "tools_notification_error",
        "Failed to send tools changed notification",
        { error: error instanceof Error ? error.message : String(error) },
        void 0,
        "Error occurred while sending tool list change notification"
      );
    } finally {
      this.isNotifying = false;
    }
  }
  /**
   * Setup signal handlers for graceful shutdown
   */
  setupSignalHandlers() {
    process.on("SIGINT", () => {
      VibeLogger.logInfo(
        "sigint_received",
        "Received SIGINT, shutting down...",
        void 0,
        void 0,
        "User pressed Ctrl+C, initiating graceful shutdown"
      );
      this.gracefulShutdown();
    });
    process.on("SIGTERM", () => {
      VibeLogger.logInfo(
        "sigterm_received",
        "Received SIGTERM, shutting down...",
        void 0,
        void 0,
        "Process termination signal received, initiating graceful shutdown"
      );
      this.gracefulShutdown();
    });
    process.on("SIGHUP", () => {
      VibeLogger.logInfo(
        "sighup_received",
        "Received SIGHUP, shutting down...",
        void 0,
        void 0,
        "Terminal hangup signal received, initiating graceful shutdown"
      );
      this.gracefulShutdown();
    });
    process.on("uncaughtException", (error) => {
      VibeLogger.logException(
        "uncaught_exception",
        error,
        void 0,
        void 0,
        "Uncaught exception occurred, shutting down safely"
      );
      this.gracefulShutdown();
    });
    process.on("unhandledRejection", (reason, promise) => {
      VibeLogger.logError(
        "unhandled_rejection",
        "Unhandled promise rejection",
        { reason: String(reason), promise: String(promise) },
        void 0,
        "Unhandled promise rejection occurred, shutting down safely"
      );
      this.gracefulShutdown();
    });
  }
  /**
   * Graceful shutdown with proper cleanup
   * BUG FIX: Enhanced shutdown process to prevent orphaned Node processes
   */
  gracefulShutdown() {
    if (this.shuttingDown) {
      return;
    }
    this.shuttingDown = true;
    VibeLogger.logInfo(
      "graceful_shutdown_start",
      "Starting graceful shutdown...",
      void 0,
      void 0,
      "Initiating graceful shutdown process"
    );
    void (async () => {
      try {
        if (this.httpServer) {
          VibeLogger.logInfo(
            "http_server_stopping",
            "Stopping HTTP server...",
            void 0,
            void 0,
            "Shutting down MCP HTTP server"
          );
          await this.httpServer.stop();
        }
        this.connectionManager.disconnect();
        if (global.gc) {
          global.gc();
        }
      } catch (error) {
        VibeLogger.logError(
          "cleanup_error",
          "Error during cleanup",
          { error: error instanceof Error ? error.message : String(error) },
          void 0,
          "Error occurred during graceful shutdown cleanup"
        );
      }
      VibeLogger.logInfo(
        "graceful_shutdown_complete",
        "Graceful shutdown completed",
        void 0,
        void 0,
        "All cleanup completed, process will exit"
      );
      process.exit(0);
    })();
  }
  /**
   * Check if shutdown is in progress
   */
  isShuttingDown() {
    return this.shuttingDown;
  }
};

// src/http-server.ts
import { createServer } from "http";
import { StreamableHTTPServerTransport } from "@modelcontextprotocol/sdk/server/streamableHttp.js";
import { isInitializeRequest } from "@modelcontextprotocol/sdk/types.js";
import { randomUUID } from "crypto";
var McpHttpServer = class {
  httpServer = null;
  transports = /* @__PURE__ */ new Map();
  config;
  onTransportCreated = null;
  constructor(config) {
    this.config = {
      port: config.port,
      enableSessions: config.enableSessions ?? true,
      sessionTimeoutMs: config.sessionTimeoutMs ?? 30 * 60 * 1e3
      // 30 minutes
    };
  }
  /**
   * Set callback for when a new transport is created
   * This allows the main server to connect to each transport
   */
  setTransportCreatedCallback(callback) {
    this.onTransportCreated = callback;
  }
  /**
   * Parse JSON body from request
   */
  async parseJsonBody(req) {
    return new Promise((resolve2, reject) => {
      let body = "";
      req.on("data", (chunk) => {
        body += chunk.toString();
      });
      req.on("end", () => {
        if (!body) {
          resolve2({});
          return;
        }
        try {
          resolve2(JSON.parse(body));
        } catch (error) {
          reject(new Error("Invalid JSON"));
        }
      });
      req.on("error", reject);
    });
  }
  /**
   * Send JSON response
   */
  sendJson(res, statusCode, data) {
    res.writeHead(statusCode, { "Content-Type": "application/json" });
    res.end(JSON.stringify(data));
  }
  /**
   * Send JSON-RPC error response
   */
  sendJsonRpcError(res, statusCode, code, message) {
    this.sendJson(res, statusCode, {
      jsonrpc: "2.0",
      error: { code, message },
      id: null
    });
  }
  /**
   * Handle incoming HTTP requests
   */
  async handleRequest(req, res) {
    const url = req.url ?? "/";
    const method = req.method ?? "GET";
    VibeLogger.logInfo("mcp_http_request", "Incoming HTTP request", {
      method,
      url,
      session_id: req.headers["mcp-session-id"],
      accept: req.headers["accept"]
    });
    if (url === "/health" && method === "GET") {
      this.sendJson(res, 200, { status: "ok", sessions: this.transports.size });
      return;
    }
    if (url === "/mcp") {
      switch (method) {
        case "POST":
          await this.handlePost(req, res);
          break;
        case "GET":
          await this.handleGet(req, res);
          break;
        case "DELETE":
          await this.handleDelete(req, res);
          break;
        default:
          this.sendJsonRpcError(res, 405, -32600, "Method not allowed");
      }
      return;
    }
    this.sendJsonRpcError(res, 404, -32600, "Not found");
  }
  /**
   * Handle POST requests - client-to-server messages
   */
  async handlePost(req, res) {
    const sessionId = req.headers["mcp-session-id"];
    try {
      const body = await this.parseJsonBody(req);
      let transport;
      if (sessionId && this.transports.has(sessionId)) {
        transport = this.transports.get(sessionId);
      } else if (!sessionId && isInitializeRequest(body)) {
        transport = new StreamableHTTPServerTransport({
          sessionIdGenerator: this.config.enableSessions ? () => randomUUID() : void 0
        });
        if (this.onTransportCreated) {
          await this.onTransportCreated(transport);
        }
        if (this.config.enableSessions) {
          transport.onclose = () => {
            for (const [id, t] of this.transports.entries()) {
              if (t === transport) {
                this.transports.delete(id);
                VibeLogger.logInfo("mcp_session_closed", "MCP session closed", {
                  session_id: id
                });
                break;
              }
            }
          };
        }
        VibeLogger.logInfo("mcp_transport_created", "New MCP transport created", {
          has_session: this.config.enableSessions
        });
      } else if (sessionId && !this.transports.has(sessionId)) {
        this.sendJsonRpcError(res, 400, -32e3, "Bad Request: Invalid session ID");
        return;
      } else {
        this.sendJsonRpcError(
          res,
          400,
          -32e3,
          "Bad Request: No valid session. Send an initialize request first."
        );
        return;
      }
      await transport.handleRequest(req, res, body);
      if (!sessionId && isInitializeRequest(body) && this.config.enableSessions) {
        const newSessionId = res.getHeader("mcp-session-id");
        if (newSessionId) {
          this.transports.set(newSessionId, transport);
          VibeLogger.logInfo("mcp_session_created", "New MCP session created", {
            session_id: newSessionId
          });
        }
      }
    } catch (error) {
      VibeLogger.logError("mcp_http_post_error", "Error handling POST request", {
        error_message: error instanceof Error ? error.message : String(error),
        session_id: sessionId
      });
      if (!res.writableEnded) {
        this.sendJsonRpcError(res, 500, -32603, "Internal server error");
      }
    }
  }
  /**
   * Handle GET requests - SSE stream for server-to-client notifications
   */
  async handleGet(req, res) {
    const sessionId = req.headers["mcp-session-id"];
    if (!sessionId || !this.transports.has(sessionId)) {
      this.sendJsonRpcError(res, 400, -32e3, "Bad Request: Mcp-Session-Id header is required");
      return;
    }
    const transport = this.transports.get(sessionId);
    try {
      await transport.handleRequest(req, res);
    } catch (error) {
      VibeLogger.logError("mcp_http_get_error", "Error handling GET request", {
        error_message: error instanceof Error ? error.message : String(error),
        session_id: sessionId
      });
    }
  }
  /**
   * Handle DELETE requests - session termination
   */
  async handleDelete(req, res) {
    const sessionId = req.headers["mcp-session-id"];
    if (!sessionId || !this.transports.has(sessionId)) {
      this.sendJsonRpcError(res, 400, -32e3, "Bad Request: Mcp-Session-Id header is required");
      return;
    }
    const transport = this.transports.get(sessionId);
    try {
      await transport.handleRequest(req, res);
      this.transports.delete(sessionId);
      VibeLogger.logInfo("mcp_session_deleted", "MCP session deleted", {
        session_id: sessionId
      });
    } catch (error) {
      VibeLogger.logError("mcp_http_delete_error", "Error handling DELETE request", {
        error_message: error instanceof Error ? error.message : String(error),
        session_id: sessionId
      });
    }
  }
  /**
   * Start the HTTP server
   */
  async start() {
    return new Promise((resolve2, reject) => {
      try {
        this.httpServer = createServer((req, res) => {
          void this.handleRequest(req, res);
        });
        this.httpServer.on("error", (error) => {
          if (error.code === "EADDRINUSE") {
            VibeLogger.logError("mcp_http_port_in_use", `HTTP port ${this.config.port} is in use`, {
              port: this.config.port
            });
            reject(new Error(`Port ${this.config.port} is already in use`));
          } else {
            VibeLogger.logError("mcp_http_server_error", "HTTP server error", {
              error_message: error.message
            });
            reject(error);
          }
        });
        this.httpServer.listen(this.config.port, () => {
          VibeLogger.logInfo("mcp_http_server_started", "MCP HTTP server started", {
            port: this.config.port,
            sessions_enabled: this.config.enableSessions
          });
          resolve2();
        });
      } catch (error) {
        reject(error);
      }
    });
  }
  /**
   * Stop the HTTP server
   */
  async stop() {
    for (const [sessionId, transport] of this.transports.entries()) {
      try {
        await transport.close();
        VibeLogger.logInfo("mcp_transport_closed", "Transport closed", {
          session_id: sessionId
        });
      } catch (error) {
        VibeLogger.logWarning("mcp_transport_close_error", "Error closing transport", {
          session_id: sessionId,
          error_message: error instanceof Error ? error.message : String(error)
        });
      }
    }
    this.transports.clear();
    if (this.httpServer) {
      return new Promise((resolve2) => {
        this.httpServer.close(() => {
          VibeLogger.logInfo("mcp_http_server_stopped", "MCP HTTP server stopped");
          this.httpServer = null;
          resolve2();
        });
      });
    }
  }
  /**
   * Get the number of active sessions
   */
  getSessionCount() {
    return this.transports.size;
  }
  /**
   * Check if server is running
   */
  isRunning() {
    return this.httpServer !== null && this.httpServer.listening;
  }
};

// package.json
var package_default = {
  name: "uloopmcp-server",
  version: "0.30.1",
  description: "TypeScript MCP Server for Unity-Cursor integration",
  main: "dist/server.bundle.js",
  type: "module",
  scripts: {
    prepare: "husky",
    build: "npm run build:bundle",
    "build:bundle": "esbuild src/server.ts --bundle --platform=node --format=esm --outfile=dist/server.bundle.js --packages=external --sourcemap",
    "build:production": "cross-env ULOOPMCP_PRODUCTION=true NODE_ENV=production esbuild src/server.ts --bundle --platform=node --format=esm --outfile=dist/server.bundle.js --packages=external --sourcemap",
    dev: "cross-env NODE_ENV=development npm run build:bundle && cross-env NODE_ENV=development NODE_OPTIONS=--enable-source-maps node dist/server.bundle.js",
    "dev:watch": "cross-env NODE_ENV=development esbuild src/server.ts --bundle --platform=node --format=esm --outfile=dist/server.bundle.js --packages=external --sourcemap --watch",
    start: "cross-env NODE_OPTIONS=--enable-source-maps node dist/server.bundle.js",
    "start:production": "cross-env ULOOPMCP_PRODUCTION=true NODE_OPTIONS=--enable-source-maps node dist/server.bundle.js",
    "start:dev": "cross-env NODE_ENV=development NODE_OPTIONS=--enable-source-maps node dist/server.bundle.js",
    lint: "eslint src --ext .ts",
    "lint:fix": "eslint src --ext .ts --fix",
    "security:check": "eslint src --ext .ts",
    "security:fix": "eslint src --ext .ts --fix",
    "security:only": "eslint src --ext .ts --config .eslintrc.security.json",
    "security:sarif": "eslint src --ext .ts -f @microsoft/eslint-formatter-sarif -o security-results.sarif",
    "security:sarif-only": "eslint src --ext .ts --config .eslintrc.security.json -f @microsoft/eslint-formatter-sarif -o typescript-security.sarif --max-warnings 0",
    format: "prettier --write src/**/*.ts",
    "format:check": "prettier --check src/**/*.ts",
    "lint:check": "npm run lint && npm run format:check",
    test: "jest",
    "test:mcp": "tsx src/tools/__tests__/test-runner.ts",
    "test:integration": "tsx src/tools/__tests__/integration-test.ts",
    "test:watch": "jest --watch",
    validate: "npm run test:integration && echo 'Integration tests passed - safe to deploy'",
    deploy: "npm run validate && npm run build",
    "debug:compile": "tsx debug/compile-check.ts",
    "debug:logs": "tsx debug/logs-fetch.ts",
    "debug:connection": "tsx debug/connection-check.ts",
    "debug:all-logs": "tsx debug/all-logs-fetch.ts",
    "debug:compile-detailed": "tsx debug/compile-detailed.ts",
    "debug:connection-survival": "tsx debug/connection-survival.ts",
    "debug:domain-reload-timing": "tsx debug/domain-reload-timing.ts",
    "debug:event-test": "tsx debug/event-test.ts",
    "debug:notification-test": "tsx debug/notification-test.ts",
    prepublishOnly: "npm run build",
    postinstall: "npm run build"
  },
  "lint-staged": {
    "src/**/*.{ts,js}": [
      "eslint --fix",
      "prettier --write"
    ]
  },
  keywords: [
    "mcp",
    "unity",
    "cursor",
    "typescript"
  ],
  author: "hatayama",
  license: "MIT",
  dependencies: {
    "@modelcontextprotocol/sdk": "1.12.2",
    "@types/uuid": "^10.0.0",
    uuid: "^11.1.0",
    zod: "3.25.64"
  },
  devDependencies: {
    "@microsoft/eslint-formatter-sarif": "^3.1.0",
    "@types/jest": "^29.5.14",
    "@types/node": "20.19.0",
    "@typescript-eslint/eslint-plugin": "^7.18.0",
    "@typescript-eslint/parser": "^7.18.0",
    "cross-env": "^10.0.0",
    esbuild: "^0.25.6",
    eslint: "^8.57.1",
    "eslint-config-prettier": "^9.1.0",
    "eslint-plugin-prettier": "^5.2.1",
    "eslint-plugin-security": "^3.0.1",
    husky: "^9.0.0",
    jest: "^30.0.0",
    "lint-staged": "^15.0.0",
    prettier: "^3.5.0",
    "ts-jest": "^29.4.0",
    tsx: "4.20.3",
    typescript: "5.8.3"
  }
};

// src/server.ts
var UnityMcpServer = class {
  server;
  unityClient;
  isDevelopment;
  isInitialized = false;
  unityDiscovery;
  connectionManager;
  toolManager;
  clientCompatibility;
  eventHandler;
  constructor() {
    this.isDevelopment = process.env.NODE_ENV === ENVIRONMENT.NODE_ENV_DEVELOPMENT;
    this.server = new Server(
      {
        name: MCP_SERVER_NAME,
        version: package_default.version
      },
      {
        capabilities: {
          tools: {
            listChanged: TOOLS_LIST_CHANGED_CAPABILITY
          }
        }
      }
    );
    this.unityClient = UnityClient.getInstance();
    this.connectionManager = new UnityConnectionManager(this.unityClient);
    this.unityDiscovery = this.connectionManager.getUnityDiscovery();
    this.toolManager = new UnityToolManager(this.unityClient);
    this.toolManager.setConnectionManager(this.connectionManager);
    this.clientCompatibility = new McpClientCompatibility(this.unityClient);
    this.eventHandler = new UnityEventHandler(
      this.server,
      this.unityClient,
      this.connectionManager
    );
    this.connectionManager.setupReconnectionCallback(async () => {
      await this.toolManager.refreshDynamicToolsSafe(() => {
        this.eventHandler.sendToolsChangedNotification();
      });
    });
    this.setupHandlers();
    this.eventHandler.setupSignalHandlers();
  }
  /**
   * Initialize client synchronously (for list_changed unsupported clients)
   */
  async initializeSyncClient(clientName) {
    try {
      await this.clientCompatibility.initializeClient(clientName);
      this.toolManager.setClientName(clientName);
      await this.connectionManager.waitForUnityConnectionWithTimeout(1e4);
      const tools = await this.toolManager.getToolsFromUnity();
      return {
        protocolVersion: MCP_PROTOCOL_VERSION,
        capabilities: {
          tools: {
            listChanged: TOOLS_LIST_CHANGED_CAPABILITY
          }
        },
        serverInfo: {
          name: MCP_SERVER_NAME,
          version: package_default.version
        },
        tools
      };
    } catch (error) {
      VibeLogger.logError(
        "mcp_unity_connection_timeout",
        "Unity connection timeout",
        {
          client_name: clientName,
          error_message: error instanceof Error ? error.message : String(error)
        },
        void 0,
        "Unity connection timed out - check Unity MCP bridge status"
      );
      return {
        protocolVersion: MCP_PROTOCOL_VERSION,
        capabilities: {
          tools: {
            listChanged: TOOLS_LIST_CHANGED_CAPABILITY
          }
        },
        serverInfo: {
          name: MCP_SERVER_NAME,
          version: package_default.version
        },
        tools: []
      };
    }
  }
  /**
   * Initialize client asynchronously (for list_changed supported clients)
   */
  initializeAsyncClient(clientName) {
    void this.clientCompatibility.initializeClient(clientName);
    this.toolManager.setClientName(clientName);
    void this.toolManager.initializeDynamicTools().then(() => {
    }).catch((error) => {
      VibeLogger.logError(
        "mcp_unity_connection_init_failed",
        "Unity connection initialization failed",
        { error_message: error instanceof Error ? error.message : String(error) },
        void 0,
        "Unity connection could not be established - check Unity MCP bridge"
      );
      this.unityDiscovery.start();
    });
  }
  setupHandlers() {
    this.server.setRequestHandler(InitializeRequestSchema, async (request) => {
      const clientInfo = request.params?.clientInfo;
      const clientName = clientInfo?.name || "";
      if (clientName) {
        this.clientCompatibility.setupClientCompatibility(clientName);
      }
      if (!this.isInitialized) {
        this.isInitialized = true;
        if (this.clientCompatibility.isListChangedUnsupported(clientName)) {
          return this.initializeSyncClient(clientName);
        } else {
          this.initializeAsyncClient(clientName);
        }
      }
      return {
        protocolVersion: MCP_PROTOCOL_VERSION,
        capabilities: {
          tools: {
            listChanged: TOOLS_LIST_CHANGED_CAPABILITY
          }
        },
        serverInfo: {
          name: MCP_SERVER_NAME,
          version: package_default.version
        }
      };
    });
    this.server.setRequestHandler(ListToolsRequestSchema, () => {
      const tools = this.toolManager.getAllTools();
      return { tools };
    });
    this.server.setRequestHandler(CallToolRequestSchema, async (request) => {
      const { name, arguments: args } = request.params;
      try {
        if (this.toolManager.hasTool(name)) {
          const dynamicTool = this.toolManager.getTool(name);
          if (!dynamicTool) {
            throw new Error(`Tool ${name} is not available`);
          }
          const result = await dynamicTool.execute(args ?? {});
          return {
            content: result.content,
            isError: result.isError
          };
        }
        throw new Error(`Unknown tool: ${name}`);
      } catch (error) {
        return {
          content: [
            {
              type: "text",
              text: `Error executing ${name}: ${error instanceof Error ? error.message : "Unknown error"}`
            }
          ],
          isError: true
        };
      }
    });
  }
  /**
   * Start the server
   */
  async start() {
    this.eventHandler.setupUnityEventListener(async () => {
      await this.toolManager.refreshDynamicToolsSafe(() => {
        this.eventHandler.sendToolsChangedNotification();
      });
    });
    this.connectionManager.initialize(async () => {
      const clientName = this.clientCompatibility.getClientName();
      if (clientName) {
        this.toolManager.setClientName(clientName);
        await this.toolManager.initializeDynamicTools();
        this.eventHandler.sendToolsChangedNotification();
      } else {
      }
    });
    const httpPortStr = process.env.MCP_HTTP_PORT;
    if (!httpPortStr) {
      throw new Error("MCP_HTTP_PORT environment variable is required but not set");
    }
    const httpPort = parseInt(httpPortStr, 10);
    if (isNaN(httpPort) || httpPort <= 0 || httpPort > 65535) {
      throw new Error(`Invalid MCP_HTTP_PORT value: ${httpPortStr}`);
    }
    VibeLogger.logInfo("mcp_server_starting", "Starting MCP HTTP server", {
      http_port: httpPort,
      is_development: this.isDevelopment
    });
    const httpServer = new McpHttpServer({ port: httpPort });
    httpServer.setTransportCreatedCallback(async (transport) => {
      await this.server.connect(transport);
    });
    await httpServer.start();
    this.eventHandler.setHttpServer(httpServer);
    VibeLogger.logInfo("mcp_server_started", "MCP HTTP server started successfully", {
      http_port: httpPort
    });
  }
};
var server = new UnityMcpServer();
server.start().catch((error) => {
  VibeLogger.logError(
    "mcp_server_startup_fatal",
    "Unity MCP Server startup failed",
    {
      error_message: error instanceof Error ? error.message : String(error),
      stack_trace: error instanceof Error ? error.stack : "No stack trace available",
      error_type: error instanceof Error ? error.constructor.name : typeof error
    },
    void 0,
    "Fatal server startup error - check Unity MCP bridge configuration"
  );
  process.exit(1);
});
//# sourceMappingURL=server.bundle.js.map
