using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// MCP tool entry point that triggers the FocusUnityWindowUseCase.
    /// </summary>
    [McpTool(Description = "Bring the currently connected Unity Editor window to the front")]
    public class FocusUnityWindowTool : AbstractUnityTool<FocusUnityWindowSchema, FocusUnityWindowResponse>
    {
        /// <inheritdoc />
        public override string ToolName => "focus-window";

        /// <inheritdoc />
        protected override Task<FocusUnityWindowResponse> ExecuteAsync(
            FocusUnityWindowSchema parameters,
            CancellationToken ct)
        {
            FocusUnityWindowUseCase useCase = new FocusUnityWindowUseCase();
            return useCase.ExecuteAsync(parameters, ct);
        }
    }
}
