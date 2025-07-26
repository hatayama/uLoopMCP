using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Application service responsible for Domain Reload detection and state management
    /// Single responsibility: Domain Reload lifecycle management
    /// Related classes: McpSessionManager, McpServerController
    /// Design reference: @Packages/docs/ARCHITECTURE_Unity.md - Application Service Layer (Single Function Implementation)
    /// </summary>
    public static class DomainReloadDetectionService
    {
        /// <summary>
        /// Execute Domain Reload start processing
        /// </summary>
        /// <param name="correlationId">Tracking ID for related operations</param>
        /// <param name="serverIsRunning">Whether server is running</param>
        /// <param name="serverPort">Server port number</param>
        public static void StartDomainReload(string correlationId, bool serverIsRunning, int? serverPort)
        {
            McpSessionManager sessionManager = McpSessionManager.instance;
            
            // Set Domain Reload in progress flag
            sessionManager.IsDomainReloadInProgress = true;

            // Save session state if server is running
            if (serverIsRunning && serverPort.HasValue)
            {
                sessionManager.IsServerRunning = true;
                sessionManager.ServerPort = serverPort.Value;
                sessionManager.IsAfterCompile = true;
                sessionManager.IsReconnecting = true;
                sessionManager.ShowReconnectingUI = true;
                sessionManager.ShowPostCompileReconnectingUI = true;
            }

            // Log recording
            VibeLogger.LogInfo(
                "domain_reload_start",
                "Domain reload starting",
                new
                {
                    server_running = serverIsRunning,
                    server_port = serverPort
                },
                correlationId
            );
        }

        /// <summary>
        /// Execute Domain Reload completion processing
        /// </summary>
        /// <param name="correlationId">Tracking ID for related operations</param>
        public static void CompleteDomainReload(string correlationId)
        {
            McpSessionManager sessionManager = McpSessionManager.instance;
            
            // Clear Domain Reload completion flag
            sessionManager.ClearDomainReloadFlag();

            // Log recording
            VibeLogger.LogInfo(
                "domain_reload_complete",
                "Domain reload completed - starting server recovery process",
                new { session_server_port = sessionManager.ServerPort },
                correlationId
            );
        }

        /// <summary>
        /// Check if currently in Domain Reload
        /// </summary>
        /// <returns>True if Domain Reload is in progress</returns>
        public static bool IsDomainReloadInProgress()
        {
            return McpSessionManager.instance.IsDomainReloadInProgress;
        }

        /// <summary>
        /// Check if reconnection UI display is required
        /// </summary>
        /// <returns>True if reconnection UI display is required</returns>
        public static bool ShouldShowReconnectingUI()
        {
            return McpSessionManager.instance.ShowReconnectingUI;
        }

        /// <summary>
        /// Check if in after-compile state
        /// </summary>
        /// <returns>True if after compile</returns>
        public static bool IsAfterCompile()
        {
            return McpSessionManager.instance.IsAfterCompile;
        }
    }
}