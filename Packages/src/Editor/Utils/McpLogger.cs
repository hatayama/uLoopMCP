using System.Diagnostics;
using UnityEngine;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Class for unified management of Unity MCP Server related logs
    /// </summary>
    public class McpLogger : ScriptableSingleton<McpLogger>
    {
        private const string LOG_PREFIX = "[uLoopMCP]";
        
        [SerializeField] private bool _enableDebugLog = false;
        
        /// <summary>
        /// Whether to output debug logs
        /// </summary>
        public static bool EnableDebugLog 
        { 
            get => instance._enableDebugLog; 
            set => instance._enableDebugLog = value; 
        }
        
        /// <summary>
        /// Output information log
        /// </summary>
        [Conditional(McpConstants.MCP_DEBUG)]
        public static void LogInfo(string message)
        {
            if (EnableDebugLog)
            {
                Debug.Log($"{LOG_PREFIX} {message}");
            }
        }
        
        /// <summary>
        /// Output warning log
        /// Always outputs warnings regardless of EnableDebugLog setting
        /// </summary>
        [Conditional(McpConstants.MCP_DEBUG)]
        public static void LogWarning(string message)
        {
            // Warnings should also always be logged as they indicate potential issues
            Debug.LogWarning($"{LOG_PREFIX} {message}");
        }
        
        /// <summary>
        /// Output error log
        /// Always outputs errors regardless of EnableDebugLog setting
        /// </summary>
        [Conditional(McpConstants.MCP_DEBUG)]
        public static void LogError(string message)
        {
            // Errors should ALWAYS be logged regardless of debug settings
            // This is critical for troubleshooting and fail-fast behavior
            Debug.LogError($"{LOG_PREFIX} {message}");
        }
        
        /// <summary>
        /// Output debug log (only when EnableDebugLog is true)
        /// </summary>
        [Conditional(McpConstants.MCP_DEBUG)]
        public static void LogDebug(string message)
        {
            if (EnableDebugLog)
            {
                Debug.Log($"{LOG_PREFIX} [DEBUG] {message}");
            }
        }
        
        /// <summary>
        /// Output JSON-RPC communication log
        /// </summary>
        [Conditional(McpConstants.MCP_DEBUG)]
        public static void LogJsonRpc(string direction, string content)
        {
            if (EnableDebugLog)
            {
                Debug.Log($"{LOG_PREFIX} [JSON-RPC {direction}] {content}");
            }
        }
        
        /// <summary>
        /// Output client connection log
        /// </summary>
        [Conditional(McpConstants.MCP_DEBUG)]
        public static void LogClientConnection(string clientEndpoint, bool connected)
        {
            string status = connected ? "connected" : "disconnected";
            LogInfo($"Client {status}: {clientEndpoint}");
        }
    }
} 