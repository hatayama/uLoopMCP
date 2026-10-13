#nullable enable
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Sends mouse pointer events to a runtime panel the way Unity turns real mouse input into them.
    /// </summary>
    internal static class UIToolkitPointerEvents
    {
        // No target is set, so the panel hands the event to the element holding the pointer or, failing that,
        // the element under it, as it does for real input. GetPooled also records the press or release in the
        // pointer device state, which fills pressedButtons for later events.
        internal static void Send(IPanel panel, EventType type, Vector2 panelPoint, Vector2 delta, int button)
        {
            Event systemEvent = new Event
            {
                type = type,
                mousePosition = panelPoint,
                delta = delta,
                button = button,
                clickCount = type == EventType.MouseDown || type == EventType.MouseUp ? 1 : 0
            };

            switch (type)
            {
                case EventType.MouseDown:
                    using (PointerDownEvent pointerDown = PointerDownEvent.GetPooled(systemEvent))
                    {
                        panel.visualTree.SendEvent(pointerDown);
                    }

                    return;
                case EventType.MouseMove:
                    using (PointerMoveEvent pointerMove = PointerMoveEvent.GetPooled(systemEvent))
                    {
                        panel.visualTree.SendEvent(pointerMove);
                    }

                    return;
                case EventType.MouseUp:
                    using (PointerUpEvent pointerUp = PointerUpEvent.GetPooled(systemEvent))
                    {
                        panel.visualTree.SendEvent(pointerUp);
                    }

                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, "Only mouse down, move and up are sent.");
            }
        }
    }
}
