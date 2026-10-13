#nullable enable
using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Builds the hit report for a GameObject target reached through the EventSystem.
    /// </summary>
    internal static class MouseUiGameObjectHitReport
    {
        // Decided by whether the handler sits under a Canvas rather than by raycaster type, because a bypass
        // press has no raycaster and both paths must report alike.
        internal static MouseUiHitReport Create(GameObject? target)
        {
            if (target == null)
            {
                return MouseUiHitReport.None;
            }

            string uiSystem = target.GetComponentInParent<Canvas>() != null
                ? MouseUiHitSystem.UGUI
                : MouseUiHitSystem.NonUi;
            return new MouseUiHitReport(target.name, GameObjectPathUtility.GetFullPath(target), uiSystem);
        }
    }
}
