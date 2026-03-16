#nullable enable
using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP
{
    public static class SimulateKeyboardOverlayState
    {
        private static readonly List<string> _heldKeys = new();
        private static string? _pressKey;
        private static float _pressStartTime;

        public static bool IsActive => _heldKeys.Count > 0 || _pressKey != null;
        public static IReadOnlyList<string> HeldKeys => _heldKeys;
        public static string? PressKey => _pressKey;
        public static float PressStartTime => _pressStartTime;

        public static void AddHeldKey(string keyName)
        {
            if (!_heldKeys.Contains(keyName))
            {
                _heldKeys.Add(keyName);
            }
        }

        public static void RemoveHeldKey(string keyName)
        {
            _heldKeys.Remove(keyName);
        }

        public static void ShowPress(string keyName)
        {
            _pressKey = keyName;
            _pressStartTime = UnityEngine.Time.realtimeSinceStartup;
        }

        public static void ClearPress()
        {
            _pressKey = null;
        }

        public static void Clear()
        {
            _heldKeys.Clear();
            _pressKey = null;
        }
    }
}
