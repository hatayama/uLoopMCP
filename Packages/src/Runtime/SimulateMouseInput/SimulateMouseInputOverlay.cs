#nullable enable
using UnityEngine;
using UnityEngine.UI;

namespace io.github.hatayama.uLoopMCP
{
    // Overlay that renders a mouse device icon on Game View and highlights
    // pressed buttons / scroll direction during simulate-mouse-input tool calls.
    public class SimulateMouseInputOverlay : MonoBehaviour
    {
        public static SimulateMouseInputOverlay? Instance { get; private set; }

        private const int CANVAS_SORT_ORDER = 32000;
        private const float DISPLAY_DURATION = 1.0f;

        private const float MOUSE_WIDTH = 60f;
        private const float MOUSE_HEIGHT = 100f;
        private const float BODY_CORNER_RADIUS = 16f;
        private const int BODY_TEXTURE_SIZE = 64;
        private const float BOTTOM_MARGIN = 48f;
        private const float RIGHT_MARGIN = 48f;

        private const float BUTTON_ZONE_RATIO = 0.45f;
        private const float DIVIDER_WIDTH = 2f;

        private const float WHEEL_WIDTH = 10f;
        private const float WHEEL_HEIGHT = 20f;
        private const float WHEEL_CORNER_RADIUS = 4f;
        private const int WHEEL_TEXTURE_SIZE = 32;
        private const float WHEEL_OUTLINE_PAD = 2f;

        private const float ARROW_SIZE = 3f;
        private const float ARROW_GAP = 3f;
        private const int TRIANGLE_TEXTURE_SIZE = 16;

        private static readonly Color BODY_COLOR = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        private static readonly Color BUTTON_IDLE_COLOR = new Color(0.35f, 0.35f, 0.35f, 0.85f);
        private static readonly Color BUTTON_PRESSED_COLOR = new Color(1f, 1f, 1f, 0.95f);
        private static readonly Color DIVIDER_COLOR = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        private static readonly Color WHEEL_OUTLINE_COLOR = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        private static readonly Color ARROW_COLOR = new Color(0.1f, 0.1f, 0.1f, 1f);

        [SerializeField] private Image? _leftButton;
        [SerializeField] private Image? _rightButton;
        [SerializeField] private Image? _scrollWheel;
        [SerializeField] private Image? _scrollArrowTop;
        [SerializeField] private Image? _scrollArrowBottom;
        [SerializeField] private RectTransform? _moveDirectionGroup;

        private Canvas _canvas = null!;
        private CanvasGroup _canvasGroup = null!;
        private bool _ownsCanvas;

        private Texture2D? _bodyTexture;
        private Sprite? _bodySprite;
        private Texture2D? _wheelTexture;
        private Sprite? _wheelSprite;
        private Texture2D? _triangleTexture;
        private Sprite? _triangleSprite;

