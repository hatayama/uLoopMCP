#nullable enable
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace io.github.hatayama.uLoopMCP
{
    public class DemoDraggableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private DropZone? dropZone;

        private RectTransform rectTransform = null!;
        private Canvas canvas = null!;
        private CanvasGroup canvasGroup = null!;
        private Vector2 startPosition;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>();
            canvasGroup = GetComponent<CanvasGroup>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            startPosition = rectTransform.anchoredPosition;
            canvasGroup.alpha = 0.6f;
            rectTransform.localScale = Vector3.one * 1.1f;

            Debug.Log($"[Demo] BeginDrag: {gameObject.name} at {startPosition}");
        }

        public void OnDrag(PointerEventData eventData)
        {
            rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            canvasGroup.alpha = 1f;
            rectTransform.localScale = Vector3.one;

            // Position-based drop detection (SimulateMouseTool does not dispatch IDropHandler events)
            if (dropZone != null && dropZone.ContainsScreenPoint(eventData.position))
            {
                dropZone.OnItemDropped(gameObject);
            }

            Debug.Log($"[Demo] EndDrag: {gameObject.name} moved from {startPosition} to {rectTransform.anchoredPosition}");
        }
    }
}
