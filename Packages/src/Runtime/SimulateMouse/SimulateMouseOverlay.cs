#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace io.github.hatayama.uLoopMCP
{
    // Canvas-based overlay that visualizes SimulateMouse cursor position and drag state on Game View.
    // Animation is driven externally via SetCursorScale/SetAlpha from async functions in SimulateMouseTool.
    public class SimulateMouseOverlay : MonoBehaviour
    {
        public static SimulateMouseOverlay? Instance { get; private set; }

        private const int CANVAS_SORT_ORDER = 32000;

        private const float CURSOR_CIRCLE_DIAMETER = 70f;
        private const float CURSOR_CROSSHAIR_SIZE = 20f;
        private const float CURSOR_CROSSHAIR_THICKNESS = 3f;
        private const int CIRCLE_TEXTURE_SIZE = 64;

        private const float START_MARKER_SIZE = 8f;
        private const float WAYPOINT_MARKER_DIAMETER = 12f;
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
        private Text _longPressText = null!;
        private Outline _longPressOutline = null!;
        private Image _dragStartMarker = null!;
        private readonly List<Image> _pathSegments = new List<Image>();
        private readonly List<Image> _waypointMarkers = new List<Image>();

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

            _canvas.enabled = true;
            UpdateCursorPosition();
            UpdateCursorMode();
            UpdateDragPath();
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

        private void UpdateCursorMode()
        {
            bool isLongPress = SimulateMouseOverlayState.Action == MouseAction.LongPress;
            _crosshairH.enabled = !isLongPress;
            _crosshairV.enabled = !isLongPress;
            _longPressText.enabled = isLongPress;

            if (isLongPress)
            {
                _longPressText.text = SimulateMouseOverlayState.LongPressElapsed.ToString("F1") + "s";

                // Pulse the circle between 1.0x and 1.2x scale over a 2-second cycle
                float t = Mathf.PingPong(SimulateMouseOverlayState.LongPressElapsed, 2f) / 2f;
                float scale = Mathf.Lerp(1.0f, 1.2f, t);
                _circleImage.rectTransform.localScale = Vector3.one * scale;
            }
            else
            {
                _circleImage.rectTransform.localScale = Vector3.one;
            }
        }

        private void UpdateDragPath()
        {
            if (!SimulateMouseOverlayState.DragStartPosition.HasValue)
            {
                _dragStartMarker.enabled = false;
                HidePool(_pathSegments);
                HidePool(_waypointMarkers);
                // Keep a reasonable number of pooled objects for reuse across drags
                TrimPool(_pathSegments, 11);
                TrimPool(_waypointMarkers, 10);
                return;
            }

            Color activeColor = GetActiveColor();

            // Start marker
            Vector2 startScreen = SimToScreen(SimulateMouseOverlayState.DragStartPosition.Value);
            _dragStartMarker.enabled = true;
            _dragStartMarker.color = activeColor;
            _dragStartMarker.rectTransform.position = new Vector3(startScreen.x, startScreen.y, 0f);

            // Polyline: start → waypoints → current
            IReadOnlyList<Vector2> waypoints = SimulateMouseOverlayState.DragWaypoints;
            Vector2 currentScreen = SimToScreen(SimulateMouseOverlayState.CurrentPosition);

            int segmentCount = waypoints.Count + 1;
            EnsurePoolCount(_pathSegments, segmentCount, CreatePooledSegment);
            EnsurePoolCount(_waypointMarkers, waypoints.Count, CreatePooledWaypointMarker);

            // Older segments fade out — the first segment is nearly invisible, the latest is fully opaque
            Vector2 prev = startScreen;
            for (int i = 0; i < waypoints.Count; i++)
            {
                Vector2 wp = SimToScreen(waypoints[i]);
                float alpha = (float)(i + 1) / segmentCount;
                Color fadedColor = activeColor;
                fadedColor.a = activeColor.a * alpha;
                DrawSegment(_pathSegments[i], prev, wp, fadedColor);
                _waypointMarkers[i].enabled = true;
                _waypointMarkers[i].color = fadedColor;
                _waypointMarkers[i].rectTransform.position = new Vector3(wp.x, wp.y, 0f);
                prev = wp;
            }
            DrawSegment(_pathSegments[waypoints.Count], prev, currentScreen, activeColor);
            HidePoolFrom(_pathSegments, segmentCount);
            HidePoolFrom(_waypointMarkers, waypoints.Count);
        }

        private void DrawSegment(Image line, Vector2 from, Vector2 to, Color color)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;

            if (length < 1f)
            {
                line.enabled = false;
                return;
            }

            line.enabled = true;
            line.color = color;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            RectTransform rect = line.rectTransform;
            rect.position = new Vector3(from.x, from.y, 0f);
            rect.sizeDelta = new Vector2(length, LINE_THICKNESS);
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private Image CreatePooledSegment()
        {
            Image line = CreateImage("PathSegment", gameObject.transform);
            line.rectTransform.pivot = new Vector2(0f, 0.5f);
            line.rectTransform.sizeDelta = Vector2.zero;
            line.enabled = false;
            return line;
        }

        private Image CreatePooledWaypointMarker()
        {
            EnsureCircleSprite();
            Image marker = CreateImage("WaypointMarker", gameObject.transform);
            marker.rectTransform.sizeDelta = new Vector2(WAYPOINT_MARKER_DIAMETER, WAYPOINT_MARKER_DIAMETER);
            marker.sprite = _circleSprite;
            marker.enabled = false;
            return marker;
        }

        private void EnsurePoolCount(List<Image> pool, int count, System.Func<Image> factory)
        {
            while (pool.Count < count)
            {
                pool.Add(factory());
            }
        }

        private static void HidePool(List<Image> pool)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                pool[i].enabled = false;
            }
        }

        // Pools only grow via EnsurePoolCount; trim excess to prevent unbounded accumulation
        private static void TrimPool(List<Image> pool, int maxRetain)
        {
            while (pool.Count > maxRetain)
            {
                Image excess = pool[pool.Count - 1];
                pool.RemoveAt(pool.Count - 1);
                Destroy(excess.gameObject);
            }
        }

        private static void HidePoolFrom(List<Image> pool, int startIndex)
        {
            for (int i = startIndex; i < pool.Count; i++)
            {
                pool[i].enabled = false;
            }
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

            GameObject textGo = new GameObject("LongPressText");
            textGo.transform.SetParent(_cursorGroup, false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(CURSOR_CIRCLE_DIAMETER * 2f, CURSOR_CIRCLE_DIAMETER);
            _longPressText = textGo.AddComponent<Text>();
            _longPressText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _longPressText.fontSize = 28;
            _longPressText.alignment = TextAnchor.MiddleCenter;
            _longPressText.color = Color.black;
            _longPressText.raycastTarget = false;
            _longPressText.enabled = false;

            _longPressOutline = textGo.AddComponent<Outline>();
            _longPressOutline.effectColor = Color.white;
            _longPressOutline.effectDistance = new Vector2(1.5f, -1.5f);
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
