#nullable enable
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    // Shared utilities for overlay MonoBehaviours instantiated with HideFlags.DontSave.
    public static class OverlayHelper
    {
        public const int CANVAS_SORT_ORDER = 32000;

        // DontSave objects are not automatically destroyed when PlayMode exits,
        // so this must be called explicitly from an ExitingPlayMode callback.
        public static void DestroyOverlay(GameObject overlayRoot)
        {
            Object.DestroyImmediate(overlayRoot);
        }
    }
}
