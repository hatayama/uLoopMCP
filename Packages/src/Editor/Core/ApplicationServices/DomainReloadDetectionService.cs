using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Domain Reload検出と状態管理を担当するアプリケーションサービス
    /// 単一責任：Domain Reloadのライフサイクル管理
    /// 関連クラス：McpSessionManager, McpServerController
    /// 設計書参照：DDDリファクタリング仕様 - Application Services Layer
    /// </summary>
    public static class DomainReloadDetectionService
    {
        /// <summary>
        /// Domain Reload開始処理を実行する
        /// </summary>
        /// <param name="correlationId">関連操作のトラッキング用ID</param>
        /// <param name="serverIsRunning">サーバーが実行中かどうか</param>
        /// <param name="serverPort">サーバーのポート番号</param>
        public static void StartDomainReload(string correlationId, bool serverIsRunning, int? serverPort)
        {
            McpSessionManager sessionManager = McpSessionManager.instance;
            
            // Domain Reload進行フラグを設定
            sessionManager.IsDomainReloadInProgress = true;

            // サーバーが実行中の場合、セッション状態を保存
            if (serverIsRunning && serverPort.HasValue)
            {
                sessionManager.IsServerRunning = true;
                sessionManager.ServerPort = serverPort.Value;
                sessionManager.IsAfterCompile = true;
                sessionManager.IsReconnecting = true;
                sessionManager.ShowReconnectingUI = true;
                sessionManager.ShowPostCompileReconnectingUI = true;
            }

            // ログ記録
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
        /// Domain Reload完了処理を実行する
        /// </summary>
        /// <param name="correlationId">関連操作のトラッキング用ID</param>
        public static void CompleteDomainReload(string correlationId)
        {
            McpSessionManager sessionManager = McpSessionManager.instance;
            
            // Domain Reload完了フラグをクリア
            sessionManager.ClearDomainReloadFlag();

            // ログ記録
            VibeLogger.LogInfo(
                "domain_reload_complete",
                "Domain reload completed - starting server recovery process",
                new { session_server_port = sessionManager.ServerPort },
                correlationId
            );
        }

        /// <summary>
        /// 現在Domain Reload中かどうかを確認する
        /// </summary>
        /// <returns>Domain Reload中の場合true</returns>
        public static bool IsDomainReloadInProgress()
        {
            return McpSessionManager.instance.IsDomainReloadInProgress;
        }

        /// <summary>
        /// 再接続UI表示が必要かどうかを確認する
        /// </summary>
        /// <returns>再接続UI表示が必要な場合true</returns>
        public static bool ShouldShowReconnectingUI()
        {
            return McpSessionManager.instance.ShowReconnectingUI;
        }

        /// <summary>
        /// コンパイル後の状態かどうかを確認する
        /// </summary>
        /// <returns>コンパイル後の場合true</returns>
        public static bool IsAfterCompile()
        {
            return McpSessionManager.instance.IsAfterCompile;
        }
    }
}