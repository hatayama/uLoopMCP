using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Which Play Mode view the current Game view tab becomes. Spelled like Unity's own concepts
    /// (ADR 0006).
    /// </summary>
    public enum SwitchGameViewTarget
    {
        GameView = 0,
        Simulator = 1,
    }

    /// <summary>
    /// Describes the optional target accepted by the Switch Game View tool.
    /// </summary>
    public sealed class SwitchGameViewSchema : UnityCliLoopToolSchema
    {
        /// <summary>
        /// Target view. Omit to report the current view without changing it.
        /// </summary>
        public SwitchGameViewTarget? To { get; set; }
    }
}
