using UnityEngine;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    // Manages a single shared Canvas for all input visualization overlays
    // and instantiates overlay prefabs as children of it.
    internal static class OverlayCanvasFactory
    {
        private static Canvas _sharedCanvas;

        public static void EnsureOverlayExists(MonoBehaviour instance, string prefabPath)
        {
            if (instance != null)
            {
                return;
            }

            EnsureSharedCanvas();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Debug.Assert(prefab != null, $"Overlay prefab not found at {prefabPath}");

            GameObject overlayInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _sharedCanvas.transform);
            overlayInstance.hideFlags = HideFlags.DontSave;
        }

        private static void EnsureSharedCanvas()
        {
            if (_sharedCanvas != null)
            {
                return;
            }

            GameObject canvasGo = new GameObject("InputVisualizationCanvas");
            canvasGo.hideFlags = HideFlags.DontSave;

            _sharedCanvas = canvasGo.AddComponent<Canvas>();
            _sharedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _sharedCanvas.sortingOrder = OverlayHelper.CANVAS_SORT_ORDER;
        }
    }
}