        private void Awake()
        {
            // HideAndDontSave objects from a previous PlayMode session may linger
            if (Instance != null && Instance != this)
            {
                Destroy(Instance.gameObject);
            }
            Instance = this;

            if (_leftButton != null)
            {
                ResolveCanvasFromHierarchy();
            }
            else
            {
                BuildCanvasHierarchy();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            // CreateOverlayCanvas() creates a parent Canvas with HideAndDontSave;
            // clean it up to prevent orphaned canvases accumulating across PlayMode sessions.
            if (!_ownsCanvas && _canvas != null && (_canvas.gameObject.hideFlags & HideFlags.HideAndDontSave) != 0)
            {
                Destroy(_canvas.gameObject);
            }

            DestroyIfNotNull(_triangleSprite);
            DestroyIfNotNull(_triangleTexture);
            DestroyIfNotNull(_wheelSprite);
            DestroyIfNotNull(_wheelTexture);
            DestroyIfNotNull(_bodySprite);
            DestroyIfNotNull(_bodyTexture);
        }

        private void LateUpdate()
        {
            if (SimulateMouseInputOverlayState.HasAnyActivity)
            {
                SetVisible(true);
                UpdateButtonColors();
                UpdateScrollIndicator();
                UpdateMoveDirection();
                return;
            }

            float elapsed = Time.realtimeSinceStartup - SimulateMouseInputOverlayState.LastActivityTime;

            if (SimulateMouseInputOverlayState.LastActivityTime <= 0f || elapsed > DISPLAY_DURATION)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            UpdateButtonColors();
            UpdateScrollIndicator();
            UpdateMoveDirection();
        }

        private void SetVisible(bool visible)
        {
            if (_ownsCanvas)
            {
                _canvas.enabled = visible;
                return;
            }

            _canvasGroup.alpha = visible ? 1f : 0f;
        }

        private void ResolveCanvasFromHierarchy()
        {
            _canvas = GetComponentInParent<Canvas>();
            Debug.Assert(_canvas != null, "SimulateMouseInputOverlay requires a parent Canvas when used as prefab");

            // CanvasGroup on our own GO so we can toggle visibility without affecting siblings
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            _ownsCanvas = false;
        }

        private void UpdateButtonColors()
        {
            _leftButton!.color = SimulateMouseInputOverlayState.IsLeftButtonHeld
                ? BUTTON_PRESSED_COLOR
                : BUTTON_IDLE_COLOR;

            _rightButton!.color = SimulateMouseInputOverlayState.IsRightButtonHeld
                ? BUTTON_PRESSED_COLOR
                : BUTTON_IDLE_COLOR;

            bool wheelActive = SimulateMouseInputOverlayState.IsMiddleButtonHeld
                               || SimulateMouseInputOverlayState.ScrollDirection != 0;
            _scrollWheel!.color = wheelActive ? BUTTON_PRESSED_COLOR : BUTTON_IDLE_COLOR;
        }

        private void UpdateScrollIndicator()
        {
            int dir = SimulateMouseInputOverlayState.ScrollDirection;

            if (dir == 0)
            {
                _scrollArrowTop!.enabled = false;
                _scrollArrowBottom!.enabled = false;
                return;
            }

            _scrollArrowTop!.enabled = true;
            _scrollArrowBottom!.enabled = true;

            float rotation = dir > 0 ? 0f : 180f;
            _scrollArrowTop.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            _scrollArrowBottom.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private void UpdateMoveDirection()
        {
            if (_moveDirectionGroup == null)
            {
                return;
            }

            Vector2 delta = SimulateMouseInputOverlayState.MoveDelta;

            if (delta == Vector2.zero)
            {
                _moveDirectionGroup.gameObject.SetActive(false);
                return;
            }

            _moveDirectionGroup.gameObject.SetActive(true);
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            _moveDirectionGroup.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void BuildCanvasHierarchy()
        {
            _ownsCanvas = true;

            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = CANVAS_SORT_ORDER;
            _canvas.enabled = false;

            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            GameObject containerGo = new GameObject("Container");
            containerGo.transform.SetParent(transform, false);
            RectTransform containerRect = containerGo.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(1f, 0f);
            containerRect.anchorMax = new Vector2(1f, 0f);
            containerRect.pivot = new Vector2(1f, 0f);
            containerRect.anchoredPosition = new Vector2(-RIGHT_MARGIN, BOTTOM_MARGIN);
            containerRect.sizeDelta = new Vector2(MOUSE_WIDTH, MOUSE_HEIGHT);

            EnsureMouseBodySprite();
            Image bodyImage = containerGo.AddComponent<Image>();
            bodyImage.sprite = _bodySprite;
            bodyImage.type = Image.Type.Sliced;
            bodyImage.color = BODY_COLOR;
            bodyImage.raycastTarget = false;

            Mask bodyMask = containerGo.AddComponent<Mask>();
            bodyMask.showMaskGraphic = true;

            _leftButton = CreateImage("LeftButton", containerGo.transform);
            SetAnchored(_leftButton.rectTransform, 0f, 1f - BUTTON_ZONE_RATIO, 0.5f, 1f);
            _leftButton.rectTransform.offsetMin = Vector2.zero;
            _leftButton.rectTransform.offsetMax = new Vector2(-DIVIDER_WIDTH / 2f, 0f);
            _leftButton.color = BUTTON_IDLE_COLOR;

            _rightButton = CreateImage("RightButton", containerGo.transform);
            SetAnchored(_rightButton.rectTransform, 0.5f, 1f - BUTTON_ZONE_RATIO, 1f, 1f);
            _rightButton.rectTransform.offsetMin = new Vector2(DIVIDER_WIDTH / 2f, 0f);
            _rightButton.rectTransform.offsetMax = Vector2.zero;
            _rightButton.color = BUTTON_IDLE_COLOR;

            Image divider = CreateImage("Divider", containerGo.transform);
            RectTransform divRect = divider.rectTransform;
            divRect.anchorMin = new Vector2(0.5f, 1f - BUTTON_ZONE_RATIO);
            divRect.anchorMax = new Vector2(0.5f, 1f);
            divRect.pivot = new Vector2(0.5f, 0.5f);
            divRect.sizeDelta = new Vector2(DIVIDER_WIDTH, 0f);
            divRect.offsetMin = new Vector2(-DIVIDER_WIDTH / 2f, 0f);
            divRect.offsetMax = new Vector2(DIVIDER_WIDTH / 2f, 0f);
            divider.color = DIVIDER_COLOR;

            EnsureScrollWheelSprite();
            float wheelCenterY = MOUSE_HEIGHT * (1f - BUTTON_ZONE_RATIO / 2f);
            float outlineW = WHEEL_WIDTH + WHEEL_OUTLINE_PAD * 2f;
            float outlineH = WHEEL_HEIGHT + WHEEL_OUTLINE_PAD * 2f;

            Image wheelOutline = CreateImage("WheelOutline", containerGo.transform);
            wheelOutline.sprite = _wheelSprite;
            wheelOutline.type = Image.Type.Sliced;
            RectTransform outlineRect = wheelOutline.rectTransform;
            outlineRect.anchorMin = new Vector2(0.5f, 0f);
            outlineRect.anchorMax = new Vector2(0.5f, 0f);
            outlineRect.pivot = new Vector2(0.5f, 0.5f);
            outlineRect.anchoredPosition = new Vector2(0f, wheelCenterY);
            outlineRect.sizeDelta = new Vector2(outlineW, outlineH);
            wheelOutline.color = WHEEL_OUTLINE_COLOR;

            _scrollWheel = CreateImage("ScrollWheel", containerGo.transform);
            _scrollWheel.sprite = _wheelSprite;
            _scrollWheel.type = Image.Type.Sliced;
            RectTransform wheelRect = _scrollWheel.rectTransform;
            wheelRect.anchorMin = new Vector2(0.5f, 0f);
            wheelRect.anchorMax = new Vector2(0.5f, 0f);
            wheelRect.pivot = new Vector2(0.5f, 0.5f);
            wheelRect.anchoredPosition = new Vector2(0f, wheelCenterY);
            wheelRect.sizeDelta = new Vector2(WHEEL_WIDTH, WHEEL_HEIGHT);
            _scrollWheel.color = BUTTON_IDLE_COLOR;

            EnsureTriangleSprite();

            _scrollArrowTop = CreateImage("ScrollArrowTop", containerGo.transform);
            _scrollArrowTop.sprite = _triangleSprite;
            RectTransform arrowTopRect = _scrollArrowTop.rectTransform;
            arrowTopRect.anchorMin = new Vector2(0.5f, 0f);
            arrowTopRect.anchorMax = new Vector2(0.5f, 0f);
            arrowTopRect.pivot = new Vector2(0.5f, 0.5f);
            arrowTopRect.anchoredPosition = new Vector2(0f, wheelCenterY + ARROW_GAP);
            arrowTopRect.sizeDelta = new Vector2(ARROW_SIZE * 2f, ARROW_SIZE * 2f);
            _scrollArrowTop.color = ARROW_COLOR;
            _scrollArrowTop.enabled = false;

            _scrollArrowBottom = CreateImage("ScrollArrowBottom", containerGo.transform);
            _scrollArrowBottom.sprite = _triangleSprite;
            RectTransform arrowBottomRect = _scrollArrowBottom.rectTransform;
            arrowBottomRect.anchorMin = new Vector2(0.5f, 0f);
            arrowBottomRect.anchorMax = new Vector2(0.5f, 0f);
            arrowBottomRect.pivot = new Vector2(0.5f, 0.5f);
            arrowBottomRect.anchoredPosition = new Vector2(0f, wheelCenterY - ARROW_GAP);
            arrowBottomRect.sizeDelta = new Vector2(ARROW_SIZE * 2f, ARROW_SIZE * 2f);
            _scrollArrowBottom.color = ARROW_COLOR;
            _scrollArrowBottom.enabled = false;
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

        private static void SetAnchored(RectTransform rect, float anchorMinX, float anchorMinY, float anchorMaxX, float anchorMaxY)
        {
            rect.anchorMin = new Vector2(anchorMinX, anchorMinY);
            rect.anchorMax = new Vector2(anchorMaxX, anchorMaxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void EnsureMouseBodySprite()
        {
            if (_bodySprite != null) return;
            _bodyTexture = CreateRoundedRectTexture(BODY_TEXTURE_SIZE, BODY_TEXTURE_SIZE,
                BODY_CORNER_RADIUS * (BODY_TEXTURE_SIZE / MOUSE_WIDTH));
            _bodySprite = CreateSlicedSprite(_bodyTexture);
        }

        private void EnsureScrollWheelSprite()
        {
            if (_wheelSprite != null) return;
            _wheelTexture = CreateRoundedRectTexture(WHEEL_TEXTURE_SIZE, WHEEL_TEXTURE_SIZE,
                WHEEL_CORNER_RADIUS * (WHEEL_TEXTURE_SIZE / WHEEL_WIDTH));
            _wheelSprite = CreateSlicedSprite(_wheelTexture);
        }

        private static Sprite CreateSlicedSprite(Texture2D texture)
        {
            float border = texture.width / 4f;
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private void EnsureTriangleSprite()
        {
            if (_triangleSprite != null) return;

            int size = TRIANGLE_TEXTURE_SIZE;
            _triangleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _triangleTexture.hideFlags = HideFlags.HideAndDontSave;

            float center = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f - center;
                    float py = y + 0.5f - center;
                    float halfBase = center * 0.9f;
                    float halfHeight = center * 0.8f;
                    float dist = TriangleSDF(px, py, halfBase, halfHeight);
                    float alpha = Mathf.Clamp01(-dist + 1f);

                    _triangleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            _triangleTexture.Apply();
            _triangleSprite = Sprite.Create(
                _triangleTexture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f));
            _triangleSprite.hideFlags = HideFlags.HideAndDontSave;
        }

        private static float TriangleSDF(float px, float py, float halfBase, float halfHeight)
        {
            float ax = Mathf.Abs(px);
            float edgeX = halfBase;
            float edgeY = halfHeight * 2f;
            float edgeLen = Mathf.Sqrt(edgeX * edgeX + edgeY * edgeY);
            float dSlant = (ax * edgeY - (py - halfHeight) * (-edgeX)) / edgeLen;
            float dBottom = -(py + halfHeight);

            return Mathf.Max(dSlant, dBottom);
        }

        private static Texture2D CreateRoundedRectTexture(int width, int height, float radius)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;

                    float dx = Mathf.Max(radius - px, px - (width - radius), 0f);
                    float dy = Mathf.Max(radius - py, py - (height - radius), 0f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - dist + 1f);

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return texture;
        }

        private static void DestroyIfNotNull(Object? obj)
        {
            if (obj != null) Destroy(obj);
        }
    }
}
