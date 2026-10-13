#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Collects the interactive controls of the screen-space UI Toolkit panels shown in the Game View, in the
    /// same shape and bottom-left screen coordinates as the uGUI annotator, skipping controls that pointer input
    /// at the screenshot's point of view could not reach.
    /// </summary>
    internal static class UIToolkitElementCollector
    {
        // gameViewHeight, not Screen.height: uloop commands run outside the player loop, where Screen reports the
        // size of the last Editor view drawn. ConvertToSimCoordinates flips the result back with the Game View
        // height, so the same height is used here.
        internal static void Collect(List<UIElementInfo> elements, float gameViewHeight)
        {
            IReadOnlyList<UIToolkitScreenControl> controls = UIToolkitScreenElements.CollectControls(
                topLeftBounds => GenerateTopLeftProbePoints(topLeftBounds, gameViewHeight));
            foreach (UIToolkitScreenControl control in controls)
            {
                Rect bottomLeftBounds = ToBottomLeft(control.TopLeftBounds, gameViewHeight);
                elements.Add(new UIElementInfo
                {
                    Name = control.Name,
                    Path = control.Path,
                    Type = control.Type,
                    Interaction = UIElementAnnotationStyling.GetInteractionForType(control.Type),
                    UiSystem = AnnotatedUiSystem.UIToolkit,
                    SimX = control.TopLeftReachablePoint.x,
                    SimY = gameViewHeight - control.TopLeftReachablePoint.y,
                    BoundsMinX = bottomLeftBounds.xMin,
                    BoundsMinY = bottomLeftBounds.yMin,
                    BoundsMaxX = bottomLeftBounds.xMax,
                    BoundsMaxY = bottomLeftBounds.yMax,
                    SortingOrder = control.SortingOrder,
                    SiblingIndex = control.SiblingIndex
                });
            }
        }

        // Probes the same points as the uGUI annotator, converted to the top-left origin panels take input in.
        private static IReadOnlyList<Vector2> GenerateTopLeftProbePoints(Rect topLeftBounds, float gameViewHeight)
        {
            Rect bottomLeftBounds = ToBottomLeft(topLeftBounds, gameViewHeight);
            Vector2[] bottomLeftProbes = UIElementAnnotator.GenerateReachabilityProbePoints(
                bottomLeftBounds.xMin, bottomLeftBounds.yMin, bottomLeftBounds.xMax, bottomLeftBounds.yMax);
            return bottomLeftProbes
                .Select(probe => new Vector2(probe.x, gameViewHeight - probe.y))
                .ToArray();
        }

        private static Rect ToBottomLeft(Rect topLeftBounds, float gameViewHeight)
        {
            return Rect.MinMaxRect(
                topLeftBounds.xMin,
                gameViewHeight - topLeftBounds.yMax,
                topLeftBounds.xMax,
                gameViewHeight - topLeftBounds.yMin);
        }
    }
}
