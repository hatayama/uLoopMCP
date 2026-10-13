#nullable enable
using UnityEngine;
using UnityEngine.EventSystems;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// A drag on a uGUI or physics IDragHandler, driven through the EventSystem's pointer events.
    /// </summary>
    internal sealed class UguiDragSession : IMouseUiDragSession
    {
        private readonly PointerEventData _pointerData;
        private readonly GameObject _target;

        private UguiDragSession(PointerEventData pointerData, GameObject target)
        {
            _pointerData = pointerData;
            _target = target;
            Report = MouseUiGameObjectHitReport.Create(target);
        }

        /// <summary>
        /// Presses on the start raycast and sends beginDrag to the drag handler.
        /// </summary>
        internal static UguiDragSession Begin(
            EventSystem eventSystem,
            Vector2 screenPos,
            RaycastResult startRaycast,
            GameObject target)
        {
            // uGUI drag controls (ScrollRect, Slider) only respond to left-button drags
            PointerEventData pointerData = MouseUiDragEventExecutor.InitiateDrag(
                eventSystem, screenPos, startRaycast, target, PointerEventData.InputButton.Left);
            ExecuteEvents.Execute(target, pointerData, ExecuteEvents.beginDragHandler);
            pointerData.dragging = true;
            return new UguiDragSession(pointerData, target);
        }

        public MouseUiHitReport Report { get; }

        // Derived from the stored screen position with the current Game View height rather than stored as an
        // input position, so a split drag continues from the same screen point if the Game View is resized.
        public Vector2 CurrentInputPosition => MouseUiCoordinateConverter.ScreenToInput(_pointerData.position);

        public bool IsTargetDestroyed => _target == null;

        // User input during a CLI drag can cause Unity's StandaloneInputModule to
        // release or reassign the drag, leaving the stored session stale.
        public string? CheckStillActive()
        {
            if (!_target.activeInHierarchy)
            {
                return "Drag target was destroyed or deactivated during drag.";
            }

            if (!_pointerData.dragging || _pointerData.pointerDrag != _target)
            {
                return "Drag was interrupted by user input or system event.";
            }

            return null;
        }

        public void MoveTo(Vector2 inputPosition)
        {
            Vector2 screenPosition = MouseUiCoordinateConverter.InputToScreen(inputPosition);
            Vector2 previousPosition = _pointerData.position;
            _pointerData.position = screenPosition;
            _pointerData.delta = screenPosition - previousPosition;
            MouseUiInputSystemSync.SyncMousePosition(screenPosition);
            ExecuteEvents.Execute(_target, _pointerData, ExecuteEvents.dragHandler);
        }

        public void Finish(GameObject? explicitDropTarget)
        {
            MouseUiDragEventExecutor.FinalizeDrag(_pointerData, _target, explicitDropTarget);
        }
    }
}
