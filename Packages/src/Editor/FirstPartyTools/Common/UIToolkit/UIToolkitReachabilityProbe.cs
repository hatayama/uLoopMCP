#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Finds the first probe point at which pointer input would reach a UI Toolkit element, following the order
    /// Unity routes input: panels in front first, then the element's own panel.
    /// </summary>
    internal static class UIToolkitReachabilityProbe
    {
        // Probe points and pickers use top-left screen coordinates. Front pickers are ordered front to back and
        // return null where their panel would not take the input.
        internal static Vector2? FindReachablePoint(
            IReadOnlyList<Vector2> probePoints,
            VisualElement target,
            IReadOnlyList<Func<Vector2, VisualElement?>> frontPanelPickers,
            Func<Vector2, VisualElement?> ownPanelPicker)
        {
            foreach (Vector2 point in probePoints)
            {
                // Unity hands the input to the first panel that picks anything, so a front panel takes it even
                // when the element it picks is unrelated to the target.
                if (IsCoveredByFrontPanel(point, frontPanelPickers))
                {
                    continue;
                }

                VisualElement? picked = ownPanelPicker(point);
                if (picked == null)
                {
                    continue;
                }

                if (picked == target || IsDescendantOf(picked, target))
                {
                    return point;
                }
            }

            return null;
        }

        private static bool IsCoveredByFrontPanel(Vector2 point, IReadOnlyList<Func<Vector2, VisualElement?>> pickers)
        {
            foreach (Func<Vector2, VisualElement?> picker in pickers)
            {
                if (picker(point) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDescendantOf(VisualElement element, VisualElement ancestor)
        {
            VisualElement? current = element.hierarchy.parent;
            while (current != null)
            {
                if (current == ancestor)
                {
                    return true;
                }

                current = current.hierarchy.parent;
            }

            return false;
        }
    }
}
