using UnityEngine;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Graceful Shutdown UseCase - Encapsulates temporal cohesion for domain reload shutdown process
    /// 
    /// Design document reference: .kiro/specs/mcp-domain-reload-shutdown/design.md
    /// 
    /// Related classes:
    /// - McpBridgeServer: The server instance to be gracefully shutdown
    /// - McpSessionManager: Session state persistence across domain reload
    /// - VibeLogger: Structured logging for domain reload tracking
    /// 
    /// This UseCase class follows the single-use pattern:
    /// 1. new() - create instance
    /// 2. execute() - perform all shutdown steps in temporal order
    /// 3. instance is discarded after use (not reused)
    /// 
    /// Temporal cohesion benefits:
    /// - All shutdown steps are contained in one place
    /// - Clear execution order and dependencies
    /// - Single point of failure handling
    /// - Easy to test and reason about
    /// </summary>
    public class GracefulShutdownUseCase
    {
        private readonly string correlationId;
        private readonly McpBridgeServer serverToShutdown;

        public GracefulShutdownUseCase(McpBridgeServer server)
        {
            this.serverToShutdown = server;
            this.correlationId = VibeLogger.GenerateCorrelationId();
        }

        /// <summary>
        /// Execute complete graceful shutdown process for domain reload
        /// This method contains all shutdown steps in temporal order
        /// Should be called only once per instance
        /// </summary>
        public GracefulShutdownResult Execute()
        {
            VibeLogger.LogInfo(
                "graceful_shutdown_usecase_start",
                "Starting graceful shutdown process for domain reload",
                new
                {
                    server_running = serverToShutdown?.IsRunning ?? false,
                    server_port = serverToShutdown?.Port,
                    has_server_instance = serverToShutdown != null
                },
                correlationId,
                "UseCase pattern: Single-use graceful shutdown with temporal cohesion",
                "Track this correlation ID for complete shutdown flow"
            );

            try
            {
                // Step 1: Set domain reload flag
                SetDomainReloadFlag();

                // Step 2: Log initial server state
                LogInitialServerState();

                // Step 3: Save session state if server is running
                if (serverToShutdown?.IsRunning == true)
                {
                    int portToSave = SaveSessionState();

                    // Step 4: Stop server gracefully
                    StopServerGracefully(portToSave);

                    // Step 5: Clear server instance references
                    ClearServerReferences();

                    VibeLogger.LogInfo(
                        "graceful_shutdown_usecase_success",
                        "Graceful shutdown completed successfully",
                        new
                        {
                            port_saved = portToSave,
                            tcp_port_released = true
                        },
                        correlationId,
                        "UseCase completed - server shutdown gracefully for domain reload"
                    );

                    return GracefulShutdownResult.Success(portToSave);
                }
                else
                {
                    // Step 6: Handle case where server was not running
                    HandleServerNotRunning();

                    VibeLogger.LogInfo(
                        "graceful_shutdown_usecase_success_no_server",
                        "Graceful shutdown completed - no running server to shutdown",
                        new { server_was_running = false },
                        correlationId,
                        "UseCase completed - no server was running to shutdown"
                    );

                    return GracefulShutdownResult.NoServerToShutdown();
                }
            }
            catch (System.Exception error)
            {
                VibeLogger.LogException(
                    "graceful_shutdown_usecase_failure",
                    error,
                    new
                    {
                        server_port = serverToShutdown?.Port,
                        server_was_running = serverToShutdown?.IsRunning ?? false
                    },
                    correlationId,
                    "Critical error during graceful shutdown before assembly reload. This may cause port conflicts on restart.",
                    "Investigate server shutdown process and ensure proper TCP port release."
                );

                return GracefulShutdownResult.Error(
                    $"Failed to properly shutdown MCP server before assembly reload. This may cause port conflicts on restart.",
                    error
                );
            }
        }

        /// <summary>
        /// Step 1: Set domain reload flag
        /// </summary>
        private void SetDomainReloadFlag()
        {
            VibeLogger.LogDebug(
                "graceful_shutdown_step_1",
                "Setting domain reload flag",
                new { },
                correlationId,
                "Step 1: Domain reload flag setting"
            );

            McpSessionManager sessionManager = McpSessionManager.instance;
            sessionManager.IsDomainReloadInProgress = true;

            VibeLogger.LogDebug(
                "graceful_shutdown_step_1_complete",
                "Domain reload flag set",
                new { domain_reload_in_progress = true },
                correlationId,
                "Step 1 complete: Domain reload flag configured"
            );
        }

        /// <summary>
        /// Step 2: Log initial server state for debugging
        /// </summary>
        private void LogInitialServerState()
        {
            VibeLogger.LogDebug(
                "graceful_shutdown_step_2",
                "Logging initial server state",
                new
                {
                    server_running = serverToShutdown?.IsRunning ?? false,
                    server_port = serverToShutdown?.Port
                },
                correlationId,
                "Step 2: Initial server state logging"
            );

            VibeLogger.LogInfo(
                "domain_reload_start",
                "Domain reload starting",
                new
                {
                    server_running = serverToShutdown?.IsRunning ?? false,
                    server_port = serverToShutdown?.Port
                },
                correlationId
            );

            VibeLogger.LogDebug(
                "graceful_shutdown_step_2_complete",
                "Initial server state logged",
                new { },
                correlationId,
                "Step 2 complete: Server state captured for domain reload tracking"
            );
        }

        /// <summary>
        /// Step 3: Save session state for server restoration after domain reload
        /// </summary>
        private int SaveSessionState()
        {
            int portToSave = serverToShutdown.Port;

            VibeLogger.LogDebug(
                "graceful_shutdown_step_3",
                "Saving session state for server restoration",
                new
                {
                    port_to_save = portToSave,
                    server_running = true
                },
                correlationId,
                "Step 3: Session state persistence for restoration"
            );

            McpSessionManager sessionManager = McpSessionManager.instance;
            
            // Execute SessionState operations immediately (to ensure they are saved before a domain reload)
            sessionManager.IsServerRunning = true;
            sessionManager.ServerPort = portToSave;
            sessionManager.IsAfterCompile = true; // Set the post-compilation flag
            sessionManager.IsReconnecting = true; // Set the reconnecting flag
            sessionManager.ShowReconnectingUI = true; // Set the UI display flag
            sessionManager.ShowPostCompileReconnectingUI = true; // Set the post-compile specific UI flag

            VibeLogger.LogDebug(
                "graceful_shutdown_step_3_complete",
                "Session state saved for restoration",
                new
                {
                    port_saved = portToSave,
                    is_server_running = true,
                    is_after_compile = true,
                    is_reconnecting = true,
                    show_reconnecting_ui = true,
                    show_post_compile_ui = true
                },
                correlationId,
                "Step 3 complete: All session flags configured for proper restoration"
            );

            return portToSave;
        }

        /// <summary>
        /// Step 4: Stop server gracefully with proper error handling
        /// </summary>
        private void StopServerGracefully(int port)
        {
            VibeLogger.LogDebug(
                "graceful_shutdown_step_4",
                "Stopping server gracefully",
                new { port },
                correlationId,
                "Step 4: Graceful server shutdown"
            );

            VibeLogger.LogInfo(
                "domain_reload_server_stopping",
                "Stopping MCP server before domain reload",
                new { port },
                correlationId
            );

            // This is the critical step - ensure the TCP connection is properly released
            serverToShutdown.Dispose();

            VibeLogger.LogInfo(
                "domain_reload_server_stopped",
                "MCP server stopped successfully",
                new { tcp_port_released = true },
                correlationId
            );

            VibeLogger.LogDebug(
                "graceful_shutdown_step_4_complete",
                "Server stopped gracefully",
                new
                {
                    port,
                    server_disposed = true,
                    tcp_port_released = true
                },
                correlationId,
                "Step 4 complete: Server resources properly released"
            );
        }

        /// <summary>
        /// Step 5: Clear server instance references
        /// </summary>
        private void ClearServerReferences()
        {
            VibeLogger.LogDebug(
                "graceful_shutdown_step_5",
                "Clearing server instance references",
                new { },
                correlationId,
                "Step 5: Server reference cleanup"
            );

            // This needs to be done through McpServerController to maintain proper state
            McpServerController.ClearServerInstance();

            VibeLogger.LogDebug(
                "graceful_shutdown_step_5_complete",
                "Server references cleared",
                new { server_instance_cleared = true },
                correlationId,
                "Step 5 complete: Server instance references cleaned up"
            );
        }

        /// <summary>
        /// Step 6: Handle case where server was not running
        /// </summary>
        private void HandleServerNotRunning()
        {
            VibeLogger.LogDebug(
                "graceful_shutdown_step_6",
                "Handling no-server-running case",
                new { server_was_running = false },
                correlationId,
                "Step 6: No server to shutdown - cleanup only"
            );

            // No specific action needed - domain reload flag is already set

            VibeLogger.LogDebug(
                "graceful_shutdown_step_6_complete",
                "No-server case handled",
                new { },
                correlationId,
                "Step 6 complete: No server was running to shutdown"
            );
        }
    }

    /// <summary>
    /// Result object for graceful shutdown operation
    /// </summary>
    public class GracefulShutdownResult
    {
        public bool IsSuccess { get; private set; }
        public string ErrorMessage { get; private set; }
        public System.Exception Exception { get; private set; }
        public int? SavedPort { get; private set; }
        public GracefulShutdownReason Reason { get; private set; }

        private GracefulShutdownResult(bool isSuccess, GracefulShutdownReason reason, string errorMessage = null, System.Exception exception = null, int? savedPort = null)
        {
            IsSuccess = isSuccess;
            Reason = reason;
            ErrorMessage = errorMessage;
            Exception = exception;
            SavedPort = savedPort;
        }

        public static GracefulShutdownResult Success(int savedPort)
        {
            return new GracefulShutdownResult(true, GracefulShutdownReason.ServerShutdownGracefully, savedPort: savedPort);
        }

        public static GracefulShutdownResult NoServerToShutdown()
        {
            return new GracefulShutdownResult(true, GracefulShutdownReason.NoServerRunning);
        }

        public static GracefulShutdownResult Error(string errorMessage, System.Exception exception)
        {
            return new GracefulShutdownResult(false, GracefulShutdownReason.ShutdownError, errorMessage, exception);
        }
    }

    /// <summary>
    /// Enumeration of possible graceful shutdown outcomes
    /// </summary>
    public enum GracefulShutdownReason
    {
        ServerShutdownGracefully,
        NoServerRunning,
        ShutdownError
    }
}