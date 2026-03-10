#nullable enable
using UnityEngine;
using UnityEngine.UI;

namespace io.github.hatayama.uLoopMCP
{
    // Canvas-based overlay that visualizes SimulateMouse cursor position and drag state on Game View.
    // Animation is driven externally via SetCursorScale/SetAlpha from async functions in SimulateMouseTool.
    public class SimulateMouseOverlay : MonoBehaviour
    {
        public static SimulateMouseOverlay? Instance { get; private set; }

        private const float OVERLAY_TIMEOUT_SECONDS = 2.0f;
        private const int CANVAS_SORT_ORDER = 32000;

        private const float CURSOR_CIRCLE_DIAMETER = 70f;
        private const float CURSOR_CROSSHAIR_SIZE = 20f;
        private const float CURSOR_CROSSHAIR_THICKNESS = 3f;
        private const int CIRCLE_TEXTURE_SIZE = 64;

        private const float START_MARKER_SIZE = 8f;
        private const float LINE_THICKNESS = 2f;

        private static readonly Color CURSOR_COLOR = new Color(1f, 1f, 1f, 0.8f);
        private static readonly Color CLICK_COLOR = new Color(0f, 1f, 0.4f, 0.9f);
        private static readonly Color DRAG_COLOR = new Color(1f, 0.6f, 0f, 0.9f);

        private Canvas _canvas = null!;
        private CanvasGroup _canvasGroup = null!;
        private RectTransform _cursorGroup = null!;
        private Image _circleImage = null!;
        private Image _crosshairH = null!;
        private Image _crosshairV = null!;
        private Image _dragLine = null!;
        private Image _dragStartMarker = null!;

        private Texture2D? _circleTexture;
        private Sprite? _circleSprite;

        private void Awake()
        {
            Debug.Assert(Instance == null, "SimulateMouseOverlay instance already exists");
            Instance = this;
            BuildCanvasHierarchy();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (_circleSprite != null)
            {
                Destroy(_circleSprite);
            }

            if (_circleTexture != null)
            {
                Destroy(_circleTexture);
            }
        }

        private void LateUpdate()
        {
            if (!SimulateMouseOverlayState.IsActive)
            {
                if (_canvas.enabled) _canvas.enabled = false;
                return;
            }

            if (SimulateMouseOverlayState.IsExpired(OVERLAY_TIMEOUT_SECONDS))
            {
                SimulateMouseOverlayState.Clear();
                if (_canvas.enabled) _canvas.enabled = false;
                return;
            }

            _canvas.enabled = true;
            UpdateCursorPosition();
            UpdateDragLine();
            UpdateDragStartMarker();
        }

        public void SetCursorScale(float scale)
        {
            _cursorGroup.localScale = Vector3.one * scale;
        }

        public void SetAlpha(float alpha)
        {
            _canvasGroup.alpha = alpha;
        }

        // sim coordinate: simX = screen pixel X, simY = EditorScreen.height - canvasY (top-left origin)
        // Canvas Screen Space Overlay position: bottom-left origin
        private static Vector2 SimToScreen(Vector2 simPos)
        {
            Vector2 srcSize = SimulateMouseOverlayState.SourceScreenSize;
            Debug.Assert(srcSize.x > 0f && srcSize.y > 0f, "SourceScreenSize must be set before SimToScreen is called");
            return new Vector2(simPos.x, srcSize.y - simPos.y);
        }

        private void UpdateCursorPosition()
        {
            Vector2 screenPos = SimToScreen(SimulateMouseOverlayState.CurrentPosition);
            _cursorGroup.position = new Vector3(screenPos.x, screenPos.y, 0f);
        }

