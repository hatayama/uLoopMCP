using System.Threading.Tasks;
using UnityEditor;
using Newtonsoft.Json;
using UnityEngine;


namespace io.github.hatayama.uLoopMCP
{
    // Related classes:
    // - McpBridgeServer: The TCP server instance that this class manages.
    // - McpEditorWindow: The UI for starting and stopping the server.
    // - AssemblyReloadEvents: Used to handle server state across domain reloads.
    /// <summary>
    /// Manages the state of the MCP Server with SessionState and automatically restores it on assembly reload.
    /// </summary>
    [InitializeOnLoad]
    public static class McpServerController
    {
        private static McpBridgeServer mcpServer;

        /// <summary>
        /// The current MCP server instance.
        /// </summary>
        public static McpBridgeServer CurrentServer => mcpServer;

        /// <summary>
        /// Whether the server is running.
        /// </summary>
        public static bool IsServerRunning => mcpServer?.IsRunning ?? false;

        /// <summary>
        /// The server's port number.
        /// </summary>
        public static int ServerPort => mcpServer?.Port ?? McpEditorSettings.GetCustomPort();

        static McpServerController()
        {
            // Register cleanup for when Unity exits.
            EditorApplication.quitting += OnEditorQuitting;

            // Processing before assembly reload.
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

            // Processing after assembly reload.
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;

            // Restore server state on initialization.
            RestoreServerStateIfNeeded();
        }

        /// <summary>
        /// Starts the server.
        /// </summary>
        /// <param name="port">
        /// The port number to bind to. Use -1 to fall back to the saved custom port
        /// from <see cref="McpEditorSettings.GetCustomPort"/>. Defaults to -1.
        /// </param>
        public static void StartServer(int port = -1)
        {
            // Use saved port if no port specified
            int actualPort = port == -1 ? McpEditorSettings.GetCustomPort() : port;

            // Find available port starting from the requested port
            int availablePort = FindAvailablePort(actualPort);

            // Show confirmation dialog if port was changed
            if (availablePort != actualPort)
            {
                bool userConfirmed = UnityEditor.EditorUtility.DisplayDialog(
                    "Port Conflict",
                    $"Port {actualPort} is already in use.\n\nWould you like to use port {availablePort} instead?",
                    "OK",
                    "Cancel"
                );

                if (!userConfirmed)
                {
                    return;
                }

                // Automatically update all configured MCP editor settings with new port
                McpPortChangeUpdater.UpdateAllConfigurationsForPortChange(availablePort, "Server port conflict resolution");
            }

            // Validate server configuration before starting
            ValidateServerConfiguration(availablePort);

            // Always stop the existing server (to release the port).
            if (mcpServer != null)
            {
                StopServer();
            }

            mcpServer = new McpBridgeServer();
            mcpServer.StartServer(availablePort);

            // Save the state to SessionState.
            McpSessionManager sessionManager = McpSessionManager.instance;
            sessionManager.IsServerRunning = true;
            sessionManager.ServerPort = availablePort;
        }

        /// <summary>
        /// Stops the server.
        /// </summary>
        public static void StopServer()
        {
            if (mcpServer != null)
            {
                mcpServer.Dispose();
                mcpServer = null;
            }

            // Delete the state from SessionState.
            McpSessionManager sessionManager = McpSessionManager.instance;
            sessionManager.ClearServerSession();
        }

