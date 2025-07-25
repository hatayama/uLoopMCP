using System.Threading.Tasks;
using System.Threading;
using UnityEditor.TestTools.TestRunner.Api;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// テスト実行処理の時間的凝集を担当
    /// 処理順序：1. テストフィルターの作成, 2. テスト実行, 3. 結果の処理
    /// 関連クラス: RunTestsTool, TestFilterCreationService, TestExecutionService
    /// 設計書参照: DDDリファクタリング仕様 - UseCase Layer
    /// </summary>
    public class RunTestsUseCase : AbstractUseCase<RunTestsSchema, RunTestsResponse>
    {
        /// <summary>
        /// テスト実行処理を実行する
        /// </summary>
        /// <param name="parameters">テスト実行パラメータ</param>
        /// <param name="cancellationToken">キャンセレーション制御用トークン</param>
        /// <returns>テスト実行結果</returns>
        public override async Task<RunTestsResponse> ExecuteAsync(RunTestsSchema parameters, CancellationToken cancellationToken)
        {
            // 1. テストフィルターの作成
            TestExecutionFilter filter = null;
            if (parameters.FilterType != TestFilterType.all)
            {
                var filterService = new TestFilterCreationService();
                filter = filterService.CreateFilter(parameters.FilterType, parameters.FilterValue);
            }
            
            // 2. テスト実行
            cancellationToken.ThrowIfCancellationRequested();
            var executionService = new TestExecutionService();
            SerializableTestResult result;
            
            if (parameters.TestMode == TestMode.PlayMode)
            {
                result = await executionService.ExecutePlayModeTestAsync(filter, parameters.SaveXml);
            }
            else
            {
                result = await executionService.ExecuteEditModeTestAsync(filter, parameters.SaveXml);
            }
            
            // 3. レスポンス作成
            return new RunTestsResponse(
                success: result.success,
                message: result.message,
                completedAt: result.completedAt,
                testCount: result.testCount,
                passedCount: result.passedCount,
                failedCount: result.failedCount,
                skippedCount: result.skippedCount,
                xmlPath: result.xmlPath
            );
        }
    }
}