#nullable enable
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// The UI Toolkit control under a Game View point, and the panel that takes pointer input there. Presses,
    /// moves and releases are sent to that panel the way real input reaches it, so they land on whichever
    /// element is under the pointer or holds the pointer at that moment.
    /// </summary>
    public sealed class UIToolkitPointerTarget
    {
        private const int MaxMouseButton = 2;

        private readonly IPanel _panel;
        private readonly UIDocument _document;
        private Vector2 _lastPanelPoint;

        internal UIToolkitPointerTarget(IPanel panel, UIDocument document, string name, string? path, float sortingOrder)
        {
            if (string.IsNullOrEmpty(name))
            {
                // Callers treat a null name as "nothing was hit", so a hit must always carry one.
                throw new ArgumentException("A UI Toolkit pointer target needs a display name.", nameof(name));
            }

            _panel = panel ?? throw new ArgumentNullException(nameof(panel));
            _document = document != null ? document : throw new ArgumentNullException(nameof(document));
            Name = name;
            Path = path;
            SortingOrder = sortingOrder;
        }

        /// <summary>The control's name, or its type name when it has none. Never null or empty.</summary>
        public string Name { get; }

        /// <summary>
        /// The same path screenshot annotations report for the control, or null when the element belongs to no
        /// document, as with the menu of an open dropdown.
        /// </summary>
        public string? Path { get; }

        /// <summary>The sorting order of the panel the control is on, as set in its panel settings.</summary>
        public float SortingOrder { get; }

        /// <summary>False once the document that owns the panel has been disabled or destroyed.</summary>
        public bool IsAvailable =>
            _document != null && _document.isActiveAndEnabled && _document.rootVisualElement?.panel == _panel;

        /// <summary>Presses a mouse button (0 left, 1 right, 2 middle) at a Game View point.</summary>
        /// <remarks>Does nothing once the target is no longer available.</remarks>
        public void PressDown(Vector2 gameViewTopLeft, int button)
        {
            ValidateButton(button);
            if (!IsAvailable)
            {
                // The document can go away between resolving the target and pressing, a frame or more later;
                // uGUI likewise sends nothing to a destroyed or inactive target.
                return;
            }

            Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(_panel, gameViewTopLeft);
            UIToolkitPointerEvents.Send(_panel, EventType.MouseDown, panelPoint, Vector2.zero, button);
            _lastPanelPoint = panelPoint;
        }

        /// <summary>Moves the pointer to a Game View point, carrying any buttons still held.</summary>
        /// <remarks>Does nothing once the target is no longer available.</remarks>
        public void MoveTo(Vector2 gameViewTopLeft)
        {
            if (!IsAvailable)
            {
                return;
            }

            Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(_panel, gameViewTopLeft);

            // The button is overwritten with "none" for moves; held buttons travel in pressedButtons.
            UIToolkitPointerEvents.Send(_panel, EventType.MouseMove, panelPoint, panelPoint - _lastPanelPoint, 0);
            _lastPanelPoint = panelPoint;
        }

        /// <summary>Releases a mouse button (0 left, 1 right, 2 middle) at a Game View point.</summary>
        /// <remarks>Does nothing once the target is no longer available, so cleanup code can always call it.</remarks>
        public void Release(Vector2 gameViewTopLeft, int button)
        {
            ValidateButton(button);
            if (!IsAvailable)
            {
                return;
            }

            Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(_panel, gameViewTopLeft);
            UIToolkitPointerEvents.Send(_panel, EventType.MouseUp, panelPoint, panelPoint - _lastPanelPoint, button);
            _lastPanelPoint = panelPoint;
        }

        private static void ValidateButton(int button)
        {
            if (button < 0 || button > MaxMouseButton)
            {
                throw new ArgumentOutOfRangeException(nameof(button), button, "Mouse button must be 0, 1 or 2.");
            }
        }
    }
}
