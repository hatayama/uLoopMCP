#nullable enable
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    public static class SimulateMouseOverlayState
    {
        public static bool IsActive { get; private set; }
        public static MouseAction Action { get; private set; }
        public static Vector2 CurrentPosition { get; private set; }
        public static Vector2? DragStartPosition { get; private set; }
        public static string? HitGameObjectName { get; private set; }
        public static float LastUpdateTime { get; private set; }

        // Screen.width/height at the time positions were recorded (Editor context may differ from Game context)
        public static Vector2 SourceScreenSize { get; private set; }

        public static void Update(
            MouseAction action,
            Vector2 currentPosition,
            Vector2? dragStartPosition,
            string? hitGameObjectName)
        {
            IsActive = true;
            Action = action;
            CurrentPosition = currentPosition;
            DragStartPosition = dragStartPosition;
            HitGameObjectName = hitGameObjectName;
            SourceScreenSize = new Vector2(Screen.width, Screen.height);
            LastUpdateTime = Time.realtimeSinceStartup;
        }

        public static void UpdatePosition(Vector2 position)
        {
            CurrentPosition = position;
            LastUpdateTime = Time.realtimeSinceStartup;
        }

        public static void Clear()
        {
            IsActive = false;
            Action = default;
            CurrentPosition = Vector2.zero;
            DragStartPosition = null;
            HitGameObjectName = null;
            SourceScreenSize = Vector2.zero;
            LastUpdateTime = 0f;
        }

        public static bool IsExpired(float timeoutSeconds)
        {
            return Time.realtimeSinceStartup - LastUpdateTime > timeoutSeconds;
        }
    }
}
