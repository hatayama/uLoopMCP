#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Walks the documents of the gathered panels and lists their reachable controls in top-left Game View
    /// pixels, skipping controls that pointer input could not reach.
    /// </summary>
    internal static class UIToolkitControlCollector
    {
        // Expects the panels' layouts to be settled already, because bounds and picking read them.
        internal static List<UIToolkitScreenControl> Collect(
            List<UIToolkitScreenPanels.PanelEntry> panels,
            Func<Rect, IReadOnlyList<Vector2>> probePointsForTopLeftBounds)
        {
            List<UIToolkitScreenControl> controls = new List<UIToolkitScreenControl>();
            foreach (UIToolkitScreenPanels.PanelEntry entry in panels)
            {
                if (entry.Mapping == null)
                {
                    continue;
                }

                VisitContext context = new VisitContext(
                    entry,
                    entry.Mapping,
                    UIToolkitScreenPanels.CreateFrontPickers(panels, entry),
                    point => UIToolkitScreenPanels.PickInPanel(entry.Panel, point),
                    probePointsForTopLeftBounds,
                    controls);
                foreach (UIDocument document in entry.Documents)
                {
                    Visit(document.rootVisualElement, document, context);
                }
            }

            return controls;
        }

        private static void Visit(VisualElement element, UIDocument document, VisitContext context)
        {
            // A child can still resolve to Flex under a display:none parent, so the whole subtree stops here.
            if (element.resolvedStyle.display == DisplayStyle.None)
            {
                return;
            }

            string? type = UIToolkitElementClassifier.Classify(element);
            if (type != null)
            {
                TryAdd(element, type, document, context);

                // The parts inside a control (the slider and buttons of a Scroller, for example) are not
                // separate controls.
                return;
            }

            // hierarchy, not Children(): Children() lists only the content container, which hides a
            // ScrollView's scrollers.
            for (int index = 0; index < element.hierarchy.childCount; index++)
            {
                Visit(element.hierarchy[index], document, context);
            }
        }

        private static void TryAdd(VisualElement element, string type, UIDocument document, VisitContext context)
        {
            if (!element.enabledInHierarchy || element.resolvedStyle.visibility == Visibility.Hidden)
            {
                return;
            }

            Rect worldBound = element.worldBound;
            if (!IsFiniteNonEmpty(worldBound))
            {
                return;
            }

            Vector2 topLeftMin = context.Mapping.PanelToScreenTopLeft(new Vector2(worldBound.xMin, worldBound.yMin));
            Vector2 topLeftMax = context.Mapping.PanelToScreenTopLeft(new Vector2(worldBound.xMax, worldBound.yMax));
            Rect topLeftBounds = Rect.MinMaxRect(topLeftMin.x, topLeftMin.y, topLeftMax.x, topLeftMax.y);

            Vector2? reachable = UIToolkitReachabilityProbe.FindReachablePoint(
                context.ProbePointsForTopLeftBounds(topLeftBounds), element, context.FrontPickers, context.OwnPicker);
            if (reachable == null)
            {
                return;
            }

            VisualElement? parent = element.hierarchy.parent;
            context.Controls.Add(new UIToolkitScreenControl(
                UIToolkitElementPath.GetDisplayName(element),
                UIToolkitElementPath.Build(document, element),
                type,
                topLeftBounds,
                reachable.Value,
                Mathf.FloorToInt(context.Entry.SortingOrder),
                parent == null ? 0 : parent.hierarchy.IndexOf(element)));
        }

        private static bool IsFiniteNonEmpty(Rect rect)
        {
            return float.IsFinite(rect.x)
                && float.IsFinite(rect.y)
                && float.IsFinite(rect.width)
                && float.IsFinite(rect.height)
                && rect.width > 0f
                && rect.height > 0f;
        }

        private sealed class VisitContext
        {
            internal VisitContext(
                UIToolkitScreenPanels.PanelEntry entry,
                UIToolkitScreenMapping mapping,
                List<Func<Vector2, VisualElement?>> frontPickers,
                Func<Vector2, VisualElement?> ownPicker,
                Func<Rect, IReadOnlyList<Vector2>> probePointsForTopLeftBounds,
                List<UIToolkitScreenControl> controls)
            {
                Entry = entry;
                Mapping = mapping;
                FrontPickers = frontPickers;
                OwnPicker = ownPicker;
                ProbePointsForTopLeftBounds = probePointsForTopLeftBounds;
                Controls = controls;
            }

            internal UIToolkitScreenPanels.PanelEntry Entry { get; }
            internal UIToolkitScreenMapping Mapping { get; }
            internal List<Func<Vector2, VisualElement?>> FrontPickers { get; }
            internal Func<Vector2, VisualElement?> OwnPicker { get; }
            internal Func<Rect, IReadOnlyList<Vector2>> ProbePointsForTopLeftBounds { get; }
            internal List<UIToolkitScreenControl> Controls { get; }
        }
    }
}
