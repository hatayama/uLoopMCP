using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// GetMenuItems tool handler - Discovers Unity MenuItems with filtering
    /// Retrieves MenuItem information from all loaded assemblies
    /// </summary>
    [McpTool(Description = "Retrieve Unity MenuItems with detailed metadata for programmatic execution. Unlike Unity Search menu provider, this provides implementation details (method names, assemblies, execution compatibility) needed for automation and debugging.")]
    public class GetMenuItemsTool : AbstractUnityTool<GetMenuItemsSchema, GetMenuItemsResponse>
    {
        public override string ToolName => "get-menu-items";

        protected override async Task<GetMenuItemsResponse> ExecuteAsync(GetMenuItemsSchema parameters, CancellationToken cancellationToken)
        {
            // GetMenuItemsUseCaseインスタンスを生成して実行
            GetMenuItemsUseCase useCase = new();
            return await useCase.ExecuteAsync(parameters, cancellationToken);
        }
    }
}