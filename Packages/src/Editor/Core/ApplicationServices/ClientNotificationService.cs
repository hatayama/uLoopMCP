using System.Threading.Tasks;
using Newtonsoft.Json;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// クライアント通知処理を担当するアプリケーションサービス
    /// 単一責任：MCPクライアントへの通知送信
    /// 関連クラス：McpBridgeServer, CustomToolManager
    /// 設計書参照：DDDリファクタリング仕様 - Application Services Layer
    /// </summary>
    public static class ClientNotificationService
    {
        /// <summary>
        /// ツール変更通知をクライアントに送信する
        /// </summary>
        public static void SendToolsChangedNotification()
        {
            McpBridgeServer currentServer = McpServerController.CurrentServer;
            if (currentServer == null)
            {
                return;
            }

            // MCP標準通知のみを送信
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
            currentServer.SendNotificationToClients(mcpNotificationJson);

            // ログ記録
            System.Diagnostics.StackTrace stackTrace = new System.Diagnostics.StackTrace(true);
            string callerInfo = stackTrace.GetFrame(1)?.GetMethod()?.Name ?? "Unknown";
            
            VibeLogger.LogInfo(
                "tools_list_changed_notification",
                "Sent tools changed notification to MCP clients",
                new { caller = callerInfo, timestamp = notificationParams.timestamp }
            );
        }

        /// <summary>
        /// サーバーが実行中かどうかを確認してツール変更通知を送信する
        /// </summary>
        public static void TriggerToolChangeNotification()
        {
            if (McpServerController.IsServerRunning)
            {
                SendToolsChangedNotification();
            }
        }

        /// <summary>
        /// コンパイル後にフレーム遅延を伴ってツール通知を送信する
        /// </summary>
        /// <returns>通知送信処理のTask</returns>
        public static async Task SendToolNotificationAfterCompilationAsync()
        {
            // Domain Reload後のUnityエディタ安定化のためのフレーム遅延
            await EditorDelay.DelayFrame(1);
            
            CustomToolManager.NotifyToolChanges();
        }

        /// <summary>
        /// 特定のクライアントに通知を送信する
        /// </summary>
        /// <param name="notification">送信する通知のJSONデータ</param>
        public static void SendNotificationToClients(string notification)
        {
            McpBridgeServer currentServer = McpServerController.CurrentServer;
            if (currentServer == null)
            {
                return;
            }

            currentServer.SendNotificationToClients(notification);
        }

        /// <summary>
        /// サーバー停止前のログを記録する
        /// </summary>
        /// <param name="correlationId">関連操作のトラッキング用ID</param>
        /// <param name="port">停止するサーバーのポート番号</param>
        public static void LogServerStoppingBeforeDomainReload(string correlationId, int port)
        {
            VibeLogger.LogInfo(
                "domain_reload_server_stopping",
                "Stopping MCP server before domain reload",
                new { port = port },
                correlationId
            );
        }

        /// <summary>
        /// サーバー停止完了のログを記録する
        /// </summary>
        /// <param name="correlationId">関連操作のトラッキング用ID</param>
        public static void LogServerStoppedAfterDomainReload(string correlationId)
        {
            VibeLogger.LogInfo(
                "domain_reload_server_stopped",
                "MCP server stopped successfully",
                new { tcp_port_released = true },
                correlationId
            );
        }

        /// <summary>
        /// サーバー停止エラーのログを記録する
        /// </summary>
        /// <param name="correlationId">関連操作のトラッキング用ID</param>
        /// <param name="ex">発生した例外</param>
        /// <param name="port">停止しようとしたポート番号</param>
        public static void LogServerShutdownError(string correlationId, System.Exception ex, int port)
        {
            VibeLogger.LogException(
                "domain_reload_server_shutdown_error",
                ex,
                new
                {
                    port = port,
                    server_was_running = true
                },
                correlationId,
                "Critical error during server shutdown before assembly reload. This may cause port conflicts on restart.",
                "Investigate server shutdown process and ensure proper TCP port release."
            );
        }
    }
}