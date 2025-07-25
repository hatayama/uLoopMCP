using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// ClearConsole tool handler - Type-safe implementation using Schema and Response
    /// Clears Unity console logs for clean development workflow
    /// Related classes:
    /// - ConsoleUtility: Service layer for console operations
    /// - ClearConsoleSchema: Type-safe parameter schema
    /// - ClearConsoleResponse: Type-safe response structure
    /// </summary>
    [McpTool(Description = "Clear Unity console logs")]
    public class ClearConsoleTool : AbstractUnityTool<ClearConsoleSchema, ClearConsoleResponse>
    {
        public override string ToolName => "clear-console";

        /// <summary>
        /// Execute console clear tool
        /// </summary>
        /// <param name="parameters">Type-safe parameters</param>
        /// <param name="cancellationToken">Cancellation token for timeout control</param>
        /// <returns>Clear operation result</returns>
        protected override async Task<ClearConsoleResponse> ExecuteAsync(ClearConsoleSchema parameters, CancellationToken cancellationToken)
        {
            // ClearConsoleUseCaseインスタンスを生成して実行
            var useCase = new ClearConsoleUseCase();
            return await useCase.ExecuteAsync(parameters, cancellationToken);
        }
    }
} 