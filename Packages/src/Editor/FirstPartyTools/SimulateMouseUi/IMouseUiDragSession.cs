#nullable enable
using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// A drag in progress on one target: one-shot drags hold it for a single command, split drags keep it
    /// between commands. Positions are Game View input coordinates with a top-left origin.
    /// </summary>
    internal interface IMouseUiDragSession
    {
        /// <summary>What the drag started on. Its name is never null.</summary>
        MouseUiHitReport Report { get; }

        /// <summary>Where the pointer is now.</summary>
        Vector2 CurrentInputPosition { get; }

        /// <summary>True once the target is destroyed; the drag then counts as no longer in progress.</summary>
        bool IsTargetDestroyed { get; }

        /// <summary>
        /// True when the drag goes through the EventSystem, which then must exist and alone handles drop targets.
        /// </summary>
        bool UsesEventSystem { get; }

        /// <summary>Returns why the drag cannot continue, or null when it can.</summary>
        string? CheckStillActive();

        /// <summary>Moves the pointer one step while the button stays held.</summary>
        void MoveTo(Vector2 inputPosition);

        /// <summary>Releases the button, dropping onto the explicit target when one is given.</summary>
        void Finish(GameObject? explicitDropTarget);
    }
}
