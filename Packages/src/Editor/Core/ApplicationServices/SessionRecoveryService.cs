using System.Threading.Tasks;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// セッション復旧処理を担当するアプリケーションサービス
    /// 単一責任：サーバーセッションの復旧とリトライ制御
    /// 関連クラス：McpSessionManager, McpBridgeServer, NetworkUtility
    /// 設計書参照：DDDリファクタリング仕様 - Application Services Layer
    /// </summary>
    public static class SessionRecoveryService
    {
        private const int MAX_RETRIES = 3;

        /// <summary>
        /// 必要に応じてサーバー状態を復旧する
        /// </summary>
        /// <returns>復旧処理結果</returns>
        public static ValidationResult RestoreServerStateIfNeeded()
        {
            McpSessionManager sessionManager = McpSessionManager.instance;
            bool wasRunning = sessionManager.IsServerRunning;
            int savedPort = sessionManager.ServerPort;
            bool isAfterCompile = sessionManager.IsAfterCompile;

            // 既にサーバーが実行中の場合
            McpBridgeServer currentServer = McpServerController.CurrentServer;
            if (currentServer?.IsRunning == true)
            {
                if (isAfterCompile)
                {
                    sessionManager.ClearAfterCompileFlag();
                }
                return ValidationResult.Success();
            }

            // コンパイル後フラグをクリア
            if (isAfterCompile)
            {
                sessionManager.ClearAfterCompileFlag();
            }

            // サーバーが実行されていて、現在停止している場合
            if (wasRunning && (currentServer == null || !currentServer.IsRunning))
            {
                if (isAfterCompile)
                {
                    // コンパイル後は即座に再起動
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
                    // 非コンパイル時は自動起動設定を確認
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
        /// リトライ付きでサーバー復旧を試行する
        /// </summary>
        /// <param name="port">復旧するポート番号</param>
        /// <param name="retryCount">現在のリトライ回数</param>
        /// <returns>復旧処理結果</returns>
        public static ValidationResult TryRestoreServerWithRetry(int port, int retryCount)
        {
            try
            {
                // 既存のサーバーインスタンスを停止
                McpBridgeServer currentServer = McpServerController.CurrentServer;
                if (currentServer != null)
                {
                    currentServer.Dispose();
                }

                // 利用可能なポートを見つける
                int availablePort = NetworkUtility.FindAvailablePort(port);

                // 新しいサーバーを起動
                var newServer = new McpBridgeServer();
                newServer.StartServer(availablePort);

                // セッション状態を更新
                McpSessionManager sessionManager = McpSessionManager.instance;
                sessionManager.ServerPort = availablePort;
                sessionManager.IsReconnecting = false;

                return ValidationResult.Success();
            }
            catch (System.Exception ex)
            {
                if (retryCount < MAX_RETRIES)
                {
                    // リトライを実行
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
                    // 最大リトライ回数に達した場合、セッションをクリア
                    McpSessionManager.instance.ClearServerSession();
                    return ValidationResult.Failure($"Failed to restore server after {MAX_RETRIES} retries: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// コンパイル後にサーバーを復旧する（非同期）
        /// </summary>
        /// <param name="port">復旧するポート番号</param>
        private static async Task RestoreServerAfterCompileAsync(int port)
        {
            await EditorDelay.DelayFrame(1);
            TryRestoreServerWithRetry(port, 0);
        }

        /// <summary>
        /// 起動時にサーバーを復旧する（非同期）
        /// </summary>
        /// <param name="port">復旧するポート番号</param>
        private static async Task RestoreServerOnStartupAsync(int port)
        {
            await EditorDelay.DelayFrame(1);
            TryRestoreServerWithRetry(port, 0);
        }

        /// <summary>
        /// サーバー復旧をリトライする（非同期）
        /// </summary>
        /// <param name="port">復旧するポート番号</param>
        /// <param name="retryCount">現在のリトライ回数</param>
        private static async Task RetryServerRestoreAsync(int port, int retryCount)
        {
            await EditorDelay.DelayFrame(5);
            TryRestoreServerWithRetry(port, retryCount + 1);
        }

        /// <summary>
        /// 再接続フラグをクリアする
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
        /// 再接続UI表示タイムアウトを開始する
        /// </summary>
        /// <returns>タイムアウト処理のTask</returns>
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