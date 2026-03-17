#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace io.github.hatayama.uLoopMCP
{
    public class SimulateKeyboardOverlay : MonoBehaviour
    {
        public static SimulateKeyboardOverlay? Instance { get; private set; }

        public const float CONTAINER_BACKGROUND_ALPHA = 0.85f;

        private const int CANVAS_SORT_ORDER = 32000;
        private const float PRESS_DISPLAY_DURATION = 0.5f;
        private const float FADE_DURATION = 0.2f;
        private const float BOTTOM_MARGIN = 48f;
        private const float CONTAINER_PADDING_H = 24f;
        private const float CONTAINER_PADDING_V = 16f;
        private const float KEY_SPACING = 8f;
        private const int FONT_SIZE = 48;
        private const int GLYPH_FONT_SIZE = 64;
        private const int CORNER_RADIUS = 16;
        private const int ROUNDED_RECT_TEXTURE_SIZE = 64;
        private static readonly Color ContainerBackgroundColor = new Color(0.15f, 0.15f, 0.15f, CONTAINER_BACKGROUND_ALPHA);

        private Canvas _canvas = null!;
        private GameObject _container = null!;
        private RectTransform _containerRect = null!;
        private Image _containerImage = null!;
        private readonly List<BadgeEntry> _badgePool = new();
        private readonly List<string> _cachedKeyNames = new();
        private readonly List<string> _displayKeys = new();

        private Texture2D? _roundedRectTexture;
        private Sprite? _roundedRectSprite;

        private void Awake()
        {
            Debug.Assert(Instance == null, "SimulateKeyboardOverlay instance already exists");
            Instance = this;

            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = CANVAS_SORT_ORDER;

            gameObject.AddComponent<CanvasScaler>();

            CanvasGroup canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            BuildContainer();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (_roundedRectSprite != null)
            {
                Destroy(_roundedRectSprite);
            }

            if (_roundedRectTexture != null)
            {
                Destroy(_roundedRectTexture);
            }
        }

        private void LateUpdate()
        {
            bool hasHeldKeys = SimulateKeyboardOverlayState.HeldKeys.Count > 0;
            string? pressKey = SimulateKeyboardOverlayState.PressKey;
            bool isPressHeld = SimulateKeyboardOverlayState.IsPressHeld;
            float pressElapsedSinceRelease = 0f;

            if (pressKey != null && !isPressHeld)
            {
                pressElapsedSinceRelease = Time.realtimeSinceStartup - SimulateKeyboardOverlayState.PressReleasedTime;
                if (pressElapsedSinceRelease > PRESS_DISPLAY_DURATION + FADE_DURATION)
                {
                    SimulateKeyboardOverlayState.ClearPress();
                    pressKey = null;
                }
            }

            if (!hasHeldKeys && pressKey == null)
            {
                SetBadgeCount(0);
                _container.SetActive(false);
                return;
            }

            _displayKeys.Clear();
            IReadOnlyList<string> heldKeys = SimulateKeyboardOverlayState.HeldKeys;
            for (int i = 0; i < heldKeys.Count; i++)
            {
                _displayKeys.Add(heldKeys[i]);
            }

            if (pressKey != null && !_displayKeys.Contains(pressKey))
            {
                _displayKeys.Add(pressKey);
            }

            SetBadgeCount(_displayKeys.Count);
            _container.SetActive(true);

            float maxAlpha = 0f;
            for (int i = 0; i < _displayKeys.Count; i++)
            {
                float alpha = GetBadgeAlpha(_displayKeys[i], pressKey, isPressHeld, pressElapsedSinceRelease);
                UpdateBadge(_badgePool[i], i, _displayKeys[i], alpha);
                if (alpha > maxAlpha)
                {
                    maxAlpha = alpha;
                }
            }

            _containerImage.color = new Color(
                ContainerBackgroundColor.r,
                ContainerBackgroundColor.g,
                ContainerBackgroundColor.b,
                ContainerBackgroundColor.a * maxAlpha);
        }

        private void BuildContainer()
        {
            EnsureRoundedRectSprite();

            _container = new GameObject("Container");
            _container.transform.SetParent(_canvas.transform, false);

            _containerRect = _container.AddComponent<RectTransform>();
            _containerRect.anchorMin = new Vector2(0.5f, 0f);
            _containerRect.anchorMax = new Vector2(0.5f, 0f);
            _containerRect.pivot = new Vector2(0.5f, 0f);
            _containerRect.anchoredPosition = new Vector2(0f, BOTTOM_MARGIN);

            _containerImage = _container.AddComponent<Image>();
            _containerImage.sprite = _roundedRectSprite;
            _containerImage.type = Image.Type.Sliced;
            _containerImage.color = ContainerBackgroundColor;
            _containerImage.raycastTarget = false;

            HorizontalLayoutGroup layout = _container.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = KEY_SPACING;
            layout.padding = new RectOffset(
                (int)CONTAINER_PADDING_H, (int)CONTAINER_PADDING_H,
                (int)CONTAINER_PADDING_V, (int)CONTAINER_PADDING_V);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = _container.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _container.SetActive(false);
        }

        private void SetBadgeCount(int count)
        {
            while (_badgePool.Count < count)
            {
                _badgePool.Add(CreateBadge());
                _cachedKeyNames.Add("");
            }

            for (int i = 0; i < _badgePool.Count; i++)
            {
                _badgePool[i].Root.SetActive(i < count);
            }
        }

        private BadgeEntry CreateBadge()
        {
            GameObject badge = new GameObject("KeyBadge");
            badge.transform.SetParent(_container.transform, false);

            badge.AddComponent<RectTransform>();

            Image bg = badge.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0f);
            bg.raycastTarget = false;

            LayoutElement layoutElement = badge.AddComponent<LayoutElement>();

            GameObject textGo = new GameObject("KeyText");
            textGo.transform.SetParent(badge.transform, false);

            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = FONT_SIZE;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.raycastTarget = false;

            // Thicken glyph strokes so Unicode symbols don't look too thin
            Outline outline = textGo.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(1f, -1f);

            return new BadgeEntry(badge, bg, text, layoutElement);
        }

        private void UpdateBadge(BadgeEntry badge, int index, string keyName, float alpha)
        {
            badge.Text.color = new Color(1f, 1f, 1f, alpha);

            if (_cachedKeyNames[index] == keyName)
            {
                return;
            }

            _cachedKeyNames[index] = keyName;
            string symbol = KeySymbolMap.GetSymbol(keyName);
            badge.Text.text = symbol;
            badge.Text.fontSize = KeySymbolMap.IsGlyphSymbol(symbol) ? GLYPH_FONT_SIZE : FONT_SIZE;

            TextGenerationSettings settings = badge.Text.GetGenerationSettings(new Vector2(float.MaxValue, float.MaxValue));
            float preferredWidth = badge.Text.cachedTextGeneratorForLayout.GetPreferredWidth(symbol, settings);
            float preferredHeight = badge.Text.cachedTextGeneratorForLayout.GetPreferredHeight(symbol, settings);

            badge.Layout.preferredWidth = Mathf.Max(preferredWidth / badge.Text.pixelsPerUnit, preferredHeight / badge.Text.pixelsPerUnit);
            badge.Layout.preferredHeight = preferredHeight / badge.Text.pixelsPerUnit;
        }

        private static float GetBadgeAlpha(
            string keyName,
            string? pressKey,
            bool isPressHeld,
            float pressElapsedSinceRelease)
        {
            if (pressKey == null || keyName != pressKey || isPressHeld || pressElapsedSinceRelease <= PRESS_DISPLAY_DURATION)
            {
                return 1f;
            }

            float fadeT = Mathf.Clamp01((pressElapsedSinceRelease - PRESS_DISPLAY_DURATION) / FADE_DURATION);
            return 1f - fadeT;
        }

        private void EnsureRoundedRectSprite()
        {
            if (_roundedRectSprite != null)
            {
                return;
            }

            int size = ROUNDED_RECT_TEXTURE_SIZE;
            _roundedRectTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _roundedRectTexture.hideFlags = HideFlags.HideAndDontSave;

            float radius = CORNER_RADIUS * (size / 64f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;

                    // SDF for rounded rectangle
                    float dx = Mathf.Max(radius - px, px - (size - radius), 0f);
                    float dy = Mathf.Max(radius - py, py - (size - radius), 0f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    float alpha = Mathf.Clamp01(radius - dist + 1f);
                    _roundedRectTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            _roundedRectTexture.Apply();

            float border = radius;
            _roundedRectSprite = Sprite.Create(
                _roundedRectTexture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            _roundedRectSprite.hideFlags = HideFlags.HideAndDontSave;
        }

        private readonly struct BadgeEntry
        {
            public readonly GameObject Root;
            public readonly Image Background;
            public readonly Text Text;
            public readonly LayoutElement Layout;

            public BadgeEntry(GameObject root, Image background, Text text, LayoutElement layout)
            {
                Root = root;
                Background = background;
                Text = text;
                Layout = layout;
            }
        }
    }
}
