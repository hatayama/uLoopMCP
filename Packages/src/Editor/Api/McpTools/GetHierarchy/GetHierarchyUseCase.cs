using System.Threading.Tasks;
using System.Threading;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Unity Hierarchy取得処理の時間的凝集を担当
    /// 処理順序：1. Hierarchy情報取得, 2. データ変換, 3. レスポンスサイズ判定とファイル出力
    /// 関連クラス: GetHierarchyTool, HierarchyService, HierarchySerializer, HierarchyResultExporter
    /// 設計書参照: DDDリファクタリング仕様 - UseCase Layer
    /// </summary>
    public class GetHierarchyUseCase : AbstractUseCase<GetHierarchySchema, GetHierarchyResponse>
    {
        /// <summary>
        /// Unity Hierarchy取得処理を実行する
        /// </summary>
        /// <param name="parameters">Hierarchy取得パラメータ</param>
        /// <param name="cancellationToken">キャンセレーション制御用トークン</param>
        /// <returns>Hierarchy取得結果</returns>
        public override Task<GetHierarchyResponse> ExecuteAsync(GetHierarchySchema parameters, CancellationToken cancellationToken)
        {
            // 1. Hierarchy情報取得
            HierarchyService service = new HierarchyService();
            HierarchySerializer serializer = new HierarchySerializer();
            
            HierarchyOptions options = new HierarchyOptions
            {
                IncludeInactive = parameters.IncludeInactive,
                MaxDepth = parameters.MaxDepth,
                RootPath = parameters.RootPath,
                IncludeComponents = parameters.IncludeComponents
            };
            
            cancellationToken.ThrowIfCancellationRequested();
            
            var nodes = service.GetHierarchyNodes(options);
            var context = service.GetCurrentContext();
            
            // 2. データ変換
            cancellationToken.ThrowIfCancellationRequested();
            
            var nestedNodes = serializer.ConvertToNestedStructure(nodes);
            
            // 3. レスポンスサイズ判定とファイル出力
            cancellationToken.ThrowIfCancellationRequested();
            
            GetHierarchyResponse nestedResponse = new GetHierarchyResponse(nestedNodes, context);
            
            // レスポンスサイズを計算
            var settings = new Newtonsoft.Json.JsonSerializerSettings
            {
                ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
                MaxDepth = McpServerConfig.DEFAULT_JSON_MAX_DEPTH
            };
            string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(nestedResponse, Newtonsoft.Json.Formatting.None, settings);
            int estimatedSizeBytes = System.Text.Encoding.UTF8.GetByteCount(jsonString);
            int estimatedSizeKB = estimatedSizeBytes / 1024;
            
            // サイズ制限を超える場合はファイルに保存
            if (estimatedSizeKB >= parameters.MaxResponseSizeKB)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                string filePath = HierarchyResultExporter.ExportHierarchyResults(nestedNodes, context);
                return Task.FromResult(new GetHierarchyResponse(filePath, "auto_threshold", context));
            }
            else
            {
                return Task.FromResult(nestedResponse);
            }
        }
    }
}