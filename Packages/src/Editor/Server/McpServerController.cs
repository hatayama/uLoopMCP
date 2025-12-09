using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
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
        private static readonly SemaphoreSlim StartupSemaphore = new SemaphoreSlim(1, 1);
        private static long startupProtectionUntilTicks = 0; // UTC ticks

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

            // Initialize connected tools monitoring service
            // Note: ConnectedToolsMonitoringService has [InitializeOnLoad] so it's automatically initialized
            // This comment ensures the service initialization order is documented

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
        public static async void StartServer(int port = -1)
        {
            await StartServerWithUseCaseAsync(port);
        }

        /// <summary>
        /// Starts the server using new UseCase implementation.
        /// </summary>
        private static async Task StartServerWithUseCaseAsync(int port)
        {
            // Always stop the existing server first (to release the port)
            if (mcpServer != null)
            {
                await StopServerWithUseCaseAsync();
            }

            // Execute initialization UseCase
            McpServerInitializationUseCase useCase = new();
            ServerInitializationSchema schema = new() { Port = port };
            System.Threading.CancellationToken cancellationToken = System.Threading.CancellationToken.None;

            var result = await useCase.ExecuteAsync(schema, cancellationToken);

            if (result.Success)
            {
                // UseCase creates a new server instance, so we keep a reference here
                // for compatibility with existing code
                mcpServer = result.ServerInstance;
            }
            else
            {
                // Error message already handled by UseCase
                UnityEngine.Debug.LogError($"Server startup failed: {result.Message}");
            }
        }

        /// <summary>
        /// Stops the server.
        /// </summary>
        public static async void StopServer()
        {
            await StopServerWithUseCaseAsync();
        }

        /// <summary>
        /// Stops the server using new UseCase implementation.
        /// </summary>
        private static async Task StopServerWithUseCaseAsync()
        {
            // Execute shutdown UseCase
            McpServerShutdownUseCase useCase = new(new McpServerStartupService());
            ServerShutdownSchema schema = new() { ForceShutdown = false };
            System.Threading.CancellationToken cancellationToken = System.Threading.CancellationToken.None;

            var result = await useCase.ExecuteAsync(schema, cancellationToken);

            if (result.Success)
            {
                // Server stopped by UseCase, so clear the reference
                mcpServer = null;
            }
            else
            {
                // Error message already handled by UseCase
                UnityEngine.Debug.LogError($"Server shutdown failed: {result.Message}");
            }
        }

        /// <summary>
        /// Processing before assembly reload.
        /// </summary>
        private static void OnBeforeAssemblyReload()
        {
            // Create and execute DomainReloadRecoveryUseCase instance
            DomainReloadRecoveryUseCase useCase = new();
            ServiceResult<string> result = useCase.ExecuteBeforeDomainReload(mcpServer);
            
            // Clear instance if server shutdown succeeded
            if (result.Success)
            {
                mcpServer = null;
            }
        }

        /// <summary>
        /// Processing after assembly reload.
        /// </summary>
        private static void OnAfterAssemblyReload()
        {
            // Create and execute DomainReloadRecoveryUseCase instance
            DomainReloadRecoveryUseCase useCase = new();
            _ = useCase.ExecuteAfterDomainReloadAsync(System.Threading.CancellationToken.None).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    UnityEngine.Debug.LogError($"Domain reload recovery failed: {task.Exception}");
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());

            // Synchronize MCP editor configurations with current debug symbol state after domain reload
            // Use delayCall to ensure the editor is fully initialized before file I/O
            EditorApplication.delayCall += () =>
            {
                try
                {
                    McpDebugStateUpdater.UpdateAllConfigurationsForDebugState();
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"MCP debug-state configuration sync failed: {ex.Message}");
                }
            };
        }

        /// <summary>
        /// Restores the server state if necessary.
        /// </summary>
        private static void RestoreServerStateIfNeeded()
        {
            bool wasRunning = McpEditorSettings.GetIsServerRunning();
            int savedPort = McpEditorSettings.GetServerPort();
            bool isAfterCompile = McpEditorSettings.GetIsAfterCompile();

            // If the server is already running (e.g., started from McpEditorWindow).
            if (mcpServer?.IsRunning == true)
            {
                // Just clear the post-compilation flag and exit.
                if (isAfterCompile)
                {
                    McpEditorSettings.ClearAfterCompileFlag();
                }

                return;
            }

            // Clear the post-compilation flag.
            if (isAfterCompile)
            {
                McpEditorSettings.ClearAfterCompileFlag();
            }

            if (!wasRunning)
            {
                return;
            }

            if (mcpServer != null && mcpServer.IsRunning)
            {
                return;
            }

            bool autoStartEnabled = McpEditorSettings.GetAutoStartServer();
            if (!autoStartEnabled && !isAfterCompile)
            {
                McpEditorSettings.ClearServerSession();
                return;
            }

            // Centralized, coalesced startup request
            _ = StartRecoveryIfNeededAsync(savedPort, isAfterCompile, CancellationToken.None).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    VibeLogger.LogError("server_startup_restore_failed",
                        $"Failed to restore server: {task.Exception?.GetBaseException().Message}");
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>
        /// Executes server recovery with retries on the original port.
        /// Does not change the port number; only attempts recovery on the specified port.
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

                // Try to start server on the requested port only
                mcpServer = new McpBridgeServer();
                mcpServer.StartServer(port);

                // Update settings with the actual port used (same as requested)
                if (McpEditorSettings.GetServerPort() != port)
                {
                    McpEditorSettings.SetServerPort(port);
                }

                // Clear server-side reconnecting flag on successful restoration
                // NOTE: Do NOT clear UI display flag here - let it be cleared by timeout or client connection
                McpEditorSettings.SetIsReconnecting(false);

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
                    McpEditorSettings.ClearServerSession();
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
            bool wasRestored = McpEditorSettings.GetIsServerRunning();
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
        /// Restore server after compilation with frame delay.
        /// Currently kept as a helper; recovery logic is unified in StartRecoveryIfNeededAsync.
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
            _ = StartRecoveryIfNeededAsync(port, false, CancellationToken.None).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    VibeLogger.LogError("server_startup_restore_failed",
                        $"Failed to restore server: {task.Exception?.GetBaseException().Message}");
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>
        /// Retry server restore with frame delay on the same port.
        /// </summary>
        private static async Task RetryServerRestoreAsync(int port, int retryCount)
        {
            // Wait longer for port release before retry
            await EditorDelay.DelayFrame(5);
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
            bool isStillShowingUI = McpEditorSettings.GetShowReconnectingUI();
            if (isStillShowingUI)
            {
                McpEditorSettings.ClearReconnectingFlags();
            }
        }

        /// <summary>
        /// Clear reconnecting flags when client connects
        /// Called by UI or bridge server when client connection is detected
        /// </summary>
        public static void ClearReconnectingFlag()
        {
            bool wasReconnecting = McpEditorSettings.GetIsReconnecting();
            bool wasShowingUI = McpEditorSettings.GetShowReconnectingUI();

            if (wasReconnecting || wasShowingUI)
            {
                McpEditorSettings.ClearReconnectingFlags();
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

        public static bool IsStartupProtectionActive()
        {
            long nowTicks = DateTime.UtcNow.Ticks;
            return nowTicks < System.Threading.Volatile.Read(ref startupProtectionUntilTicks);
        }

        private static void ActivateStartupProtection(int milliseconds)
        {
            long untilTicks = DateTime.UtcNow.AddMilliseconds(milliseconds).Ticks;
            System.Threading.Volatile.Write(ref startupProtectionUntilTicks, untilTicks);
            VibeLogger.LogInfo("startup_protection_active", $"window={milliseconds}ms");
        }

        /// <summary>
        /// Centralized, coalesced recovery start.
        /// Attempts recovery on the specified port for up to 5 seconds without changing the port number.
        /// </summary>
        public static async Task StartRecoveryIfNeededAsync(int savedPort, bool isAfterCompile, CancellationToken cancellationToken)
        {
            VibeLogger.LogInfo("startup_request", $"port={savedPort}");

            if (IsStartupProtectionActive())
            {
                VibeLogger.LogInfo("server_start_ignored", "startup_protection_active");
                return;
            }

            await StartupSemaphore.WaitAsync(cancellationToken);
            try
            {
                // If any server is already running, ignore this request to prevent double-binding
                if (mcpServer != null && mcpServer.IsRunning)
                {
                    VibeLogger.LogInfo("server_start_ignored", $"already_running port={mcpServer.Port}");
                    return;
                }

                // Ensure previous instance is fully disposed before trying to bind a new one
                if (mcpServer != null)
                {
                    try
                    {
                        mcpServer.Dispose();
                        VibeLogger.LogInfo("server_disposed_before_bind", "disposed previous server instance");
                    }
                    catch (Exception ex)
                    {
                        VibeLogger.LogWarning("server_dispose_failed", ex.Message);
                    }
                    finally
                    {
                        mcpServer = null;
                    }
                }

                // Auto-update configuration files before starting server
                // This ensures paths are updated after package updates
                try
                {
                    McpConfigAutoUpdater.UpdateAllConfiguredEditors(savedPort);
                }
                catch (Exception ex)
                {
                    VibeLogger.LogWarning("config_auto_update_failed", $"Failed to auto-update configurations: {ex.Message}");
                    Debug.LogError($"[uLoopMCP] Failed to auto-update configurations: {ex.Message}");
                    // Continue with server startup even if config update fails
                }

                int chosenPort = savedPort;
                bool started = await TryBindWithWaitAsync(chosenPort, 5000, 250, cancellationToken);

                if (!started)
                {
                    // Ensure session reflects stopped state on failure
                    McpEditorSettings.ClearServerSession();
                    McpEditorSettings.ClearReconnectingFlags();
                    throw new InvalidOperationException($"Failed to bind port {savedPort} during server recovery.");
                }

                // Mark running and update settings
                McpEditorSettings.SetIsServerRunning(true);
                if (McpEditorSettings.GetServerPort() != chosenPort)
                {
                    // Defer aggressive external updates; only update internal setting here
                    McpEditorSettings.SetServerPort(chosenPort);
                }

                // Clear reconnection-related flags on successful recovery
                McpEditorSettings.ClearReconnectingFlags();
                McpEditorSettings.ClearPostCompileReconnectingUI();

                ActivateStartupProtection(5000);
            }
            finally
            {
                StartupSemaphore.Release();
            }
        }

        private static async Task<bool> TryBindWithWaitAsync(int port, int maxWaitMs, int stepMs, CancellationToken cancellationToken)
        {
            int remainingMs = maxWaitMs;
            while (true)
            {
                VibeLogger.LogInfo("binding_attempt", $"port={port}");
                McpBridgeServer server = null;
                try
                {
                    // Defensive: dispose any non-running stale instance before creating a new one
                    if (mcpServer != null && !mcpServer.IsRunning)
                    {
                        try
                        {
                            mcpServer.Dispose();
                            VibeLogger.LogInfo("server_disposed_before_bind", "disposed stale instance");
                        }
                        catch (Exception ex)
                        {
                            VibeLogger.LogWarning("server_dispose_failed", ex.Message);
                        }
                        finally
                        {
                            mcpServer = null;
                        }
                    }

                    server = new McpBridgeServer();
                    server.StartServer(port);
                    mcpServer = server;
                    VibeLogger.LogInfo("binding_success", $"port={port}");
                    return true;
                }
                catch (Exception ex)
                {
                    // Ensure partially created server is cleaned up on failure
                    try { server?.Dispose(); } catch { }
                    // Unwrap SocketException details if present
                    SocketException sockEx = ex as SocketException;
                    if (ex is InvalidOperationException && ex.InnerException is SocketException innerSock)
                    {
                        sockEx = innerSock;
                    }

                    if (sockEx != null)
                    {
                        VibeLogger.LogWarning("binding_failed", $"port={port} code={sockEx.SocketErrorCode} hresult={sockEx.HResult} native={sockEx.ErrorCode}");
                    }
                    else
                    {
                        VibeLogger.LogWarning("binding_failed", $"port={port} code=Unknown hresult={ex.HResult}");
                    }

                    if (remainingMs <= 0)
                    {
                        return false;
                    }

                    int delay = stepMs <= 0 ? remainingMs : Math.Min(stepMs, remainingMs);
                    await TimerDelay.Wait(delay, cancellationToken);
                    remainingMs -= delay;
                }
            }
        }
    }
}