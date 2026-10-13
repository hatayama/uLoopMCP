#nullable enable
using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// One interactive UI Toolkit control on screen, in top-left Game View pixels.
    /// </summary>
    public sealed class UIToolkitScreenControl
    {
        public UIToolkitScreenControl(
            string name,
            string path,
            string type,
            Rect topLeftBounds,
            Vector2 topLeftReachablePoint,
            int sortingOrder,
            int siblingIndex)
        {
            Name = name;
            Path = path;
            Type = type;
            TopLeftBounds = topLeftBounds;
            TopLeftReachablePoint = topLeftReachablePoint;
            SortingOrder = sortingOrder;
            SiblingIndex = siblingIndex;
        }

        /// <summary>The element's name, or its type name when it has none.</summary>
        public string Name { get; }

        /// <summary>The owning document's GameObject path, '#', and the element chain below the document root.</summary>
        public string Path { get; }

        /// <summary>The kind of control, such as "Button", "Toggle" or "Slider".</summary>
        public string Type { get; }

        public Rect TopLeftBounds { get; }

        /// <summary>A point inside the bounds where pointer input reaches this control.</summary>
        public Vector2 TopLeftReachablePoint { get; }

        /// <summary>The panel's sorting order, rounded down.</summary>
        public int SortingOrder { get; }

        /// <summary>The control's index among its parent's children.</summary>
        public int SiblingIndex { get; }
    }
}
