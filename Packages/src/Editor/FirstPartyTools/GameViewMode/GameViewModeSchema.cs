using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Which Play Mode view the current Game view tab becomes. Spelled like Unity's own concepts
    /// (ADR 0006).
    /// </summary>
    public enum GameViewModeTarget
    {
        GameView = 0,
        Simulator = 1,
    }

    /// <summary>
    /// Describes the optional target accepted by the game-view-mode tool.
    /// </summary>
    public sealed class GameViewModeSchema : UnityCliLoopToolSchema
    {
        /// <summary>
        /// Target view. Omit to report the current view without changing it.
        /// </summary>
        public GameViewModeTarget? To { get; set; }

        /// <summary>
        /// Device to show in the Simulator, matched case-insensitively against the device names.
        /// Implies Simulator as the target when <see cref="To"/> is omitted. Null when not given.
        /// </summary>
        public string Device { get; set; }

        /// <summary>
        /// When true the response lists every device the Simulator can show.
        /// </summary>
        public bool ListDevices { get; set; } = false;
    }
}
