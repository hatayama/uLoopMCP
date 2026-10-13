#nullable enable

using System.Collections.Generic;

using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Carries the response data returned by the Simulate Mouse UI tool.
    /// </summary>
    public class SimulateMouseUiResponse : UnityCliLoopToolResponse
    {
        public string Message { get; set; } = "";
        public string Action { get; set; } = "";
        /// <summary>
        /// Name of what received the press: the uGUI or physics GameObject that handles it, or the UI Toolkit
        /// control under the point (its name, or its type name when it has none). Null when nothing was hit.
        /// </summary>
        public string? HitGameObjectName { get; set; }

        /// <summary>
        /// Which UI system was hit: "UGUI" for a target under a Canvas, "" for a target outside any Canvas (a
        /// physics hit), "UIToolkit" for a UI Toolkit control, or null when nothing was hit.
        /// </summary>
        public string? UiSystem { get; set; }

        /// <summary>
        /// Path of what was hit, in the form screenshot annotations use: the GameObject's hierarchy path for
        /// uGUI and physics, or the document path, '#', and element chain for UI Toolkit. Null when nothing was
        /// hit or when a UI Toolkit element belongs to no document, such as an open dropdown's menu.
        /// </summary>
        public string? HitPath { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float? EndPositionX { get; set; }
        public float? EndPositionY { get; set; }
        public bool InterruptedByPausePoint { get; set; }
        /// <summary>
        /// Id of the pause point that refused this call before it ran anything, null otherwise.
        /// Distinct from PausePointId, which reports a marker hit *during* the call: a refusal means
        /// no input was injected at all, so a caller reading only Success would miss that the action
        /// never happened. The CLI's --trigger diagnosis compares this against the marker it awaits.
        /// </summary>
        public string? RejectedByActivePausePointId { get; set; }

        /// <summary>
        /// True when this command was refused before it did anything: the PlayMode preflight
        /// rejected it. Why a separate flag from RejectedByActivePausePointId: a refusal is not
        /// always owned by a pause point (PlayMode simply not running is the common case), and the
        /// CLI's --trigger wait has to abort on "the trigger performed no action" without matching
        /// message text. Mid-flight failures leave this false.
        /// </summary>
        public bool RejectedBeforeExecution { get; set; }

        public string? PausePointId { get; set; }
        public int? PausePointHitCount { get; set; }
        public List<UnityCliLoopPausePointHit>? PausePointHits { get; set; }

        public SimulateMouseUiResponse()
        {
        }
    }
}
