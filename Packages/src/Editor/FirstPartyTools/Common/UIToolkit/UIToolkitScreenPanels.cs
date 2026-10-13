#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Finds the screen-space UI Toolkit panels that show in the Game View or take input there, and picks
    /// elements on them in top-left Game View pixels.
    /// </summary>
    internal static class UIToolkitScreenPanels
    {
        internal static List<PanelEntry> Gather()
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

        // worldBound and visualTree.layout are only settled once the panel validates its layout, and Pick is
        // the only public call that does so. Every panel is settled, including those used only to decide
        // what covers what, because PickInPanel reads their layout too.
        internal static void SettleLayouts(List<PanelEntry> panels)
        {
            foreach (PanelEntry entry in panels)
            {
                entry.Panel.Pick(Vector2.zero);
            }
        }

        // Panels with the same sorting order are not treated as covering each other: Unity breaks that tie by
        // creation order, which is not public.
        internal static List<Func<Vector2, VisualElement?>> CreateFrontPickers(List<PanelEntry> panels, PanelEntry entry)
        {
            return panels
                .Where(other => other.SortingOrder > entry.SortingOrder)
                .OrderByDescending(other => other.SortingOrder)
                .Select(other => (Func<Vector2, VisualElement?>)(point => PickInPanel(other.Panel, point)))
                .ToList();
        }

        // Uses the forward screen-to-panel function Unity applies to input, so the result matches where input
        // goes even for panels whose function cannot be inverted.
        internal static VisualElement? PickInPanel(IPanel panel, Vector2 screenTopLeftPoint)
        {
            Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(panel, screenTopLeftPoint);
            if (!panel.visualTree.layout.Contains(panelPoint))
            {
                return null;
            }

            return panel.Pick(panelPoint);
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

        internal sealed class PanelEntry
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
    }
}
