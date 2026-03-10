using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace io.github.hatayama.uLoopMCP
{
    // Creates a temporary Screen Space Overlay Canvas that draws bounding boxes and labels
    // over interactive UI elements. The overlay is captured by GameView's m_RenderTexture
    // (OnGUI-based overlays are NOT included in the RT).
    public static class UIElementAnnotator
    {
        private const int OVERLAY_SORT_ORDER = 32767;
        private const int LABEL_FONT_SIZE = 14;
        private const float BORDER_THICKNESS = 2f;
        private const int LABEL_PADDING_H = 4;
        private const int LABEL_PADDING_V = 2;

        private static readonly Color BUTTON_COLOR = new Color(0f, 1f, 0.4f, 0.9f);
        private static readonly Color DRAGGABLE_COLOR = new Color(1f, 0.6f, 0f, 0.9f);
        private static readonly Color DROP_TARGET_COLOR = new Color(0f, 0.8f, 1f, 0.9f);
        private static readonly Color SELECTABLE_COLOR = new Color(1f, 1f, 0f, 0.9f);
        private static readonly Color LABEL_BG_COLOR = new Color(0f, 0f, 0f, 0.75f);

        public static List<UIElementInfo> CollectInteractiveElements()
        {
            List<UIElementInfo> elements = new List<UIElementInfo>();
            HashSet<GameObject> processedObjects = new HashSet<GameObject>();

            CollectSelectables(elements, processedObjects);
            CollectDragAndDropHandlers(elements, processedObjects);

            return elements;
        }

        public static GameObject CreateAnnotationOverlay(List<UIElementInfo> elements)
        {
            GameObject root = new GameObject("__UIAnnotation__");
            root.hideFlags = HideFlags.HideAndDontSave;

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = OVERLAY_SORT_ORDER;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            foreach (UIElementInfo element in elements)
            {
                CreateAnnotationForElement(root.transform, element, font);
            }

            return root;
        }

        public static void DestroyAnnotationOverlay(GameObject overlay)
        {
            if (overlay != null)
            {
                Object.DestroyImmediate(overlay);
            }
        }

        private static void CollectSelectables(List<UIElementInfo> elements, HashSet<GameObject> processedObjects)
        {
            Selectable[] selectables = Selectable.allSelectablesArray;
            foreach (Selectable selectable in selectables)
            {
                if (!selectable.IsInteractable() || !selectable.gameObject.activeInHierarchy)
                {
                    continue;
                }

                processedObjects.Add(selectable.gameObject);

                string type = ClassifySelectable(selectable);
                AddElementInfo(elements, selectable.gameObject, selectable.name, type);
            }
        }

        private static void CollectDragAndDropHandlers(List<UIElementInfo> elements, HashSet<GameObject> processedObjects)
        {
            MonoBehaviour[] allBehaviours = Object.FindObjectsOfType<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in allBehaviours)
            {
                if (!behaviour.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (processedObjects.Contains(behaviour.gameObject))
                {
                    continue;
                }

                // IDragHandler takes precedence over IDropHandler because drag interaction
                // is the primary action an AI agent would perform on the element
                if (behaviour is IDragHandler)
                {
                    processedObjects.Add(behaviour.gameObject);
                    AddElementInfo(elements, behaviour.gameObject, behaviour.name, "Draggable");
                }
                else if (behaviour is IDropHandler)
                {
                    processedObjects.Add(behaviour.gameObject);
                    AddElementInfo(elements, behaviour.gameObject, behaviour.name, "DropTarget");
                }
            }
        }

        private static string ClassifySelectable(Selectable selectable)
        {
            if (selectable is Button) return "Button";
            if (selectable is Toggle) return "Toggle";
            if (selectable is Slider) return "Slider";
            if (selectable is Dropdown) return "Dropdown";
            if (selectable is InputField) return "InputField";
            if (selectable is Scrollbar) return "Scrollbar";
            if (selectable is IDragHandler) return "Draggable";
            if (selectable is IDropHandler) return "DropTarget";
            return "Selectable";
        }

        // Reusable buffers to avoid per-element allocations in AddElementInfo → GetScreenCorners
        private static readonly Vector3[] SharedWorldCorners = new Vector3[4];
        private static readonly Vector2[] SharedScreenCorners = new Vector2[4];

        private static void AddElementInfo(List<UIElementInfo> elements, GameObject go, string name, string type)
        {
            RectTransform rectTransform = go.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                return;
            }

            Canvas canvas = go.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            if (!GetScreenCorners(rectTransform, canvas))
            {
                return;
            }

            float minX = Mathf.Min(SharedScreenCorners[0].x, SharedScreenCorners[1].x, SharedScreenCorners[2].x, SharedScreenCorners[3].x);
            float maxX = Mathf.Max(SharedScreenCorners[0].x, SharedScreenCorners[1].x, SharedScreenCorners[2].x, SharedScreenCorners[3].x);
            float minY = Mathf.Min(SharedScreenCorners[0].y, SharedScreenCorners[1].y, SharedScreenCorners[2].y, SharedScreenCorners[3].y);
            float maxY = Mathf.Max(SharedScreenCorners[0].y, SharedScreenCorners[1].y, SharedScreenCorners[2].y, SharedScreenCorners[3].y);

            float centerX = (minX + maxX) / 2f;
            float centerY = (minY + maxY) / 2f;

            // Convert from screen space (bottom-left origin) to sim coordinates (top-left origin)
            float simX = centerX;
            float simY = Screen.height - centerY;

            elements.Add(new UIElementInfo
            {
                Name = name,
                Type = type,
                SimX = simX,
                SimY = simY,
                BoundsMinX = minX,
                BoundsMinY = Screen.height - maxY,
                BoundsMaxX = maxX,
                BoundsMaxY = Screen.height - minY
            });
        }

        // Writes 4 corners into SharedScreenCorners in screen pixel coordinates (bottom-left origin).
        // For ScreenSpaceOverlay: world corners == screen pixels.
        // For Camera/WorldSpace: projects through the canvas camera.
        // Returns false when the canvas camera is unavailable for non-overlay canvases.
        private static bool GetScreenCorners(RectTransform rectTransform, Canvas canvas)
        {
            rectTransform.GetWorldCorners(SharedWorldCorners);

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                for (int i = 0; i < 4; i++)
                {
                    SharedScreenCorners[i] = new Vector2(SharedWorldCorners[i].x, SharedWorldCorners[i].y);
                }
            }
            else
            {
                // Prefer the rendering canvas's camera; fall back to root canvas, then Camera.main
                Camera cam = canvas.worldCamera;
                if (cam == null)
                {
                    Canvas rootCanvas = canvas.rootCanvas;
                    if (rootCanvas != canvas)
                    {
                        cam = rootCanvas.worldCamera;
                    }
                }

                if (cam == null)
                {
                    cam = Camera.main;
                }

                if (cam == null)
                {
                    return false;
                }

                for (int i = 0; i < 4; i++)
                {
                    SharedScreenCorners[i] = RectTransformUtility.WorldToScreenPoint(cam, SharedWorldCorners[i]);
                }
            }

            return true;
        }

        private static void CreateAnnotationForElement(Transform parent, UIElementInfo element, Font font)
        {
            // UIElementInfo stores sim coordinates (top-left origin) for the API response;
            // Canvas positioning needs screen coordinates (bottom-left origin), so invert Y back
            float screenMinX = element.BoundsMinX;
            float screenMaxX = element.BoundsMaxX;
            float screenMinY = Screen.height - element.BoundsMaxY;
            float screenMaxY = Screen.height - element.BoundsMinY;

            Color color = GetColorForType(element.Type);

            float boxWidth = screenMaxX - screenMinX;
            float boxHeight = screenMaxY - screenMinY;

            CreateBorderEdge(parent, "Top", screenMinX, screenMaxY, boxWidth, BORDER_THICKNESS, color);
            CreateBorderEdge(parent, "Bottom", screenMinX, screenMinY, boxWidth, BORDER_THICKNESS, color);
            CreateBorderEdge(parent, "Left", screenMinX, screenMinY, BORDER_THICKNESS, boxHeight, color);
            CreateBorderEdge(parent, "Right", screenMaxX - BORDER_THICKNESS, screenMinY, BORDER_THICKNESS, boxHeight, color);

            string labelText = $"{element.Name} ({element.SimX:F0},{element.SimY:F0})";
            CreateLabel(parent, labelText, screenMinX, screenMaxY + BORDER_THICKNESS, color, font);
        }

        private static void CreateBorderEdge(
            Transform parent, string name,
            float x, float y, float width, float height,
            Color color)
        {
            GameObject edgeGo = new GameObject($"Border_{name}");
            edgeGo.hideFlags = HideFlags.HideAndDontSave;
            edgeGo.transform.SetParent(parent, false);

            RectTransform rt = edgeGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, height);

            Image image = edgeGo.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static void CreateLabel(
            Transform parent, string text,
            float x, float y,
            Color textColor, Font font)
        {
            GameObject bgGo = new GameObject("LabelBg");
            bgGo.hideFlags = HideFlags.HideAndDontSave;
            bgGo.transform.SetParent(parent, false);

            RectTransform bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.zero;
            bgRt.pivot = new Vector2(0f, 0f);
            bgRt.anchoredPosition = new Vector2(x, y);

            Image bgImage = bgGo.AddComponent<Image>();
            bgImage.color = LABEL_BG_COLOR;
            bgImage.raycastTarget = false;

            ContentSizeFitter fitter = bgGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            HorizontalLayoutGroup layout = bgGo.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(LABEL_PADDING_H, LABEL_PADDING_H, LABEL_PADDING_V, LABEL_PADDING_V);
            layout.childAlignment = TextAnchor.MiddleLeft;

            GameObject textGo = new GameObject("LabelText");
            textGo.hideFlags = HideFlags.HideAndDontSave;
            textGo.transform.SetParent(bgGo.transform, false);

            textGo.AddComponent<RectTransform>();

            Text labelText = textGo.AddComponent<Text>();
            labelText.text = text;
            labelText.font = font;
            labelText.fontSize = LABEL_FONT_SIZE;
            labelText.color = textColor;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            labelText.verticalOverflow = VerticalWrapMode.Overflow;
            labelText.raycastTarget = false;
        }

        private static Color GetColorForType(string type)
        {
            return type switch
            {
                "Button" => BUTTON_COLOR,
                "Toggle" => BUTTON_COLOR,
                "Draggable" => DRAGGABLE_COLOR,
                "DropTarget" => DROP_TARGET_COLOR,
                "Slider" => DRAGGABLE_COLOR,
                "Scrollbar" => DRAGGABLE_COLOR,
                _ => SELECTABLE_COLOR
            };
        }
    }
}
