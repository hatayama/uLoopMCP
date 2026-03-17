#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    public static class KeySymbolMap
    {
        private static readonly Dictionary<string, string> Symbols = new()
        {
            { "Space", "\u2423" },           // ␣
            { "LeftShift", "\u21E7" },       // ⇧
            { "RightShift", "\u21E7" },      // ⇧
            { "Enter", "\u23CE" },           // ⏎
            { "UpArrow", "\u2191" },         // ↑
            { "DownArrow", "\u2193" },       // ↓
            { "LeftArrow", "\u2190" },       // ←
            { "RightArrow", "\u2192" },      // →
            { "LeftCtrl", "Ctrl" },
            { "RightCtrl", "Ctrl" },
            { "LeftAlt", "Alt" },
            { "RightAlt", "Alt" },
            { "Tab", "\u21E5" },             // ⇥
            { "Escape", "Esc" },
            { "Backspace", "\u232B" },       // ⌫
            { "Delete", "\u2326" },          // ⌦
            { "LeftWindows", "\u229E" },     // ⊞
            { "RightWindows", "\u229E" },    // ⊞
            { "ContextMenu", "Menu" },
            { "PrintScreen", "PrtSc" },
            { "ScrollLock", "ScrLk" },
        };

        // Meta key maps to ⌘ on macOS, ⊞ on Windows/Linux
        private static bool IsMac =>
            Application.platform == RuntimePlatform.OSXEditor ||
            Application.platform == RuntimePlatform.OSXPlayer;

        public static string GetSymbol(string keyName)
        {
            if (keyName == "LeftMeta" || keyName == "RightMeta")
            {
                return IsMac ? "\u2318" : "\u229E"; // ⌘ or ⊞
            }

            if (Symbols.TryGetValue(keyName, out string symbol))
            {
                return symbol;
            }

            return keyName;
        }
    }
}
