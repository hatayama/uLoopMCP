using System.Threading;
using System.Threading.Tasks;

using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Reports which view the Game tab is showing, or switches it between the Game view and the
    /// Device Simulator.
    /// </summary>
    [UnityCliLoopTool]
    public sealed class GameViewModeTool : UnityCliLoopTool<GameViewModeSchema, GameViewModeResponse>
    {
        public override string ToolName => "game-view-mode";

        protected override Task<GameViewModeResponse> ExecuteAsync(
            GameViewModeSchema parameters,
            CancellationToken ct)
        {
            GameViewModeUseCase useCase = new(new EditorGameViewSwitchPort());
            return Task.FromResult(useCase.Execute(parameters));
        }
    }
}
