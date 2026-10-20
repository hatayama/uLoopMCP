using System.Threading;
using System.Threading.Tasks;

using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Switches the current Game view tab to the Device Simulator or back, or reports which one is
    /// showing.
    /// </summary>
    [UnityCliLoopTool]
    public sealed class SwitchGameViewTool : UnityCliLoopTool<SwitchGameViewSchema, SwitchGameViewResponse>
    {
        public override string ToolName => "switch-game-view";

        protected override Task<SwitchGameViewResponse> ExecuteAsync(
            SwitchGameViewSchema parameters,
            CancellationToken ct)
        {
            SwitchGameViewUseCase useCase = new(new EditorGameViewSwitchPort());
            return Task.FromResult(useCase.Execute(parameters));
        }
    }
}
