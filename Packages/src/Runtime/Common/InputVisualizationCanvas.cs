#nullable enable
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    // Orchestrator that owns the shared Canvas and holds references to all input visualization overlays.
    public class InputVisualizationCanvas : MonoBehaviour
    {
        public static InputVisualizationCanvas? Instance { get; private set; }

        [SerializeField] private SimulateKeyboardOverlay _keyboardOverlay = null!;
        [SerializeField] private SimulateMouseUiOverlay _mouseUiOverlay = null!;
        [SerializeField] private SimulateMouseInputOverlay _mouseInputOverlay = null!;

        public SimulateKeyboardOverlay KeyboardOverlay => _keyboardOverlay;
        public SimulateMouseUiOverlay MouseUiOverlay => _mouseUiOverlay;
        public SimulateMouseInputOverlay MouseInputOverlay => _mouseInputOverlay;

        private void Awake()
        {
            Debug.Assert(Instance == null, "InputVisualizationCanvas instance already exists");
            Instance = this;

            Debug.Assert(_keyboardOverlay != null, "_keyboardOverlay must be assigned in prefab");
            Debug.Assert(_mouseUiOverlay != null, "_mouseUiOverlay must be assigned in prefab");
            Debug.Assert(_mouseInputOverlay != null, "_mouseInputOverlay must be assigned in prefab");
        }

        public static void DestroyAll()
        {
            if (Instance == null)
            {
                return;
            }

            DestroyImmediate(Instance.gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
