using System.Threading.Tasks;
using System.Threading;
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
            bool wasRunning = McpEditorSettings.GetIsServerRunning();
            int savedPort = McpEditorSettings.GetServerPort();
            bool isAfterCompile = McpEditorSettings.GetIsAfterCompile();

            // If server is already running
            McpBridgeServer currentServer = McpServerController.CurrentServer;
            UnityEngine.Debug.Log($"[SessionRecoveryService] RestoreServerStateIfNeeded: currentServer={currentServer != null}, IsRunning={currentServer?.IsRunning}, wasRunning={wasRunning}, autoStart={McpEditorSettings.GetAutoStartServer()}");
            if (currentServer?.IsRunning == true)
            {
                // Server is running, clean up lock files
                UnityEngine.Debug.Log("[SessionRecoveryService] Server already running, cleaning up lock files");
                CompilationLockService.DeleteLockFile();
                DomainReloadDetectionService.DeleteLockFile();
                ServerStartingLockService.DeleteLockFile();

                if (isAfterCompile)
                {
                    McpEditorSettings.ClearAfterCompileFlag();
                }
                return ValidationResult.Success();
            }

            // Clear after-compile flag
            if (isAfterCompile)
            {
                McpEditorSettings.ClearAfterCompileFlag();
            }

            // If server was running and is currently stopped, delegate to centralized controller logic
            if (wasRunning && (currentServer == null || !currentServer.IsRunning))
            {
                bool autoStartEnabled = McpEditorSettings.GetAutoStartServer();
                if (autoStartEnabled || isAfterCompile)
                {
                    _ = McpServerController.StartRecoveryIfNeededAsync(savedPort, isAfterCompile, CancellationToken.None).ContinueWith(task =>
                    {
                        if (task.IsFaulted)
                        {
                            VibeLogger.LogError("server_startup_restore_failed", 
                                $"Failed to restore server: {task.Exception?.GetBaseException().Message}");
                        }
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                }
                else
                {
                    McpEditorSettings.ClearServerSession();
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
                                McpEditorSettings.SetServerPort(availablePort);
                McpEditorSettings.SetIsReconnecting(false);

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
                    McpEditorSettings.ClearServerSession();
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
            _ = McpServerController.StartRecoveryIfNeededAsync(port, true, CancellationToken.None).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    VibeLogger.LogError("server_startup_restore_failed",
                        $"Failed to restore server: {task.Exception?.GetBaseException().Message}");
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>
        /// Restore server at startup (asynchronous)
        /// </summary>
        /// <param name="port">Port number to recover</param>
        private static async Task RestoreServerOnStartupAsync(int port)
        {
            await EditorDelay.DelayFrame(1);
            _ = McpServerController.StartRecoveryIfNeededAsync(port, false, CancellationToken.None).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    VibeLogger.LogError("server_startup_restore_failed",
                        $"Failed to restore server: {task.Exception?.GetBaseException().Message}");
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>
        /// Retry server recovery (asynchronous)
        /// </summary>
        /// <param name="port">Port number to recover</param>
        /// <param name="retryCount">Current retry count</param>
        private static async Task RetryServerRestoreAsync(int port, int retryCount)
        {
            await EditorDelay.DelayFrame(5);
            _ = McpServerController.StartRecoveryIfNeededAsync(port, false, CancellationToken.None).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    VibeLogger.LogError("server_startup_restore_failed",
                        $"Failed to restore server: {task.Exception?.GetBaseException().Message}");
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>
        /// Clear reconnection flag
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
        /// Start reconnection UI display timeout
        /// </summary>
        /// <returns>Task for timeout processing</returns>
        public static async Task StartReconnectionUITimeoutAsync()
        {
            int timeoutFrames = McpConstants.RECONNECTION_TIMEOUT_SECONDS * 60;
            await EditorDelay.DelayFrame(timeoutFrames);

                        bool isStillShowingUI = McpEditorSettings.GetShowReconnectingUI();
            if (isStillShowingUI)
            {
                McpEditorSettings.ClearReconnectingFlags();
            }
        }
    }
}