#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Entry point for the UI Toolkit controls shown over the Game View on screen-space panels. Every point and
    /// rectangle is in Game View pixels with the origin at the top left, the space screenshot annotations and
    /// simulated pointer input use.
    /// </summary>
    public static class UIToolkitScreenElements
    {
        /// <summary>
        /// Returns the enabled, visible controls that pointer input can reach, each with a point where it does.
        /// Controls inside another control (the buttons of a scrollbar, for example) are not listed separately.
        /// </summary>
        /// <param name="probePointsForTopLeftBounds">
        /// Gives the candidate points to try for a control's bounds, in the order to try them. The first point
        /// that is not covered by a panel in front or by another element becomes the reachable point.
        /// </param>
        public static IReadOnlyList<UIToolkitScreenControl> CollectControls(
            Func<Rect, IReadOnlyList<Vector2>> probePointsForTopLeftBounds)
        {
            if (probePointsForTopLeftBounds == null)
            {
                throw new ArgumentNullException(nameof(probePointsForTopLeftBounds));
            }

            List<UIToolkitScreenPanels.PanelEntry> panels = UIToolkitScreenPanels.Gather();
            UIToolkitScreenPanels.SettleLayouts(panels);
            return UIToolkitControlCollector.Collect(panels, probePointsForTopLeftBounds);
        }
    }
}
