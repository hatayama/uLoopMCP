#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace io.github.hatayama.uLoopMCP
{
    public class SimulateKeyboardOverlay : MonoBehaviour
    {
        public static SimulateKeyboardOverlay? Instance { get; private set; }

        private const float PRESS_DISPLAY_DURATION = 0.5f;
        private const float FADE_DURATION = 0.2f;
        private const float BADGE_WIDTH = 80f;
        private const float BADGE_HEIGHT = 36f;
        private const float BADGE_SPACING = 8f;
        private const float MARGIN = 16f;
        private static readonly Color BadgeBackgroundColor = new Color(0f, 0f, 0f, 0.7f);

        private Canvas _canvas = null!;
        private readonly List<BadgeEntry> _badgePool = new();
        private readonly List<string> _displayKeys = new();

        private void Awake()
        {
            Debug.Assert(Instance == null, "SimulateKeyboardOverlay instance already exists");
            Instance = this;

            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32000;

            gameObject.AddComponent<CanvasScaler>();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void LateUpdate()
        {
            bool hasHeldKeys = SimulateKeyboardOverlayState.HeldKeys.Count > 0;
            string? pressKey = SimulateKeyboardOverlayState.PressKey;
            float pressElapsed = 0f;

            if (pressKey != null)
            {
                pressElapsed = Time.realtimeSinceStartup - SimulateKeyboardOverlayState.PressStartTime;
                if (pressElapsed > PRESS_DISPLAY_DURATION + FADE_DURATION)
                {
                    SimulateKeyboardOverlayState.ClearPress();
                    pressKey = null;
                }
            }

            if (!hasHeldKeys && pressKey == null)
            {
                SetBadgeCount(0);
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

            for (int i = 0; i < _displayKeys.Count; i++)
            {
                float alpha = GetBadgeAlpha(_displayKeys[i], pressKey, pressElapsed);
                UpdateBadge(_badgePool[i], _displayKeys[i], i, alpha);
            }
        }

        private void SetBadgeCount(int count)
        {
            while (_badgePool.Count < count)
            {
                _badgePool.Add(CreateBadge());
            }

            for (int i = 0; i < _badgePool.Count; i++)
            {
                _badgePool[i].Root.SetActive(i < count);
            }
        }

        private BadgeEntry CreateBadge()
        {
            GameObject badge = new GameObject("KeyBadge");
            badge.transform.SetParent(_canvas.transform, false);

            RectTransform rect = badge.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(BADGE_WIDTH, BADGE_HEIGHT);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;

            Image bg = badge.AddComponent<Image>();
            bg.color = BadgeBackgroundColor;

            GameObject textGo = new GameObject("KeyText");
            textGo.transform.SetParent(badge.transform, false);

            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;

            return new BadgeEntry(badge, rect, bg, text);
        }

        private void UpdateBadge(BadgeEntry badge, string keyName, int index, float alpha)
        {
            float xPos = MARGIN + index * (BADGE_WIDTH + BADGE_SPACING);
            badge.Rect.anchoredPosition = new Vector2(xPos, MARGIN);
            badge.Background.color = new Color(
                BadgeBackgroundColor.r,
                BadgeBackgroundColor.g,
                BadgeBackgroundColor.b,
                BadgeBackgroundColor.a * alpha);
            badge.Text.color = new Color(Color.white.r, Color.white.g, Color.white.b, alpha);
            badge.Text.text = keyName;
        }

        private static float GetBadgeAlpha(string keyName, string? pressKey, float pressElapsed)
        {
            if (pressKey == null || keyName != pressKey || pressElapsed <= PRESS_DISPLAY_DURATION)
            {
                return 1f;
            }

            float fadeT = Mathf.Clamp01((pressElapsed - PRESS_DISPLAY_DURATION) / FADE_DURATION);
            return 1f - fadeT;
        }

        private readonly struct BadgeEntry
        {
            public readonly GameObject Root;
            public readonly RectTransform Rect;
            public readonly Image Background;
            public readonly Text Text;

            public BadgeEntry(GameObject root, RectTransform rect, Image background, Text text)
            {
                Root = root;
                Rect = rect;
                Background = background;
                Text = text;
            }
        }
    }
}
