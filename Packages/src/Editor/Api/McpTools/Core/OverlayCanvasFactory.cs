using UnityEngine;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    // Instantiates the InputVisualizationCanvas prefab and manages its lifecycle.
    [InitializeOnLoad]
    internal static class OverlayCanvasFactory
    {
        private const string CANVAS_PREFAB_PATH = "Packages/io.github.hatayama.uloopmcp/Runtime/Common/InputVisualizationCanvas.prefab";

        static OverlayCanvasFactory()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

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

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                InputVisualizationCanvas.DestroyAll();
            }
        }
    }
}
