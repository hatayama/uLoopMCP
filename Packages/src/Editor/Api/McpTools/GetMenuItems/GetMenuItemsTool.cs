using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// GetMenuItems tool handler - Discovers Unity MenuItems with filtering
    /// Retrieves MenuItem information from all loaded assemblies
    /// 
    /// Design Reference: @Packages/docs/ARCHITECTURE_Unity.md - UseCase + Tool Pattern (DDD Integration)
    /// 
    /// This Tool class delegates to GetMenuItemsUseCase for business logic execution,
    /// following the UseCase + Tool pattern for separation of concerns.
    /// 
    /// Related classes:
    /// - GetMenuItemsUseCase: Business logic and orchestration
    /// - GetMenuItemsSchema: Type-safe parameter schema
    /// - GetMenuItemsResponse: Type-safe response structure
    /// </summary>
    [McpTool(Description = "Retrieve Unity MenuItems with detailed metadata for programmatic execution. Unlike Unity Search menu provider, this provides implementation details (method names, assemblies, execution compatibility) needed for automation and debugging.")]
    public class GetMenuItemsTool : AbstractUnityTool<GetMenuItemsSchema, GetMenuItemsResponse>
    {
        public override string ToolName => "get-menu-items";

        protected override async Task<GetMenuItemsResponse> ExecuteAsync(GetMenuItemsSchema parameters, CancellationToken cancellationToken)
        {
            // Create and execute GetMenuItemsUseCase instance
            GetMenuItemsUseCase useCase = new();
            return await useCase.ExecuteAsync(parameters, cancellationToken);
        }
    }
}