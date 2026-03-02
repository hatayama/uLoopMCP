using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Provides EventSystem-based UI interaction for UGUI elements.
    /// Supports StandaloneInputModule and InputSystemUIInputModule.
    /// </summary>
    public static class EventSystemHelper
    {
        // Reuse across calls to avoid GC pressure on mobile devices
        private static readonly List<RaycastResult> SharedRaycastResults = new();

        public static bool IsEventSystemAvailable()
        {
            return EventSystem.current != null;
        }

        public static bool SimulateClick(GameObject target)
        {
            Debug.Assert(target != null, "target must not be null");

            if (EventSystem.current == null) return false;

            PointerEventData pointerData = new(EventSystem.current)
            {
                position = GetScreenPosition(target)
            };

            ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerClickHandler);

            return true;
        }

        public static bool SimulateClickAtPosition(Vector2 screenPosition, out GameObject hitObject)
        {
            hitObject = RaycastFirstHit(screenPosition);
            if (hitObject == null) return false;

            PointerEventData pointerData = new(EventSystem.current)
            {
                position = screenPosition
            };

            ExecuteEvents.Execute(hitObject, pointerData, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(hitObject, pointerData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(hitObject, pointerData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(hitObject, pointerData, ExecuteEvents.pointerClickHandler);

            return true;
        }

        public static GameObject RaycastFirstHit(Vector2 screenPosition)
        {
            if (EventSystem.current == null) return null;

            PointerEventData pointerData = new(EventSystem.current)
            {
                position = screenPosition
            };

            return RaycastFirstHit(pointerData);
        }

        public static GameObject RaycastFirstHit(PointerEventData pointerData)
        {
            if (EventSystem.current == null) return null;

            SharedRaycastResults.Clear();
            EventSystem.current.RaycastAll(pointerData, SharedRaycastResults);

            return SharedRaycastResults.Count > 0 ? SharedRaycastResults[0].gameObject : null;
        }

        // EventSystem only exposes submitHandler/cancelHandler; arbitrary KeyCode injection
        // requires Input System's InputTestFixture which is unavailable in runtime builds
        public static void SimulateKey(KeyCode keyCode)
        {
            if (EventSystem.current == null) return;

            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null) return;

            BaseEventData eventData = new(EventSystem.current);

            if (keyCode == KeyCode.Escape)
            {
                ExecuteEvents.Execute(selected, eventData, ExecuteEvents.cancelHandler);
            }
            else
            {
                ExecuteEvents.Execute(selected, eventData, ExecuteEvents.submitHandler);
            }
        }

        public static Vector2 GetScreenPosition(GameObject go)
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) return Vector2.zero;

            RectTransform rectTransform = go.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Vector3[] corners = new Vector3[4];
                rectTransform.GetWorldCorners(corners);
                Vector3 center = (corners[0] + corners[2]) / 2f;
                return mainCam.WorldToScreenPoint(center);
            }

            return mainCam.WorldToScreenPoint(go.transform.position);
        }
    }
}
