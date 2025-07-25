using System.Threading.Tasks;
using System.Threading;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// サーバー終了処理の時間的凝集を担当するUseCase
    /// 処理順序：1. サーバー停止, 2. セッション状態クリア, 3. リソース解放
    /// 関連クラス：McpServerStartupService, McpSessionManager
    /// 設計書参照：DDDリファクタリング仕様 - UseCase Layer
    /// </summary>
    public class McpServerShutdownUseCase : AbstractUseCase<ServerShutdownSchema, ServerShutdownResponse>
    {
        /// <summary>
        /// サーバー終了処理を実行する
        /// </summary>
        /// <param name="parameters">終了パラメータ</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        /// <returns>終了結果</returns>
        public override async Task<ServerShutdownResponse> ExecuteAsync(ServerShutdownSchema parameters, CancellationToken cancellationToken)
        {
            var response = new ServerShutdownResponse();
            var startTime = System.DateTime.UtcNow;

            try
            {
                // 1. 現在のサーバーインスタンスを取得
                McpBridgeServer currentServer = McpServerController.CurrentServer;

                // 2. サーバー停止処理 - McpServerStartupService
                var startupService = new McpServerStartupService();
                var stopResult = startupService.StopServer(currentServer);
                if (!stopResult.Success)
                {
                    response.Success = false;
                    response.Message = stopResult.ErrorMessage;
                    return response;
                }

                // 3. セッション状態クリア
                var sessionUpdateResult = startupService.UpdateSessionState(false, 0);
                if (!sessionUpdateResult.Success)
                {
                    response.Success = false;
                    response.Message = sessionUpdateResult.ErrorMessage;
                    return response;  
                }

                // 4. SessionManagerでセッションクリア
                McpSessionManager sessionManager = McpSessionManager.instance;
                sessionManager.ClearServerSession();

                // 成功レスポンス
                response.Success = true;
                response.Message = "Server shutdown completed successfully";

                return response;
            }
            catch (System.Exception ex)
            {
                response.Success = false;
                response.Message = $"Server shutdown failed: {ex.Message}";
                return response;
            }
            finally
            {
                response.SetTimingInfo(startTime, System.DateTime.UtcNow);
            }
        }
    }
}