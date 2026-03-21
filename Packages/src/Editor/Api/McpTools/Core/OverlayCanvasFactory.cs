using UnityEngine;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    // Instantiates the InputVisualizationCanvas prefab which contains all input visualization overlays.
    internal static class OverlayCanvasFactory
    {
        private const string CANVAS_PREFAB_PATH = "Packages/io.github.hatayama.uloopmcp/Runtime/Common/InputVisualizationCanvas.prefab";

        public static void EnsureExists()
        {
            if (InputVisualizationCanvas.Instance != null)
            {
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CANVAS_PREFAB_PATH);
            Debug.Assert(prefab != null, $"InputVisualizationCanvas prefab not found at {CANVAS_PREFAB_PATH}");

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.hideFlags = HideFlags.DontSave;
        }
    }
}
