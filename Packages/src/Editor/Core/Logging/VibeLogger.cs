using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// AI-friendly structured logger for Unity MCP
    /// 
    /// Design document reference: Packages/src/TypeScriptServer~/ARCHITECTURE.md
    /// 
    /// Related classes:
    /// - McpLogger: Traditional Unity logger that this class replaces
    /// - McpServerController: Uses this logger for domain reload tracking
    /// - UnityMcpServer: Uses this logger for connection event tracking
    /// 
    /// Key features:
    /// - Structured JSON logging with operation, context, correlation_id
    /// - AI-friendly format for Claude Code analysis
    /// - Automatic file rotation and memory management
    /// - Correlation ID tracking for related operations
    /// </summary>
    public static class VibeLogger
    {
        private static readonly string LOG_DIRECTORY = Path.Combine(Application.dataPath, "..", McpConstants.OUTPUT_ROOT_DIR, McpConstants.VIBE_LOGS_DIR);
        private static readonly string LOG_FILE_PREFIX = "unity_vibe";
        private static readonly int MAX_FILE_SIZE_MB = 10;
        private static readonly int MAX_MEMORY_LOGS = 1000;
        
        private static readonly List<VibeLogEntry> memoryLogs = new();
        private static readonly object lockObject = new();
        
        [Serializable]
        public class VibeLogEntry
        {
            public string timestamp;
            public string level;
            public string operation;
            public string message;
            public object context;
            public string correlation_id;
            public string source;
            public string human_note;
            public string ai_todo;
            public EnvironmentInfo environment;
        }
        
        [Serializable]
        public class EnvironmentInfo
        {
            public string unity_version;
            public string platform;
            public string editor_mode;
            public string domain_reload_state;
        }
        
        /// <summary>
        /// Log an info level message with structured context
        /// Only logs when ULOOPMCP_DEBUG symbol is defined
        /// </summary>
        [Conditional(McpConstants.MCP_DEBUG)]
        public static void LogInfo(string operation, string message, object context = null, 
                                  string correlationId = null, string humanNote = null, string aiTodo = null)
        {
            Log("INFO", operation, message, context, correlationId, humanNote, aiTodo);
        }
        
        /// <summary>
        /// Log a warning level message with structured context
        /// Only logs when ULOOPMCP_DEBUG symbol is defined
        /// </summary>
        [Conditional(McpConstants.MCP_DEBUG)]
        public static void LogWarning(string operation, string message, object context = null, 
                                     string correlationId = null, string humanNote = null, string aiTodo = null)
        {
            Log("WARNING", operation, message, context, correlationId, humanNote, aiTodo);
        }
        
        /// <summary>
        /// Log an error level message with structured context
        /// Only logs when ULOOPMCP_DEBUG symbol is defined
        /// </summary>
        [Conditional(McpConstants.MCP_DEBUG)]
        public static void LogError(string operation, string message, object context = null, 
                                   string correlationId = null, string humanNote = null, string aiTodo = null)
        {
            Log("ERROR", operation, message, context, correlationId, humanNote, aiTodo);
        }
        
        /// <summary>
        /// Log a debug level message with structured context
        /// Only logs when ULOOPMCP_DEBUG symbol is defined
        /// </summary>
        [Conditional(McpConstants.MCP_DEBUG)]
        public static void LogDebug(string operation, string message, object context = null, 
                                   string correlationId = null, string humanNote = null, string aiTodo = null)
        {
            Log("DEBUG", operation, message, context, correlationId, humanNote, aiTodo);
        }
        
        /// <summary>
        /// Log an exception with structured context
        /// Only logs when ULOOPMCP_DEBUG symbol is defined
        /// </summary>
        [Conditional(McpConstants.MCP_DEBUG)]
        public static void LogException(string operation, Exception exception, object context = null, 
                                       string correlationId = null, string humanNote = null, string aiTodo = null)
        {
            var exceptionContext = new Dictionary<string, object>();
            if (context != null)
            {
                exceptionContext["original_context"] = context;
            }
            
            exceptionContext["exception"] = new
            {
                type = exception.GetType().Name,
                message = exception.Message,
                stack_trace = exception.StackTrace,
                inner_exception = exception.InnerException?.Message
            };
            
            Log("ERROR", operation, $"Exception occurred: {exception.Message}", exceptionContext, 
                correlationId, humanNote, aiTodo);
        }
        
        /// <summary>
        /// Generate a new correlation ID for tracking related operations
        /// </summary>
        public static string GenerateCorrelationId()
        {
            return $"unity_{Guid.NewGuid().ToString("N")[..8]}_{DateTime.Now:HHmmss}";
        }
        
        /// <summary>
        /// Get logs for AI analysis (formatted for Claude Code)
        /// Output directory: {project_root}/uLoopMCPOutputs/VibeLogs/
        /// </summary>
        public static string GetLogsForAi(string operation = null, string correlationId = null, int maxCount = 100)
        {
            lock (lockObject)
            {
                var filteredLogs = new List<VibeLogEntry>(memoryLogs);
                
                if (!string.IsNullOrEmpty(operation))
                {
                    filteredLogs = filteredLogs.FindAll(log => log.operation.Contains(operation));
                }
                
                if (!string.IsNullOrEmpty(correlationId))
                {
                    filteredLogs = filteredLogs.FindAll(log => log.correlation_id == correlationId);
                }
                
                if (filteredLogs.Count > maxCount)
                {
                    filteredLogs = filteredLogs.GetRange(filteredLogs.Count - maxCount, maxCount);
                }
                
                return JsonConvert.SerializeObject(filteredLogs, Formatting.Indented);
            }
        }
        
        /// <summary>
        /// Clear all memory logs
        /// </summary>
        public static void ClearMemoryLogs()
        {
            lock (lockObject)
            {
                memoryLogs.Clear();
            }
        }
        
        /// <summary>
        /// Core logging method
        /// </summary>
        private static void Log(string level, string operation, string message, object context,
                               string correlationId, string humanNote, string aiTodo)
        {
            var logEntry = new VibeLogEntry
            {
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                level = level,
                operation = operation,
                message = message,
                context = context,
                correlation_id = correlationId ?? GenerateCorrelationId(),
                source = "Unity",
                human_note = humanNote,
                ai_todo = aiTodo,
                environment = GetEnvironmentInfo()
            };
            
            // Add to memory logs
            lock (lockObject)
            {
                memoryLogs.Add(logEntry);
                
                // Rotate memory logs if too many
                if (memoryLogs.Count > MAX_MEMORY_LOGS)
                {
                    memoryLogs.RemoveAt(0);
                }
            }
            
            // Save to file
            try
            {
                SaveLogToFile(logEntry);
            }
            catch (Exception ex)
            {
                // Fallback to Unity console if file logging fails
                UnityEngine.Debug.LogError($"[VibeLogger] Failed to save log to file: {ex.Message}");
                UnityEngine.Debug.Log($"[VibeLogger] {level} | {operation} | {message}");
            }
        }
        
        /// <summary>
        /// Save log entry to file with file locking for concurrent access
        /// </summary>
        private static void SaveLogToFile(VibeLogEntry logEntry)
        {
            if (!Directory.Exists(LOG_DIRECTORY))
            {
                Directory.CreateDirectory(LOG_DIRECTORY);
            }
            
            string fileName = $"{LOG_FILE_PREFIX}_{DateTime.UtcNow:yyyyMMdd}.json";
            string filePath = Path.Combine(LOG_DIRECTORY, fileName);
            
            // Check file size and rotate if necessary
            if (File.Exists(filePath))
            {
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > MAX_FILE_SIZE_MB * 1024 * 1024)
                {
                    string rotatedFileName = $"{LOG_FILE_PREFIX}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
                    string rotatedFilePath = Path.Combine(LOG_DIRECTORY, rotatedFileName);
                    File.Move(filePath, rotatedFilePath);
                }
            }
            
            string jsonLog = JsonConvert.SerializeObject(logEntry) + "\n";
            
            // Use file locking with retry mechanism to handle concurrent access
            int maxRetries = 3;
            int retryDelayMs = 50;
            
            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    using (var fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                    using (var writer = new StreamWriter(fileStream))
                    {
                        writer.Write(jsonLog);
                        writer.Flush();
                        return; // Success - exit retry loop
                    }
                }
                catch (IOException ex) when (IsFileSharingViolation(ex) && retry < maxRetries - 1)
                {
                    // Wait and retry for sharing violations
                    System.Threading.Thread.Sleep(retryDelayMs * (retry + 1));
                }
                catch (Exception ex)
                {
                    // For other exceptions, throw immediately
                    throw new InvalidOperationException($"Failed to write VibeLogger entry to file: {ex.Message}", ex);
                }
            }
            
            // If all retries failed, throw with sharing violation context
            throw new InvalidOperationException($"Failed to write VibeLogger entry after {maxRetries} retries due to file sharing violations");
        }
        
        /// <summary>
        /// Check if exception is a file sharing violation
        /// </summary>
        private static bool IsFileSharingViolation(IOException ex)
        {
            // ERROR_SHARING_VIOLATION (0x80070020)
            const int ERROR_SHARING_VIOLATION = unchecked((int)0x80070020);
            return ex.HResult == ERROR_SHARING_VIOLATION;
        }
        
        /// <summary>
        /// Get current environment information
        /// </summary>
        private static EnvironmentInfo GetEnvironmentInfo()
        {
            return new EnvironmentInfo
            {
                unity_version = Application.unityVersion,
                platform = Application.platform.ToString(),
                editor_mode = Application.isEditor ? "Editor" : "Runtime",
                domain_reload_state = McpSessionManager.instance?.IsDomainReloadInProgress == true ? "InProgress" : "Idle"
            };
        }
    }
}