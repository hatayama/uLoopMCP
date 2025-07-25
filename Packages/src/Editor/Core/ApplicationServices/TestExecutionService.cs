using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// テスト実行サービス
    /// 単一機能：Unity Test Runnerを使用したテスト実行を行う
    /// 関連クラス: PlayModeTestExecuter, RunTestsUseCase, RunTestsTool
    /// 設計書参照: DDDリファクタリング仕様 - Application Service Layer
    /// </summary>
    public class TestExecutionService
    {
        /// <summary>
        /// PlayModeでテストを実行する
        /// </summary>
        /// <param name="filter">テスト実行フィルター</param>
        /// <param name="saveXml">XML保存フラグ</param>
        /// <returns>テスト実行結果</returns>
        public async Task<SerializableTestResult> ExecutePlayModeTestAsync(TestExecutionFilter filter, bool saveXml)
        {
            return await PlayModeTestExecuter.ExecutePlayModeTest(filter, saveXml);
        }

        /// <summary>
        /// EditModeでテストを実行する
        /// </summary>
        /// <param name="filter">テスト実行フィルター</param>
        /// <param name="saveXml">XML保存フラグ</param>
        /// <returns>テスト実行結果</returns>
        public async Task<SerializableTestResult> ExecuteEditModeTestAsync(TestExecutionFilter filter, bool saveXml)
        {
            return await PlayModeTestExecuter.ExecuteEditModeTest(filter, saveXml);
        }
    }
}