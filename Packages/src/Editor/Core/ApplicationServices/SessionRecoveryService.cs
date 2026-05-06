using System.Threading.Tasks;
using System.Threading;
using UnityEditor;

namespace io.github.hatayama.UnityCliLoop
{
    /// <summary>
    /// Application service responsible for session recovery processing
    /// Single responsibility: Server session recovery and retry control
    /// Related classes: UnityCliLoopEditorSettings, IUnityCliLoopServerInstance
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
            bool wasRunning = UnityCliLoopEditorSettings.GetIsServerRunning();
            bool isAfterCompile = UnityCliLoopEditorSettings.GetIsAfterCompile();

            // If server is already running
            IUnityCliLoopServerInstance currentServer = UnityCliLoopServerController.CurrentServer;
            if (currentServer?.IsRunning == true)
            {
                // Server is running, clean up lock files
                CompilationLockService.DeleteLockFile();
                DomainReloadDetectionService.DeleteLockFile();
                // Why: only the startup generation that created serverstarting.lock knows whether
                // the canonical lock still belongs to it or has already been replaced by a newer
                // generation. Why not delete it here: a stale domain-reload recovery path can race
                // with an active startup/prewarm sequence and tear down another generation's lock.

                if (isAfterCompile)
                {
                    UnityCliLoopEditorSettings.ClearAfterCompileFlag();
                }
                return ValidationResult.Success();
            }

            // Clear after-compile flag
            if (isAfterCompile)
            {
                UnityCliLoopEditorSettings.ClearAfterCompileFlag();
            }

            // If server was running and is currently stopped, delegate to centralized controller logic
            if (wasRunning && (currentServer == null || !currentServer.IsRunning))
            {
                _ = UnityCliLoopServerController.StartRecoveryIfNeededAsync(isAfterCompile, CancellationToken.None).ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        VibeLogger.LogError("server_startup_restore_failed",
                            $"Failed to restore server: {task.Exception?.GetBaseException().Message}");
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Attempt server recovery with retry mechanism
        /// </summary>
        /// <param name="retryCount">Current retry count</param>
        /// <returns>Recovery process result</returns>
        public static ValidationResult TryRestoreServerWithRetry(int retryCount)
        {
            try
            {
                // Stop existing server instance
                IUnityCliLoopServerInstance currentServer = UnityCliLoopServerController.CurrentServer;
                if (currentServer != null)
                {
                    currentServer.Dispose();
                }

                IUnityCliLoopServerInstance newServer = new UnityCliLoopBridgeServer();
                newServer.StartServer();
                UnityCliLoopServerController.RegisterRecoveredServer(newServer);

                // Update session state
                UnityCliLoopEditorSettings.UpdateSettings(s => s with
                {
                    isReconnecting = false
                });

                return ValidationResult.Success();
            }
            catch (System.Exception ex)
            {
                if (retryCount < MAX_RETRIES)
                {
                    // Execute retry
                    _ = RetryServerRestoreAsync(retryCount).ContinueWith(task =>
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
                    UnityCliLoopEditorSettings.ClearServerSession();
                    return ValidationResult.Failure($"Failed to restore server after {MAX_RETRIES} retries: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Retry server recovery (asynchronous)
        /// </summary>
        /// <param name="retryCount">Current retry count</param>
        private static async Task RetryServerRestoreAsync(int retryCount)
        {
            await EditorDelay.DelayFrame(5);
            _ = UnityCliLoopServerController.StartRecoveryIfNeededAsync(false, CancellationToken.None).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    VibeLogger.LogError("server_startup_restore_failed",
                        $"Failed to restore server: {task.Exception?.GetBaseException().Message}");
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>
        /// Start reconnection UI display timeout
        /// </summary>
        /// <returns>Task for timeout processing</returns>
        public static async Task StartReconnectionUITimeoutAsync()
        {
            int timeoutFrames = UnityCliLoopConstants.RECONNECTION_TIMEOUT_SECONDS * 60;
            await EditorDelay.DelayFrame(timeoutFrames);

            bool isStillShowingUI = UnityCliLoopEditorSettings.GetShowReconnectingUI();
            if (isStillShowingUI)
            {
                UnityCliLoopEditorSettings.ClearReconnectingFlags();
            }
        }
    }
}
