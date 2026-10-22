using System;
using System.Text.RegularExpressions;
using UnityEngine;

using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Checks and converts the background color accepted by prefab screenshots.
    /// </summary>
    internal static class PrefabBackgroundColorParser
    {
        // Accepts only #RRGGBB or #RRGGBBAA so the documented form is the whole contract;
        // ColorUtility alone would also accept color names such as "red".
        private static readonly Regex HexColor = new("^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$");

        internal static bool IsValid(string value)
        {
            return string.IsNullOrEmpty(value) || HexColor.IsMatch(value);
        }

        /// <summary>
        /// Returns the color for a value that already passed IsValid; empty means the default gray.
        /// </summary>
        internal static Color Resolve(string value)
        {
            string source = string.IsNullOrEmpty(value)
                ? UnityCliLoopConstants.SCREENSHOT_PREFAB_DEFAULT_BACKGROUND_COLOR
                : value;
            if (!HexColor.IsMatch(source))
            {
                throw new ArgumentException($"Background color must be validated before it is resolved, got: {value}", nameof(value));
            }

            ColorUtility.TryParseHtmlString(source, out Color color);
            return color;
        }
    }
}