        /// <summary>
        /// Processing before assembly reload.
        /// </summary>
        private static void OnBeforeAssemblyReload()
        {
            // Generate correlation ID for tracking this domain reload cycle
            string correlationId = VibeLogger.GenerateCorrelationId();

            // Set the domain reload start flag.
            McpSessionManager sessionManager = McpSessionManager.instance;
            sessionManager.IsDomainReloadInProgress = true;

            // Log server state before assembly reload
            bool serverRunning = mcpServer?.IsRunning ?? false;

            VibeLogger.LogInfo(
                "domain_reload_start",
                "Domain reload starting",
                new
                {
                    server_running = serverRunning,
                    server_port = mcpServer?.Port
                },
                correlationId
            );

            // If the server is running, save its state and stop it.
            if (mcpServer?.IsRunning == true)
            {
                int portToSave = mcpServer.Port;

                // Execute SessionState operations immediately (to ensure they are saved before a domain reload).
                sessionManager.IsServerRunning = true;
                sessionManager.ServerPort = portToSave;
                sessionManager.IsAfterCompile = true; // Set the post-compilation flag.
                sessionManager.IsReconnecting = true; // Set the reconnecting flag.
                sessionManager.ShowReconnectingUI = true; // Set the UI display flag.
                sessionManager.ShowPostCompileReconnectingUI = true; // Set the post-compile specific UI flag.

                // Stop the server completely (using Dispose to ensure the TCP connection is released).
                try
                {
                    VibeLogger.LogInfo(
                        "domain_reload_server_stopping",
                        "Stopping MCP server before domain reload",
                        new { port = portToSave },
                        correlationId
                    );

                    mcpServer.Dispose();
                    mcpServer = null;

                    VibeLogger.LogInfo(
                        "domain_reload_server_stopped",
                        "MCP server stopped successfully",
                        new { tcp_port_released = true },
                        correlationId
                    );
                }
                catch (System.Exception ex)
                {
                    VibeLogger.LogException(
                        "domain_reload_server_shutdown_error",
                        ex,
                        new
                        {
                            port = portToSave,
                            server_was_running = true
                        },
                        correlationId,
                        "Critical error during server shutdown before assembly reload. This may cause port conflicts on restart.",
                        "Investigate server shutdown process and ensure proper TCP port release."
                    );

                    // Don't suppress this exception - server shutdown failure could leave ports locked
                    // and cause startup issues after domain reload
                    throw new System.InvalidOperationException(
                        $"Failed to properly shutdown MCP server before assembly reload. This may cause port conflicts on restart.", ex);
                }
            }
        }

        /// <summary>
        /// Processing after assembly reload.
        /// </summary>
        private static void OnAfterAssemblyReload()
        {
            // Generate correlation ID for tracking this domain reload recovery
            string correlationId = VibeLogger.GenerateCorrelationId();

            // Clear the domain reload completion flag.
            McpSessionManager sessionManager = McpSessionManager.instance;
            sessionManager.ClearDomainReloadFlag();

            // Start UI timeout if UI display flag is set
            bool showReconnectingUI = sessionManager.ShowReconnectingUI;

            VibeLogger.LogInfo(
                "domain_reload_complete",
                "Domain reload completed - starting server recovery process",
                new { session_server_port = sessionManager.ServerPort },
                correlationId
            );

            if (showReconnectingUI)
            {
                StartReconnectionUITimeoutAsync().Forget();
            }

            // Update MCP configurations to match current ULOOPMCP_DEBUG state
            McpDebugStateUpdater.UpdateAllConfigurationsForDebugState();

            // Restore server state.
            RestoreServerStateIfNeeded();

            // Process pending compile requests.
            ProcessPendingCompileRequests();

            // Always send tool change notification after compilation
            // This ensures schema changes (descriptions, parameters) are communicated to Cursor
            if (IsServerRunning)
            {
                SendToolNotificationAfterCompilationAsync().Forget();
            }
        }

