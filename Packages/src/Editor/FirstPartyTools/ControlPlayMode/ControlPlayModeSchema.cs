using System.ComponentModel;

using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    public enum PlayModeAction
    {
        Play = 0,
        Stop = 1,
        Pause = 2
    }

    /// <summary>
    /// Describes the parameters accepted by the Control Play Mode tool.
    /// </summary>
    public class ControlPlayModeSchema : UnityCliLoopToolSchema
    {
        [Description("Action to perform: Play(0) - Start play mode, Stop(1) - Stop play mode, Pause(2) - Pause play mode")]
        public PlayModeAction Action { get; set; } = PlayModeAction.Play;
    }
}

