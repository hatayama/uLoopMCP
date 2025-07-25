using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// ExecuteMenuItem tool handler - Executes Unity MenuItems by path
    /// Supports both EditorApplication.ExecuteMenuItem and reflection-based execution
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
            // ExecuteMenuItemUseCaseインスタンスを生成して実行
            var useCase = new ExecuteMenuItemUseCase();
            return await useCase.ExecuteAsync(parameters, cancellationToken);
        }
    }
}