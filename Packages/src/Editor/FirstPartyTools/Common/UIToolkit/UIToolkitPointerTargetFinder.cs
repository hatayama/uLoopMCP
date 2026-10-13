#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Decides which panel takes pointer input at a Game View point and which control to report for it.
    /// </summary>
    internal static class UIToolkitPointerTargetFinder
    {
        // Expects the panels' layouts to be settled already, because picking reads them. The first panel, front
        // to back, that picks anything takes the input, as Unity routes it. Panels with the same sorting order
        // keep the order they were gathered in, the same known limit as the annotation's cover check.
        internal static UIToolkitPointerTarget? Find(List<UIToolkitScreenPanels.PanelEntry> panels, Vector2 gameViewTopLeft)
        {
            foreach (UIToolkitScreenPanels.PanelEntry entry in panels.OrderByDescending(panel => panel.SortingOrder))
            {
                VisualElement? picked = UIToolkitScreenPanels.PickInPanel(entry.Panel, gameViewTopLeft);
                if (picked == null)
                {
                    continue;
                }

                return CreateTarget(entry, picked);
            }

            return null;
        }

        private static UIToolkitPointerTarget CreateTarget(UIToolkitScreenPanels.PanelEntry entry, VisualElement picked)
        {
            UIDocument? document = FindOwningDocument(entry, picked);
            if (document == null)
            {
                // Elements outside every document, such as the menu of an open dropdown, have no annotation path.
                // The panel's first document stands in to tell when the panel goes away.
                return new UIToolkitPointerTarget(
                    entry.Panel, entry.Documents[0], UIToolkitElementPath.GetDisplayName(picked), null, entry.SortingOrder);
            }

            VisualElement reported = FindOutermostControl(document.rootVisualElement, picked) ?? picked;
            return new UIToolkitPointerTarget(
                entry.Panel,
                document,
                UIToolkitElementPath.GetDisplayName(reported),
                UIToolkitElementPath.Build(document, reported),
                entry.SortingOrder);
        }

        private static UIDocument? FindOwningDocument(UIToolkitScreenPanels.PanelEntry entry, VisualElement picked)
        {
            for (VisualElement? current = picked; current != null; current = current.hierarchy.parent)
            {
                UIDocument? document = entry.Documents.Find(candidate => candidate.rootVisualElement == current);
                if (document != null)
                {
                    return document;
                }
            }

            return null;
        }

        // The outermost, not the nearest, control: annotations stop at the first control on the way down from
        // the root and never list the parts inside it. A Scroller holds a Slider and two RepeatButtons, and a
        // Slider with an input field holds a TextField; reporting those parts would not match the annotation.
        private static VisualElement? FindOutermostControl(VisualElement root, VisualElement picked)
        {
            List<VisualElement> chain = new List<VisualElement>();
            for (VisualElement? current = picked; current != null && current != root; current = current.hierarchy.parent)
            {
                chain.Add(current);
            }

            chain.Reverse();
            return chain.Find(element => UIToolkitElementClassifier.Classify(element) != null);
        }
    }
}