        private void UpdateDragLine()
        {
            if (!SimulateMouseOverlayState.DragStartPosition.HasValue)
            {
                _dragLine.enabled = false;
                return;
            }

            Vector2 startScreen = SimToScreen(SimulateMouseOverlayState.DragStartPosition.Value);
            Vector2 endScreen = SimToScreen(SimulateMouseOverlayState.CurrentPosition);
            Vector2 delta = endScreen - startScreen;
            float length = delta.magnitude;

            if (length < 1f)
            {
                _dragLine.enabled = false;
                return;
            }

            _dragLine.enabled = true;
            _dragLine.color = GetActiveColor();
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            RectTransform lineRect = _dragLine.rectTransform;
            lineRect.position = new Vector3(startScreen.x, startScreen.y, 0f);
            lineRect.sizeDelta = new Vector2(length, LINE_THICKNESS);
            lineRect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void UpdateDragStartMarker()
        {
            if (!SimulateMouseOverlayState.DragStartPosition.HasValue)
            {
                _dragStartMarker.enabled = false;
                return;
            }

            _dragStartMarker.enabled = true;
            _dragStartMarker.color = GetActiveColor();
            Vector2 startScreen = SimToScreen(SimulateMouseOverlayState.DragStartPosition.Value);
            _dragStartMarker.rectTransform.position = new Vector3(startScreen.x, startScreen.y, 0f);
        }

        private Color GetActiveColor()
        {
            return SimulateMouseOverlayState.Action == MouseAction.Click
                ? CLICK_COLOR
                : DRAG_COLOR;
        }

        private void BuildCanvasHierarchy()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = CANVAS_SORT_ORDER;
            // No GraphicRaycaster — overlay must not block UI interaction behind it

            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            _dragLine = CreateImage("DragLine", gameObject.transform);
            _dragLine.rectTransform.pivot = new Vector2(0f, 0.5f);
            _dragLine.rectTransform.sizeDelta = Vector2.zero;
            _dragLine.enabled = false;

            _dragStartMarker = CreateImage("DragStartMarker", gameObject.transform);
            _dragStartMarker.rectTransform.sizeDelta = new Vector2(START_MARKER_SIZE, START_MARKER_SIZE);
            _dragStartMarker.enabled = false;

            GameObject cursorGroupGo = new GameObject("CursorGroup");
            cursorGroupGo.transform.SetParent(gameObject.transform, false);
            _cursorGroup = cursorGroupGo.AddComponent<RectTransform>();
            _cursorGroup.sizeDelta = Vector2.zero;

            _circleImage = CreateImage("Circle", _cursorGroup);
            _circleImage.rectTransform.sizeDelta = new Vector2(CURSOR_CIRCLE_DIAMETER, CURSOR_CIRCLE_DIAMETER);
            _circleImage.color = CURSOR_COLOR;
            EnsureCircleSprite();
            _circleImage.sprite = _circleSprite;

            _crosshairH = CreateImage("CrosshairH", _cursorGroup);
            _crosshairH.rectTransform.sizeDelta = new Vector2(CURSOR_CROSSHAIR_SIZE * 2f, CURSOR_CROSSHAIR_THICKNESS);
            _crosshairH.color = Color.black;

            _crosshairV = CreateImage("CrosshairV", _cursorGroup);
            _crosshairV.rectTransform.sizeDelta = new Vector2(CURSOR_CROSSHAIR_THICKNESS, CURSOR_CROSSHAIR_SIZE * 2f);
            _crosshairV.color = Color.black;
        }

        private static Image CreateImage(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            Image image = go.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private void EnsureCircleSprite()
        {
            if (_circleSprite != null)
            {
                return;
            }

            _circleTexture = new Texture2D(CIRCLE_TEXTURE_SIZE, CIRCLE_TEXTURE_SIZE, TextureFormat.RGBA32, false);
            _circleTexture.hideFlags = HideFlags.HideAndDontSave;

            float center = CIRCLE_TEXTURE_SIZE / 2f;
            float radius = center - 1f;

            for (int y = 0; y < CIRCLE_TEXTURE_SIZE; y++)
            {
                for (int x = 0; x < CIRCLE_TEXTURE_SIZE; x++)
                {
                    float dx = x + 0.5f - center;
                    float dy = y + 0.5f - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    float alpha = Mathf.Clamp01(radius - dist + 1f);
                    _circleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            _circleTexture.Apply();

            _circleSprite = Sprite.Create(
                _circleTexture,
                new Rect(0, 0, CIRCLE_TEXTURE_SIZE, CIRCLE_TEXTURE_SIZE),
                new Vector2(0.5f, 0.5f));
            _circleSprite.hideFlags = HideFlags.HideAndDontSave;
        }
    }
}
