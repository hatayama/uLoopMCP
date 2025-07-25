using System.Threading.Tasks;
using System.Threading;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Unity Search実行処理の時間的凝集を担当
    /// 処理順序：1. 古いファイルのクリーンアップ, 2. 検索実行, 3. 結果の処理
    /// 関連クラス: UnitySearchTool, UnitySearchService, SearchResultExporter
    /// 設計書参照: DDDリファクタリング仕様 - UseCase Layer
    /// </summary>
    public class UnitySearchUseCase : AbstractUseCase<UnitySearchSchema, UnitySearchResponse>
    {
        /// <summary>
        /// Unity Search処理を実行する
        /// </summary>
        /// <param name="parameters">検索パラメータ</param>
        /// <param name="cancellationToken">キャンセレーション制御用トークン</param>
        /// <returns>検索結果</returns>
        public override async Task<UnitySearchResponse> ExecuteAsync(UnitySearchSchema parameters, CancellationToken cancellationToken)
        {
            // 0. デフォルト値適用（内部処理）
            ApplyDefaultValues(parameters);

            // 1. 古いファイルのクリーンアップ
            cancellationToken.ThrowIfCancellationRequested();
            UnitySearchService.CleanupOldExports();

            // 2. 検索実行
            cancellationToken.ThrowIfCancellationRequested();
            UnitySearchResponse response = await UnitySearchService.ExecuteSearchAsync(parameters);

            // 3. 結果の処理（ログ記録等は既にUnitySearchServiceで処理済み）
            return response;
        }

        /// <summary>
        /// スキーマにデフォルト値を適用する（内部処理）
        /// </summary>
        /// <param name="schema">スキーマ</param>
        private void ApplyDefaultValues(UnitySearchSchema schema)
        {
            // 配列がnullでないことを保証
            schema.Providers ??= new string[0];
            schema.FileExtensions ??= new string[0];
            schema.AssetTypes ??= new string[0];

            // 合理的なデフォルト値を適用
            if (schema.MaxResults <= 0)
                schema.MaxResults = 50;

            if (schema.AutoSaveThreshold < 0)
                schema.AutoSaveThreshold = 100;

            // 検索クエリがnullでないことを保証
            schema.SearchQuery ??= "";
            schema.PathFilter ??= "";
        }
    }
}