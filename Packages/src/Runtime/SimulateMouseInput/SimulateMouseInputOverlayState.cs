#nullable enable
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    public static class SimulateMouseInputOverlayState
    {
        public static bool IsLeftButtonHeld { get; private set; }
        public static bool IsRightButtonHeld { get; private set; }
        public static bool IsMiddleButtonHeld { get; private set; }

        // 1 = up, -1 = down, 0 = none
        public static int ScrollDirection { get; private set; }

        public static float LastActivityTime { get; private set; }

        public static bool HasAnyActivity =>
            IsLeftButtonHeld || IsRightButtonHeld || IsMiddleButtonHeld || ScrollDirection != 0;

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
            ScrollDirection = direction;
            LastActivityTime = Time.realtimeSinceStartup;
        }

        public static void ClearScroll()
        {
            ScrollDirection = 0;
        }

        public static void Clear()
        {
            IsLeftButtonHeld = false;
            IsRightButtonHeld = false;
            IsMiddleButtonHeld = false;
            ScrollDirection = 0;
            LastActivityTime = 0f;
        }
    }
}
