#nullable enable
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace io.github.hatayama.uLoopMCP
{
    public class DropZone : MonoBehaviour
    {
        [SerializeField] private Text? statusText;
        [SerializeField] private Color highlightColor = new Color(0.5f, 1f, 0.5f, 1f);

        private Image image = null!;
        private Color normalColor;
        private RectTransform rectTransform = null!;

        private void Awake()
        {
            image = GetComponent<Image>();
            normalColor = image.color;
            rectTransform = GetComponent<RectTransform>();
        }

        public bool ContainsScreenPoint(Vector2 screenPoint)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPoint);
        }

        public void OnItemDropped(GameObject item)
        {
            if (statusText != null)
            {
                statusText.text = $"Dropped: {item.name}";
            }

            Debug.Log($"[Demo] '{item.name}' dropped on DropZone");
            StopAllCoroutines();
            StartCoroutine(FlashHighlight());
        }

        private IEnumerator FlashHighlight()
        {
            image.color = highlightColor;
            yield return new WaitForSeconds(0.3f);
            image.color = normalColor;
        }
    }
}
