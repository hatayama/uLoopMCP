#nullable enable
using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Decides whether a uGUI hit or a UI Toolkit hit at the same point takes the input, the way the
    /// EventSystem orders them for real input.
    /// </summary>
    internal static class MouseUiHitPriority
    {
        // The EventSystem compares hits from different raycasters by camera depth only when both have a camera,
        // and the UI Toolkit raycaster has none; then by sortOrderPriority, where a panel reports its sorting
        // order rounded down; then by renderOrderPriority, where a panel reports nearly int.MaxValue. So uGUI is
        // in front only when its sortOrderPriority is strictly greater.
        internal static bool IsUguiInFront(int uguiSortOrderPriority, float panelSortingOrder)
        {
            return uguiSortOrderPriority > Mathf.FloorToInt(panelSortingOrder);
        }
    }
}
