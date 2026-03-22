using UnityEditor;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    // Dynamically instantiates Record/Replay overlay prefabs as children of the
    // shared InputVisualizationCanvas. The overlay prefabs were removed from the
    // package-side canvas so they need to be loaded on demand from Assets/.
    internal static class RecordReplayOverlayFactory
    {
        private const string RECORD_OVERLAY_RELATIVE_PATH = "Runtime/RecordInput/RecordInputOverlay.prefab";
        private const string REPLAY_OVERLAY_RELATIVE_PATH = "Runtime/ReplayInput/ReplayInputOverlay.prefab";

        private static RecordInputOverlayPresenter _recordPresenter;
        private static ReplayInputOverlay _replayOverlay;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticFields()
        {
            _recordPresenter = null;
            _replayOverlay = null;
        }

        public static void EnsureRecordOverlay()
        {
            if (_recordPresenter != null)
            {
                return;
            }

            _recordPresenter = Object.FindAnyObjectByType<RecordInputOverlayPresenter>();
            if (_recordPresenter != null)
            {
                return;
            }

            InputVisualizationCanvas canvas = OverlayCanvasFactory.VisualizationCanvas;
            string recordPath = $"{McpConstants.PackageAssetPath}/{RECORD_OVERLAY_RELATIVE_PATH}";
            _recordPresenter = InstantiateOverlayChild<RecordInputOverlayPresenter>(
                recordPath, canvas.transform);
        }

        public static void EnsureReplayOverlay()
        {
            if (_replayOverlay != null)
            {
                return;
            }

            _replayOverlay = Object.FindAnyObjectByType<ReplayInputOverlay>();
            if (_replayOverlay != null)
            {
                return;
            }

            InputVisualizationCanvas canvas = OverlayCanvasFactory.VisualizationCanvas;
            string replayPath = $"{McpConstants.PackageAssetPath}/{REPLAY_OVERLAY_RELATIVE_PATH}";
            _replayOverlay = InstantiateOverlayChild<ReplayInputOverlay>(
                replayPath, canvas.transform);
        }

        private static T InstantiateOverlayChild<T>(string prefabPath, Transform parent) where T : Component
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Debug.Assert(prefab != null, $"Overlay prefab not found at {prefabPath}");

            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            T component = go.GetComponent<T>();
            Debug.Assert(component != null, $"Component {typeof(T).Name} not found on prefab at {prefabPath}");
            return component;
        }
    }
}
