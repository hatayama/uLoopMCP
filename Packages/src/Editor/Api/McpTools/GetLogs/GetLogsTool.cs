using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// GetLogs tool handler - Type-safe implementation using Schema and Response
    /// Retrieves Unity console logs with filtering options
    /// </summary>
    [McpTool(Description = "Retrieve logs from Unity Console")]
    public class GetLogsTool : AbstractUnityTool<GetLogsSchema, GetLogsResponse>
    {
        public override string ToolName => "get-logs";



        protected override async Task<GetLogsResponse> ExecuteAsync(GetLogsSchema parameters, CancellationToken cancellationToken)
        {
            // GetLogsUseCaseインスタンスを生成して実行
            GetLogsUseCase useCase = new();
            return await useCase.ExecuteAsync(parameters, cancellationToken);
        }
    }
} 