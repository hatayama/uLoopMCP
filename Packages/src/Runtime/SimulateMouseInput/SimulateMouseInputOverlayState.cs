#nullable enable
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    public static class SimulateMouseInputOverlayState
    {
        public static bool IsLeftButtonHeld { get; private set; }
        public static bool IsRightButtonHeld { get; private set; }
        public static bool IsMiddleButtonHeld { get; private set; }

        private const float SCROLL_DISPLAY_DURATION = 0.05f;
        private static int _scrollDirection;
        private static float _scrollActiveUntil;

        public static int ScrollDirection =>
            _scrollDirection != 0 && Time.realtimeSinceStartup < _scrollActiveUntil
                ? _scrollDirection
                : 0;

        private const float MOVE_DISPLAY_DURATION = 0.3f;
        private const int MOVE_SAMPLE_FRAMES = 5;
        private static Vector2 _moveDelta;
        private static Vector2 _moveAccumulator;
        private static int _moveFrameCount;
        private static float _moveActiveUntil;

        public static Vector2 MoveDelta =>
            _moveDelta != Vector2.zero && Time.realtimeSinceStartup < _moveActiveUntil
                ? _moveDelta
                : Vector2.zero;

        public static float LastActivityTime { get; private set; }

        public static bool HasAnyActivity =>
            IsLeftButtonHeld || IsRightButtonHeld || IsMiddleButtonHeld
            || ScrollDirection != 0 || MoveDelta != Vector2.zero;

        public static void SetButtonHeld(MouseButton button, bool held)
        {
            switch (button)
            {
                case MouseButton.Left:
                    IsLeftButtonHeld = held;
                    break;
                case MouseButton.Right:
                    IsRightButtonHeld = held;
                    break;
                case MouseButton.Middle:
                    IsMiddleButtonHeld = held;
                    break;
                default:
                    Debug.Assert(false, $"Unexpected MouseButton value: {button}");
                    break;
            }

            LastActivityTime = Time.realtimeSinceStartup;
        }

        public static void SetScrollDirection(int direction)
        {
            Debug.Assert(direction >= -1 && direction <= 1, $"direction must be -1, 0, or 1, got: {direction}");
            _scrollDirection = direction;
            _scrollActiveUntil = Time.realtimeSinceStartup + SCROLL_DISPLAY_DURATION;
            LastActivityTime = Time.realtimeSinceStartup;
        }

        public static void ClearScroll()
        {
            _scrollActiveUntil = 0f;
        }

        public static void SetMoveDelta(Vector2 delta)
        {
            _moveAccumulator += delta;
            _moveFrameCount++;

            // Update direction every frame from accumulated delta so single-call
            // MoveDelta actions are visible, while the accumulator reset every N
            // frames smooths out per-frame integer quantization noise.
            if (_moveAccumulator != Vector2.zero)
            {
                _moveDelta = _moveAccumulator.normalized;
            }

            if (_moveFrameCount >= MOVE_SAMPLE_FRAMES)
            {
                _moveAccumulator = Vector2.zero;
                _moveFrameCount = 0;
            }

            _moveActiveUntil = Time.realtimeSinceStartup + MOVE_DISPLAY_DURATION;
            LastActivityTime = Time.realtimeSinceStartup;
        }

        public static void Clear()
        {
            IsLeftButtonHeld = false;
            IsRightButtonHeld = false;
            IsMiddleButtonHeld = false;
            _scrollDirection = 0;
            _scrollActiveUntil = 0f;
            _moveDelta = Vector2.zero;
            _moveAccumulator = Vector2.zero;
            _moveFrameCount = 0;
            _moveActiveUntil = 0f;
            LastActivityTime = 0f;
        }
    }
}
