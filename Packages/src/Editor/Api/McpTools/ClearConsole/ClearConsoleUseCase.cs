using System.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Console クリア処理の時間的凝集を担当
    /// 処理順序：1. 現在のログ数取得, 2. コンソールクリア実行, 3. 確認メッセージ追加, 4. 結果作成
    /// 関連クラス: ClearConsoleTool, ConsoleUtility
    /// 設計書参照: DDDリファクタリング仕様 - UseCase Layer
    /// </summary>
    public class ClearConsoleUseCase : AbstractUseCase<ClearConsoleSchema, ClearConsoleResponse>
    {
        /// <summary>
        /// Console クリア処理を実行する
        /// </summary>
        /// <param name="parameters">クリア設定パラメータ</param>
        /// <param name="cancellationToken">キャンセレーション制御用トークン</param>
        /// <returns>クリア実行結果</returns>
        public override Task<ClearConsoleResponse> ExecuteAsync(ClearConsoleSchema parameters, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (parameters == null)
            {
                throw new System.ArgumentNullException(nameof(parameters));
            }

            try
            {
                // 1. 現在のログ数取得
                ConsoleUtility.GetConsoleLogCounts(out int errorCount, out int warningCount, out int logCount);
                int totalLogCount = errorCount + warningCount + logCount;
                
                ClearedLogCounts clearedCounts = new ClearedLogCounts(errorCount, warningCount, logCount);

                cancellationToken.ThrowIfCancellationRequested();

                // 2. コンソールクリア実行
                ConsoleUtility.ClearConsole();

                // 3. 確認メッセージ追加
                if (parameters.AddConfirmationMessage)
                {
                    Debug.Log("=== Console cleared via MCP tool ===");
                }

                // 4. 結果作成
                string message = totalLogCount > 0 
                    ? $"Successfully cleared {totalLogCount} console logs (Errors: {errorCount}, Warnings: {warningCount}, Logs: {logCount})"
                    : "Console was already empty";

                ClearConsoleResponse response = new ClearConsoleResponse(
                    success: true,
                    clearedLogCount: totalLogCount,
                    clearedCounts: clearedCounts,
                    message: message
                );

                return Task.FromResult(response);
            }
            catch (System.OperationCanceledException)
            {
                throw; // Re-throw cancellation exceptions
            }
            catch (System.Exception ex)
            {
                // Handle exceptions from console operations
                ClearConsoleResponse errorResponse = new ClearConsoleResponse(
                    success: false,
                    clearedLogCount: 0,
                    clearedCounts: new ClearedLogCounts(0, 0, 0),
                    message: $"Failed to clear console: {ex.Message}"
                );
                return Task.FromResult(errorResponse);
            }
        }
    }
}