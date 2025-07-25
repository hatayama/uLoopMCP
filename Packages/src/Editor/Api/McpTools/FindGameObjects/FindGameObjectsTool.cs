using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Tool to find multiple GameObjects with advanced search criteria
    /// Related classes:
    /// - GameObjectFinderService: Core logic for finding GameObjects
    /// - FindGameObjectsSchema: Search parameters
    /// </summary>
    [McpTool(Description = "Find multiple GameObjects with advanced search criteria (component type, tag, layer, etc.)")]
    public class FindGameObjectsTool : AbstractUnityTool<FindGameObjectsSchema, FindGameObjectsResponse>
    {
        public override string ToolName => "find-game-objects";
        
        protected override async Task<FindGameObjectsResponse> ExecuteAsync(FindGameObjectsSchema parameters, CancellationToken cancellationToken)
        {
            // FindGameObjectsUseCaseインスタンスを生成して実行
            var useCase = new FindGameObjectsUseCase();
            return await useCase.ExecuteAsync(parameters, cancellationToken);
        }
    }
}