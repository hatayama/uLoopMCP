#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

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
        private const char PATH_SEPARATOR = '#';
        private const string CHAIN_SEPARATOR = "/";

        internal static void Collect(List<UIElementInfo> elements)
        {
            List<PanelEntry> panels = GatherPanels();

            // worldBound and visualTree.layout are only settled once the panel validates its layout, and Pick is
            // the only public call that does so. Every panel is settled, including those used only to decide
            // what covers what, because PickInPanel reads their layout too.
            foreach (PanelEntry entry in panels)
            {
                entry.Panel.Pick(Vector2.zero);
            }

            foreach (PanelEntry entry in panels)
            {
                if (entry.Mapping == null)
                {
                    continue;
                }

                VisitContext context = new VisitContext(
                    entry,
                    entry.Mapping,
                    CreateFrontPickers(panels, entry),
                    point => PickInPanel(entry.Panel, point),
                    elements);
                foreach (UIDocument document in entry.Documents)
                {
                    Visit(document.rootVisualElement, document, context);
                }
            }
        }

        private static List<PanelEntry> GatherPanels()
        {
#if UNITY_6000_4_OR_NEWER
            UIDocument[] documents = UnityEngine.Object.FindObjectsByType<UIDocument>();
#else
            UIDocument[] documents = UnityEngine.Object.FindObjectsOfType<UIDocument>();
#endif
            List<PanelEntry> panels = new List<PanelEntry>();
            foreach (UIDocument document in documents)
            {
                IPanel? panel = GetScreenSpacePanelShownInGameView(document);
                if (panel == null)
                {
                    continue;
                }

                PanelEntry? entry = panels.Find(candidate => candidate.Panel == panel);
                if (entry == null)
                {
                    entry = CreatePanelEntry(panel, document.panelSettings);
                    panels.Add(entry);
                }

                entry.Documents.Add(document);
            }

            return panels;
        }

        // Returns null for documents that contribute nothing: their panel neither shows in the Game View nor
        // takes input there, or they are nested and reached through their parent document's root.
        private static IPanel? GetScreenSpacePanelShownInGameView(UIDocument document)
        {
            if (!document.isActiveAndEnabled || document.parentUI != null)
            {
                return null;
            }

            PanelSettings settings = document.panelSettings;
            VisualElement root = document.rootVisualElement;
            if (settings == null || root == null || root.panel == null)
            {
                return null;
            }

            if (settings.targetDisplay != Display.activeEditorGameViewTarget)
            {
                return null;
            }

            // World-space panels do not cover overlay controls and their elements have no screen rectangle.
            if (UIToolkitPanelKind.IsWorldSpace(settings))
            {
                return null;
            }

            return root.panel;
        }

        // A panel that renders into a texture still takes screen input, so it stays as a cover candidate, but
        // its controls are not on the screenshot and get no mapping.
        private static PanelEntry CreatePanelEntry(IPanel panel, PanelSettings settings)
        {
            UIToolkitScreenMapping? mapping = settings.targetTexture == null
                ? UIToolkitScreenMapping.TryCreate(screen => RuntimePanelUtils.ScreenToPanel(panel, screen))
                : null;
            return new PanelEntry(panel, settings.sortingOrder, mapping);
        }

        // Panels with the same sorting order are not treated as covering each other: Unity breaks that tie by
        // creation order, which is not public.
        private static List<Func<Vector2, VisualElement?>> CreateFrontPickers(List<PanelEntry> panels, PanelEntry entry)
        {
            return panels
                .Where(other => other.SortingOrder > entry.SortingOrder)
                .OrderByDescending(other => other.SortingOrder)
                .Select(other => (Func<Vector2, VisualElement?>)(point => PickInPanel(other.Panel, point)))
                .ToList();
        }

        // Uses the forward screen-to-panel function Unity applies to input, so the result matches where input
        // goes even for panels whose function cannot be inverted.
        private static VisualElement? PickInPanel(IPanel panel, Vector2 screenTopLeftPoint)
        {
            Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(panel, screenTopLeftPoint);
            if (!panel.visualTree.layout.Contains(panelPoint))
            {
                return null;
            }

            return panel.Pick(panelPoint);
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

            // Screen.height, not the Game View size, because Unity flips input with Screen.height before handing
            // it to the panel.
            float screenHeight = Screen.height;
            Vector2 topLeftMin = context.Mapping.PanelToScreenTopLeft(new Vector2(worldBound.xMin, worldBound.yMin));
            Vector2 topLeftMax = context.Mapping.PanelToScreenTopLeft(new Vector2(worldBound.xMax, worldBound.yMax));
            Rect bottomLeftBounds = Rect.MinMaxRect(
                topLeftMin.x,
                screenHeight - topLeftMax.y,
                topLeftMax.x,
                screenHeight - topLeftMin.y);

            Vector2? reachable = FindReachablePoint(element, bottomLeftBounds, screenHeight, context);
            if (reachable == null)
            {
                return;
            }

            context.Elements.Add(CreateElementInfo(
                element, type, document, context.Entry, bottomLeftBounds, new Vector2(reachable.Value.x, screenHeight - reachable.Value.y)));
        }

        // Probes the same points as the uGUI annotator, converted to the top-left origin panels take input in.
        private static Vector2? FindReachablePoint(
            VisualElement element, Rect bottomLeftBounds, float screenHeight, VisitContext context)
        {
            Vector2[] bottomLeftProbes = UIElementAnnotator.GenerateReachabilityProbePoints(
                bottomLeftBounds.xMin, bottomLeftBounds.yMin, bottomLeftBounds.xMax, bottomLeftBounds.yMax);
            Vector2[] topLeftProbes = bottomLeftProbes
                .Select(probe => new Vector2(probe.x, screenHeight - probe.y))
                .ToArray();
            return UIToolkitReachabilityProbe.FindReachablePoint(
                topLeftProbes, element, context.FrontPickers, context.OwnPicker);
        }

        private static UIElementInfo CreateElementInfo(
            VisualElement element,
            string type,
            UIDocument document,
            PanelEntry entry,
            Rect bottomLeftBounds,
            Vector2 bottomLeftPoint)
        {
            VisualElement? parent = element.hierarchy.parent;
            return new UIElementInfo
            {
                Name = GetDisplayName(element),
                Path = GameObjectPathUtility.GetFullPath(document.gameObject) + PATH_SEPARATOR
                    + BuildElementChain(document.rootVisualElement, element),
                Type = type,
                Interaction = UIElementAnnotationStyling.GetInteractionForType(type),
                UiSystem = AnnotatedUiSystem.UIToolkit,
                SimX = bottomLeftPoint.x,
                SimY = bottomLeftPoint.y,
                BoundsMinX = bottomLeftBounds.xMin,
                BoundsMinY = bottomLeftBounds.yMin,
                BoundsMaxX = bottomLeftBounds.xMax,
                BoundsMaxY = bottomLeftBounds.yMax,
                SortingOrder = Mathf.FloorToInt(entry.SortingOrder),
                SiblingIndex = parent == null ? 0 : parent.hierarchy.IndexOf(element)
            };
        }

        // Lists the elements from just below the document root down to the element, so the root itself, which
        // has no name of its own, is left out.
        private static string BuildElementChain(VisualElement root, VisualElement element)
        {
            List<string> segments = new List<string>();
            VisualElement? current = element;
            while (current != null && current != root)
            {
                segments.Add(GetDisplayName(current));
                current = current.hierarchy.parent;
            }

            segments.Reverse();
            return string.Join(CHAIN_SEPARATOR, segments);
        }

        private static string GetDisplayName(VisualElement element)
        {
            return string.IsNullOrEmpty(element.name) ? element.GetType().Name : element.name;
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

        private sealed class PanelEntry
        {
            internal PanelEntry(IPanel panel, float sortingOrder, UIToolkitScreenMapping? mapping)
            {
                Panel = panel;
                SortingOrder = sortingOrder;
                Mapping = mapping;
            }

            internal IPanel Panel { get; }
            internal float SortingOrder { get; }

            // Null when the panel's controls cannot be placed on the screenshot; the panel still covers others.
            internal UIToolkitScreenMapping? Mapping { get; }
            internal List<UIDocument> Documents { get; } = new List<UIDocument>();
        }

        private sealed class VisitContext
        {
            internal VisitContext(
                PanelEntry entry,
                UIToolkitScreenMapping mapping,
                List<Func<Vector2, VisualElement?>> frontPickers,
                Func<Vector2, VisualElement?> ownPicker,
                List<UIElementInfo> elements)
            {
                Entry = entry;
                Mapping = mapping;
                FrontPickers = frontPickers;
                OwnPicker = ownPicker;
                Elements = elements;
            }

            internal PanelEntry Entry { get; }
            internal UIToolkitScreenMapping Mapping { get; }
            internal List<Func<Vector2, VisualElement?>> FrontPickers { get; }
            internal Func<Vector2, VisualElement?> OwnPicker { get; }
            internal List<UIElementInfo> Elements { get; }
        }
    }
}
