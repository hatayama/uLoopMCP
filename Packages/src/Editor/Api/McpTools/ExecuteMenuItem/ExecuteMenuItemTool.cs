using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// ExecuteMenuItem tool handler - Executes Unity MenuItems by path
    /// Supports both EditorApplication.ExecuteMenuItem and reflection-based execution
    /// 
    /// Design Reference: @Packages/docs/ARCHITECTURE_Unity.md - UseCase + Tool Pattern (DDD Integration)
    /// 
    /// This Tool class delegates to ExecuteMenuItemUseCase for business logic execution,
    /// following the UseCase + Tool pattern for separation of concerns.
    /// 
    /// Related classes:
    /// - ExecuteMenuItemUseCase: Business logic and orchestration
    /// - ExecuteMenuItemSchema: Type-safe parameter schema
    /// - ExecuteMenuItemResponse: Type-safe response structure
    /// </summary>
    [McpTool(
        RequiredSecuritySetting = SecuritySettings.AllowMenuItemExecution,
        Description = "Execute Unity MenuItem by path"
    )]
    public class ExecuteMenuItemTool : AbstractUnityTool<ExecuteMenuItemSchema, ExecuteMenuItemResponse>
    {
        public override string ToolName => "execute-menu-item";

        protected override async Task<ExecuteMenuItemResponse> ExecuteAsync(ExecuteMenuItemSchema parameters, CancellationToken cancellationToken)
        {
            // Create and execute ExecuteMenuItemUseCase instance
            ExecuteMenuItemUseCase useCase = new();
            return await useCase.ExecuteAsync(parameters, cancellationToken);
        }
    }
}