        /// <summary>
        /// Restores the server state if necessary.
        /// </summary>
        private static void RestoreServerStateIfNeeded()
        {
            McpSessionManager sessionManager = McpSessionManager.instance;
            bool wasRunning = sessionManager.IsServerRunning;
            int savedPort = sessionManager.ServerPort;
            bool isAfterCompile = sessionManager.IsAfterCompile;

            // If the server is already running (e.g., started from McpEditorWindow).
            if (mcpServer?.IsRunning == true)
            {
                // Just clear the post-compilation flag and exit.
                if (isAfterCompile)
                {
                    sessionManager.ClearAfterCompileFlag();
                }

                return;
            }

            // Clear the post-compilation flag.
            if (isAfterCompile)
            {
                sessionManager.ClearAfterCompileFlag();
            }

            if (wasRunning && (mcpServer == null || !mcpServer.IsRunning))
            {
                // If it's after a compilation, restart immediately (regardless of the Auto Start Server setting).
                if (isAfterCompile)
                {
                    // Wait a short while before restarting immediately (to release TCP port).
                    RestoreServerAfterCompileAsync(savedPort).Forget();
                }
                else
                {
                    // For non-compilation scenarios, such as Unity startup.
                    // Check the Auto Start Server setting.
                    bool autoStartEnabled = McpEditorSettings.GetAutoStartServer();

                    if (autoStartEnabled)
                    {
                        // Wait for Unity Editor to be ready before auto-starting
                        RestoreServerOnStartupAsync(savedPort).Forget();
                    }
                    else
                    {
                        // If Auto Start Server is off, do not start the server.
                        // Clear SessionState (wait for the server to be started manually).
                        sessionManager.ClearServerSession();
                    }
                }
            }
        }

        /// <summary>
        /// Executes server recovery with retries.
        /// </summary>
        private static void TryRestoreServerWithRetry(int port, int retryCount)
        {
            const int maxRetries = 3;

            try
            {
                // If there is an existing server instance, ensure it is stopped.
                if (mcpServer != null)
                {
                    mcpServer.Dispose();
                    mcpServer = null;
                }

                // Find available port starting from the requested port
                int availablePort = FindAvailablePort(port);

                mcpServer = new McpBridgeServer();
                mcpServer.StartServer(availablePort);

                // Update session manager with the actual port used
                McpSessionManager sessionManager = McpSessionManager.instance;
                sessionManager.ServerPort = availablePort;

                // Clear server-side reconnecting flag on successful restoration
                // NOTE: Do NOT clear UI display flag here - let it be cleared by timeout or client connection
                sessionManager.IsReconnecting = false;

                // Tools changed notification will be sent by OnAfterAssemblyReload
            }
            catch (System.Exception)
            {
                // If the maximum number of retries has not been reached, try again.
                if (retryCount < maxRetries)
                {
                    // Wait for port release before retry
                    RetryServerRestoreAsync(port, retryCount).Forget();
                }
                else
                {
                    // If it ultimately fails, clear the SessionState.
                    McpSessionManager.instance.ClearServerSession();
                }
            }
        }

        /// <summary>
        /// Cleanup on Unity exit.
        /// </summary>
        private static void OnEditorQuitting()
        {
            StopServer();
        }

        /// <summary>
        /// Processes pending compile requests.
        /// </summary>
        private static void ProcessPendingCompileRequests()
        {
            // Temporarily disabled to avoid main thread errors due to SessionState operations.
            // TODO: Re-enable after resolving the main thread issue.
            // CompileSessionState.StartForcedRecompile();
        }

        /// <summary>
        /// Gets server status information.
        /// </summary>
        public static (bool isRunning, int port, bool wasRestoredFromSession) GetServerStatus()
        {
            bool wasRestored = McpSessionManager.instance.IsServerRunning;
            return (IsServerRunning, ServerPort, wasRestored);
        }

        /// <summary>
        /// Send tools changed notification to TypeScript side
        /// </summary>
        private static void SendToolsChangedNotification()
        {
            // Log with stack trace to identify caller
            System.Diagnostics.StackTrace stackTrace = new System.Diagnostics.StackTrace(true);
            string callerInfo = stackTrace.GetFrame(1)?.GetMethod()?.Name ?? "Unknown";

            if (mcpServer == null)
            {
                return;
            }

            // Send MCP standard notification only
            var notificationParams = new
            {
                timestamp = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                message = "Unity tools have been updated"
            };

            var mcpNotification = new
            {
                jsonrpc = McpServerConfig.JSONRPC_VERSION,
                method = "notifications/tools/list_changed",
                @params = notificationParams
            };

            string mcpNotificationJson = JsonConvert.SerializeObject(mcpNotification);
            mcpServer.SendNotificationToClients(mcpNotificationJson);
        }

