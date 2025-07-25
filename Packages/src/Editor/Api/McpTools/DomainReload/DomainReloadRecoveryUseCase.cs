using System.Threading.Tasks;
using System.Threading;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Domain Reload復旧処理の時間的凝集を担当するUseCase
    /// 処理順序：1. 事前停止処理, 2. 復旧処理, 3. 通知処理
    /// 関連クラス：DomainReloadDetectionService, SessionRecoveryService, ClientNotificationService
    /// 設計書参照：DDDリファクタリング仕様 - UseCase Layer
    /// </summary>
    public class DomainReloadRecoveryUseCase
    {
        /// <summary>
        /// Domain Reload開始前の処理を実行する
        /// </summary>
        /// <param name="currentServer">現在のサーバーインスタンス</param>
        /// <returns>処理結果</returns>
        public ServiceResult<string> ExecuteBeforeDomainReload(McpBridgeServer currentServer)
        {
            // 1. 関連操作のトラッキング用IDを生成
            string correlationId = VibeLogger.GenerateCorrelationId();

            // 2. サーバー状態を確認
            bool serverRunning = currentServer?.IsRunning ?? false;
            int? serverPort = currentServer?.Port;

            // 3. Domain Reload開始を検出・記録
            DomainReloadDetectionService.StartDomainReload(correlationId, serverRunning, serverPort);

            // 4. サーバーが実行中の場合、停止処理を実行
            if (currentServer?.IsRunning == true)
            {
                int portToSave = currentServer.Port;
                
                try
                {
                    // 4.1. クライアントに停止通知
                    ClientNotificationService.LogServerStoppingBeforeDomainReload(correlationId, portToSave);

                    // 4.2. サーバーを停止
                    currentServer.Dispose();

                    // 4.3. クライアントに停止完了通知
                    ClientNotificationService.LogServerStoppedAfterDomainReload(correlationId);

                    return ServiceResult<string>.SuccessResult(correlationId);
                }
                catch (System.Exception ex)
                {
                    // 4.4. エラー通知
                    ClientNotificationService.LogServerShutdownError(correlationId, ex, portToSave);

                    // サーバー停止失敗は重大なエラー（ポート競合の原因となる）
                    throw new System.InvalidOperationException(
                        $"Failed to properly shutdown MCP server before assembly reload. This may cause port conflicts on restart.", ex);
                }
            }

            return ServiceResult<string>.SuccessResult(correlationId);
        }

        /// <summary>
        /// Domain Reload完了後の復旧処理を実行する
        /// </summary>
        /// <returns>処理結果</returns>
        public async Task<ServiceResult<string>> ExecuteAfterDomainReloadAsync()
        {
            // 1. 関連操作のトラッキング用IDを生成
            string correlationId = VibeLogger.GenerateCorrelationId();

            // 2. Domain Reload完了を記録
            DomainReloadDetectionService.CompleteDomainReload(correlationId);

            // 3. 再接続UI表示が必要な場合、タイムアウトを開始
            if (DomainReloadDetectionService.ShouldShowReconnectingUI())
            {
                SessionRecoveryService.StartReconnectionUITimeoutAsync().Forget();
            }

            // 4. MCP設定を現在のデバッグ状態に更新
            McpDebugStateUpdater.UpdateAllConfigurationsForDebugState();

            // 5. サーバー状態を復旧
            ValidationResult restoreResult = SessionRecoveryService.RestoreServerStateIfNeeded();
            if (!restoreResult.IsValid)
            {
                return ServiceResult<string>.FailureResult($"Server restoration failed: {restoreResult.ErrorMessage}");
            }

            // 6. 保留中のコンパイルリクエストを処理（現在は無効化済み）
            ProcessPendingCompileRequests();

            // 7. サーバーが実行中の場合、ツール変更通知を送信
            if (McpServerController.IsServerRunning)
            {
                await ClientNotificationService.SendToolNotificationAfterCompilationAsync();
            }

            return ServiceResult<string>.SuccessResult(correlationId);
        }

        /// <summary>
        /// 保留中のコンパイルリクエストを処理する
        /// 注意：現在は機能フラグにより無効化されている（メインスレッドエラー回避のため）
        /// </summary>
        private void ProcessPendingCompileRequests()
        {
            // 機能フラグによる制御（現在は無効化）
            bool enablePendingCompileProcessing = false; // TODO: 設定ファイルまたはエディタ設定から読み込み
            
            if (enablePendingCompileProcessing)
            {
                // メインスレッド問題解決後に有効化予定
                // CompileSessionState.StartForcedRecompile();
                VibeLogger.LogInfo(
                    "pending_compile_processing", 
                    "Processing pending compile requests", 
                    correlationId: VibeLogger.GenerateCorrelationId()
                );
            }
            else
            {
                VibeLogger.LogInfo(
                    "pending_compile_processing_disabled", 
                    "Pending compile request processing is disabled via feature flag", 
                    correlationId: VibeLogger.GenerateCorrelationId()
                );
            }
        }
    }
}