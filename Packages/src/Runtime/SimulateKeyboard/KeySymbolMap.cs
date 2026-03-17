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

            if (keyName == "LeftCtrl" || keyName == "RightCtrl")
            {
                return IsMac ? "\u2303" : "Ctrl"; // ⌃ or Ctrl
            }

            if (keyName == "LeftAlt" || keyName == "RightAlt")
            {
                return IsMac ? "\u2325" : "Alt"; // ⌥ or Alt
            }

            if (Symbols.TryGetValue(keyName, out string symbol))
            {
                return symbol;
            }

            return keyName;
        }

        // Single non-ASCII characters render smaller than text in most fonts
        public static bool IsGlyphSymbol(string symbol)
        {
            return symbol.Length == 1 && symbol[0] > 127;
        }
    }
}
