using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// GameObject検索処理の時間的凝集を担当
    /// 処理順序：1. 検索条件の検証, 2. GameObject検索実行, 3. 結果の変換と整形
    /// 関連クラス: FindGameObjectsTool, GameObjectFinderService, ComponentSerializer
    /// 設計書参照: DDDリファクタリング仕様 - UseCase Layer
    /// </summary>
    public class FindGameObjectsUseCase : AbstractUseCase<FindGameObjectsSchema, FindGameObjectsResponse>
    {
        private readonly GameObjectFinderService _finderService;
        private readonly ComponentSerializer _componentSerializer;

        public FindGameObjectsUseCase() : this(new GameObjectFinderService(), new ComponentSerializer())
        {
        }

        public FindGameObjectsUseCase(GameObjectFinderService finderService, ComponentSerializer componentSerializer)
        {
            _finderService = finderService ?? throw new System.ArgumentNullException(nameof(finderService));
            _componentSerializer = componentSerializer ?? throw new System.ArgumentNullException(nameof(componentSerializer));
        }
        /// <summary>
        /// GameObject検索処理を実行する
        /// </summary>
        /// <param name="parameters">検索パラメータ</param>
        /// <param name="cancellationToken">キャンセレーション制御用トークン</param>
        /// <returns>検索結果</returns>
        public override Task<FindGameObjectsResponse> ExecuteAsync(FindGameObjectsSchema parameters, CancellationToken cancellationToken)
        {
            // 1. 検索条件の検証
            if (string.IsNullOrEmpty(parameters.NamePattern) &&
                (parameters.RequiredComponents == null || parameters.RequiredComponents.Length == 0) &&
                string.IsNullOrEmpty(parameters.Tag) &&
                !parameters.Layer.HasValue)
            {
                return Task.FromResult(new FindGameObjectsResponse
                {
                    results = new FindGameObjectResult[0],
                    totalFound = 0,
                    errorMessage = "At least one search criterion must be provided"
                });
            }
            
            // 2. GameObject検索実行
            cancellationToken.ThrowIfCancellationRequested();
            
            GameObjectSearchOptions options = new GameObjectSearchOptions
            {
                NamePattern = parameters.NamePattern,
                SearchMode = parameters.SearchMode,
                RequiredComponents = parameters.RequiredComponents,
                Tag = parameters.Tag,
                Layer = parameters.Layer,
                IncludeInactive = parameters.IncludeInactive,
                MaxResults = parameters.MaxResults
            };
            
            GameObjectDetails[] foundObjects = _finderService.FindGameObjectsAdvanced(options);
            
            // 3. 結果の変換と整形
            cancellationToken.ThrowIfCancellationRequested();
            
            List<FindGameObjectResult> results = new List<FindGameObjectResult>();
            
            foreach (GameObjectDetails details in foundObjects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                FindGameObjectResult result = new FindGameObjectResult
                {
                    name = details.Name,
                    path = details.Path,
                    isActive = details.IsActive,
                    tag = details.GameObject.tag,
                    layer = details.GameObject.layer,
                    components = _componentSerializer.SerializeComponents(details.GameObject)
                };
                
                results.Add(result);
            }
            
            FindGameObjectsResponse response = new FindGameObjectsResponse
            {
                results = results.ToArray(),
                totalFound = results.Count
            };
            
            return Task.FromResult(response);
        }
    }
}