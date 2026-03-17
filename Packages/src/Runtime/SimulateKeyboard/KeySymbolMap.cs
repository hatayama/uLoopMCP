#nullable enable
using System.Collections.Generic;

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
            { "LeftMeta", "\u2318" },        // ⌘
            { "RightMeta", "\u2318" },       // ⌘
            { "Tab", "\u21E5" },             // ⇥
            { "Escape", "Esc" },
            { "Backspace", "\u232B" },       // ⌫
            { "Delete", "\u2326" },          // ⌦
        };

        public static string GetSymbol(string keyName)
        {
            if (Symbols.TryGetValue(keyName, out string symbol))
            {
                return symbol;
            }

            return keyName;
        }
    }
}
