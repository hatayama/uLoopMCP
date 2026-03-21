using UnityEngine;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    // Manages a single shared Canvas for all input visualization overlays
    // and instantiates overlay prefabs as children of it.
    internal static class OverlayCanvasFactory
    {
        private const string CANVAS_PREFAB_PATH = "Packages/io.github.hatayama.uloopmcp/Runtime/Common/InputVisualizationCanvas.prefab";

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

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CANVAS_PREFAB_PATH);
            Debug.Assert(prefab != null, $"InputVisualizationCanvas prefab not found at {CANVAS_PREFAB_PATH}");

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.hideFlags = HideFlags.DontSave;

            _sharedCanvas = instance.GetComponent<Canvas>();
            Debug.Assert(_sharedCanvas != null, "InputVisualizationCanvas prefab must have a Canvas component");
        }
    }
}
