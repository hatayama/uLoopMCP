using UnityEngine;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    // Manages a single shared Canvas prefab that contains all input visualization overlays.
    internal static class OverlayCanvasFactory
    {
        private const string CANVAS_PREFAB_PATH = "Packages/io.github.hatayama.uloopmcp/Runtime/Common/InputVisualizationCanvas.prefab";

        private static GameObject _instance;

        public static void EnsureExists()
        {
            if (_instance != null)
            {
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CANVAS_PREFAB_PATH);
            Debug.Assert(prefab != null, $"InputVisualizationCanvas prefab not found at {CANVAS_PREFAB_PATH}");

            _instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            _instance.hideFlags = HideFlags.DontSave;
        }
    }
}
