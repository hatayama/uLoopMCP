using UnityEngine;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    // Instantiates the InputVisualizationCanvas prefab and manages its lifecycle.
    [InitializeOnLoad]
    internal static class OverlayCanvasFactory
    {
        private const string CANVAS_PREFAB_PATH = "Packages/io.github.hatayama.uloopmcp/Runtime/Common/InputVisualizationCanvas.prefab";

        private static InputVisualizationCanvas _instance;

        public static InputVisualizationCanvas VisualizationCanvas
        {
            get
            {
                EnsureExists();
                Debug.Assert(_instance != null, "InputVisualizationCanvas instance must exist after EnsureExists");
                return _instance!;
            }
        }

        static OverlayCanvasFactory()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static void EnsureExists()
        {
            if (_instance != null)
            {
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CANVAS_PREFAB_PATH);
            Debug.Assert(prefab != null, $"InputVisualizationCanvas prefab not found at {CANVAS_PREFAB_PATH}");

            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Object.DontDestroyOnLoad(go);
            _instance = go.GetComponent<InputVisualizationCanvas>();
            Debug.Assert(_instance != null, "InputVisualizationCanvas component not found on prefab");
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingPlayMode)
            {
                return;
            }

            if (_instance != null)
            {
                Object.DestroyImmediate(_instance.gameObject);
            }
            _instance = null;
        }
    }
}
