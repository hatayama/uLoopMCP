using System.Threading.Tasks;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Application service responsible for session recovery processing
    /// Single responsibility: Server session recovery and retry control
    /// Related classes: McpSessionManager, McpBridgeServer, NetworkUtility
    /// Design reference: @Packages/docs/ARCHITECTURE_Unity.md - Application Service Layer (Single Function Implementation)
    /// </summary>
    public static class SessionRecoveryService
    {
        private const int MAX_RETRIES = 3;

        /// <summary>
        /// Restore server state if needed
        /// </summary>
        /// <returns>Recovery process result</returns>
        public static ValidationResult RestoreServerStateIfNeeded()
        {
            McpSessionManager sessionManager = McpSessionManager.instance;
            bool wasRunning = sessionManager.IsServerRunning;
            int savedPort = sessionManager.ServerPort;
            bool isAfterCompile = sessionManager.IsAfterCompile;

            // If server is already running
            McpBridgeServer currentServer = McpServerController.CurrentServer;
            if (currentServer?.IsRunning == true)
            {
                if (isAfterCompile)
                {
                    sessionManager.ClearAfterCompileFlag();
                }
                return ValidationResult.Success();
            }

            // Clear after-compile flag
            if (isAfterCompile)
            {
                sessionManager.ClearAfterCompileFlag();
            }

            // If server was running and is currently stopped
            if (wasRunning && (currentServer == null || !currentServer.IsRunning))
            {
                if (isAfterCompile)
                {
                    // Restart immediately after compilation
                    _ = RestoreServerAfterCompileAsync(savedPort).ContinueWith(task =>
                    {
                        if (task.IsFaulted)
                        {
                            VibeLogger.LogError("server_restore_failed", 
                                $"Failed to restore server after compile: {task.Exception?.GetBaseException().Message}");
                        }
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                }
                else
                {
                    // Check auto-start settings for non-compile scenarios
                    bool autoStartEnabled = McpEditorSettings.GetAutoStartServer();
                    if (autoStartEnabled)
                    {
                        _ = RestoreServerOnStartupAsync(savedPort).ContinueWith(task =>
                        {
                            if (task.IsFaulted)
                            {
                                VibeLogger.LogError("server_startup_restore_failed", 
                                    $"Failed to restore server on startup: {task.Exception?.GetBaseException().Message}");
                            }
                        }, TaskScheduler.FromCurrentSynchronizationContext());
                    }
                    else
                    {
                        sessionManager.ClearServerSession();
                    }
                }
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Attempt server recovery with retry mechanism
        /// </summary>
        /// <param name="port">Port number to recover</param>
        /// <param name="retryCount">Current retry count</param>
        /// <returns>Recovery process result</returns>
        public static ValidationResult TryRestoreServerWithRetry(int port, int retryCount)
        {
            try
            {
                // Stop existing server instance
                McpBridgeServer currentServer = McpServerController.CurrentServer;
                if (currentServer != null)
                {
                    currentServer.Dispose();
                }

                // Find available port
                int availablePort = NetworkUtility.FindAvailablePort(port);

                // Start new server
                var newServer = new McpBridgeServer();
                newServer.StartServer(availablePort);

                // Update session state
                McpSessionManager sessionManager = McpSessionManager.instance;
                sessionManager.ServerPort = availablePort;
                sessionManager.IsReconnecting = false;

                return ValidationResult.Success();
            }
            catch (System.Exception ex)
            {
                if (retryCount < MAX_RETRIES)
                {
                    // Execute retry
                    _ = RetryServerRestoreAsync(port, retryCount).ContinueWith(task =>
                    {
                        if (task.IsFaulted)
                        {
                            VibeLogger.LogError("server_restore_retry_failed", 
                                $"Failed to retry server restore (attempt {retryCount}): {task.Exception?.GetBaseException().Message}");
                        }
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                    return ValidationResult.Success();
                }
                else
                {
                    // Clear session when maximum retry count is reached
                    McpSessionManager.instance.ClearServerSession();
                    return ValidationResult.Failure($"Failed to restore server after {MAX_RETRIES} retries: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Restore server after compilation (asynchronous)
        /// </summary>
        /// <param name="port">Port number to recover</param>
        private static async Task RestoreServerAfterCompileAsync(int port)
        {
            await EditorDelay.DelayFrame(1);
            TryRestoreServerWithRetry(port, 0);
        }

        /// <summary>
        /// Restore server at startup (asynchronous)
        /// </summary>
        /// <param name="port">Port number to recover</param>
        private static async Task RestoreServerOnStartupAsync(int port)
        {
            await EditorDelay.DelayFrame(1);
            TryRestoreServerWithRetry(port, 0);
        }

        /// <summary>
        /// Retry server recovery (asynchronous)
        /// </summary>
        /// <param name="port">Port number to recover</param>
        /// <param name="retryCount">Current retry count</param>
        private static async Task RetryServerRestoreAsync(int port, int retryCount)
        {
            await EditorDelay.DelayFrame(5);
            TryRestoreServerWithRetry(port, retryCount + 1);
        }

        /// <summary>
        /// Clear reconnection flag
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
        /// Start reconnection UI display timeout
        /// </summary>
        /// <returns>Task for timeout processing</returns>
        public static async Task StartReconnectionUITimeoutAsync()
        {
            int timeoutFrames = McpConstants.RECONNECTION_TIMEOUT_SECONDS * 60;
            await EditorDelay.DelayFrame(timeoutFrames);

            McpSessionManager sessionManager = McpSessionManager.instance;
            bool isStillShowingUI = sessionManager.ShowReconnectingUI;
            if (isStillShowingUI)
            {
                sessionManager.ClearReconnectingFlags();
            }
        }
    }
}