        /// <summary>
        /// Manually trigger tool change notification
        /// Public method for external calls (e.g., from UnityToolRegistry)
        /// </summary>
        public static void TriggerToolChangeNotification()
        {
            if (IsServerRunning)
            {
                SendToolsChangedNotification();
            }
        }

        /// <summary>
        /// Send tool notification after compilation with frame delay
        /// </summary>
        private static async Task SendToolNotificationAfterCompilationAsync()
        {
            // Use frame delay for timing adjustment after domain reload
            // This ensures Unity Editor is in a stable state before sending notifications
            await EditorDelay.DelayFrame(1);

            CustomToolManager.NotifyToolChanges();
        }


        /// <summary>
        /// Restore server after compilation with frame delay
        /// </summary>
        private static async Task RestoreServerAfterCompileAsync(int port)
        {
            // Wait a short while for timing adjustment (TCP port release)
            await EditorDelay.DelayFrame(1);

            TryRestoreServerWithRetry(port, 0);
        }

        /// <summary>
        /// Restore server on startup with frame delay
        /// </summary>
        private static async Task RestoreServerOnStartupAsync(int port)
        {
            // Wait for Unity Editor to be ready before auto-starting
            await EditorDelay.DelayFrame(1);

            TryRestoreServerWithRetry(port, 0);
        }


        /// <summary>
        /// Retry server restore with frame delay
        /// </summary>
        private static async Task RetryServerRestoreAsync(int port, int retryCount)
        {
            // Wait longer for port release before retry
            await EditorDelay.DelayFrame(5);

            // Do not change the port number; retry with the same port
            TryRestoreServerWithRetry(port, retryCount + 1);
        }

        /// <summary>
        /// Start UI display timeout timer for reconnecting message
        /// </summary>
        private static async Task StartReconnectionUITimeoutAsync()
        {
            // Wait for the timeout period (convert seconds to frames at ~60fps)
            int timeoutFrames = McpConstants.RECONNECTION_TIMEOUT_SECONDS * 60;
            await EditorDelay.DelayFrame(timeoutFrames);

            // Check if UI flag is still set after timeout
            McpSessionManager sessionManager = McpSessionManager.instance;
            bool isStillShowingUI = sessionManager.ShowReconnectingUI;
            if (isStillShowingUI)
            {
                sessionManager.ClearReconnectingFlags();
            }
        }

        /// <summary>
        /// Clear reconnecting flags when client connects
        /// Called by UI or bridge server when client connection is detected
        /// </summary>
        public static void ClearReconnectingFlag()
        {
            McpSessionManager sessionManager = McpSessionManager.instance;
            bool wasReconnecting = sessionManager.IsReconnecting;
            bool wasShowingUI = sessionManager.ShowReconnectingUI;

            if (wasReconnecting || wasShowingUI)
            {
                sessionManager.ClearReconnectingFlags();
            }
        }

        /// <summary>
        /// Finds an available port starting from the given port number
        /// Delegates to NetworkUtility for consistent port finding behavior.
        /// </summary>
        /// <param name="startPort">The starting port number to check</param>
        /// <returns>The first available port number</returns>
        private static int FindAvailablePort(int startPort)
        {
            return NetworkUtility.FindAvailablePort(startPort);
        }

        /// <summary>
        /// Validates server configuration before starting
        /// Implements fail-fast behavior for invalid configurations
        /// </summary>
        private static void ValidateServerConfiguration(int port)
        {
            // Validate port number using shared validator
            if (!McpPortValidator.ValidatePort(port, "for MCP server"))
            {
                throw new System.ArgumentOutOfRangeException(nameof(port),
                    $"Port number must be between 1 and 65535. Received: {port}");
            }

            // Validate Unity Editor state
            if (EditorApplication.isCompiling)
            {
                throw new System.InvalidOperationException(
                    "Cannot start MCP server while Unity is compiling. Please wait for compilation to complete.");
            }

            // Server configuration validation passed
            // Note: Port availability and system port conflicts are handled by FindAvailablePort
        }
    }
}