#if ULOOPMCP_HAS_INPUT_SYSTEM
#nullable enable
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace io.github.hatayama.uLoopMCP
{
    public static class InputReplayVerificationSceneBuilder
    {
        private const string SCENE_PATH = "Assets/Scenes/InputReplayVerificationScene.unity";

        [MenuItem("uLoopMCP/Build InputReplay Verification Scene")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera();
            CreateEventSystem();
            GameObject cube = CreateCube();
            CreateUI(cube);

            bool saved = EditorSceneManager.SaveScene(scene, SCENE_PATH);
            if (!saved)
            {
                Debug.Assert(false, $"[InputReplayVerificationSceneBuilder] Failed to save scene to {SCENE_PATH}");
                return;
            }
            Debug.Log($"[InputReplayVerificationSceneBuilder] Scene saved to {SCENE_PATH}");
        }

        private static void CreateCamera()
        {
            GameObject cameraGo = new GameObject("Main Camera");
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 1f);
            cameraGo.transform.position = new Vector3(0f, 5f, -8f);
            cameraGo.transform.eulerAngles = new Vector3(30f, 0f, 0f);
        }

        private static void CreateEventSystem()
        {
            GameObject eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<InputSystemUIInputModule>();
        }

        private static GameObject CreateCube()
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "VerificationCube";
            cube.transform.position = Vector3.zero;
            cube.transform.localScale = Vector3.one;

            // Physics would make the scene non-deterministic
            Collider? collider = cube.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            Rigidbody? rb = cube.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Object.DestroyImmediate(rb);
            }

            return cube;
        }

        private static void CreateUI(GameObject cube)
        {
            GameObject canvasGo = new GameObject("Canvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // Title
            CreateText(canvasGo.transform, "Title",
                "Input Replay Verification Scene",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -20f), new Vector2(600f, 40f),
                28, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

            // Info panel (top-left)
            float yOffset = -20f;
            float lineHeight = 30f;

            Text frameText = CreateText(canvasGo.transform, "FrameText",
                "Frame: 0",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(20f, yOffset), new Vector2(400f, lineHeight),
                20, FontStyle.Normal, Color.green, TextAnchor.MiddleLeft);

            Text positionText = CreateText(canvasGo.transform, "PositionText",
                "Pos: (0, 0, 0)",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(20f, yOffset - lineHeight), new Vector2(400f, lineHeight),
                20, FontStyle.Normal, Color.green, TextAnchor.MiddleLeft);

            Text rotationText = CreateText(canvasGo.transform, "RotationText",
                "Rot Y: 0.00",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(20f, yOffset - lineHeight * 2), new Vector2(400f, lineHeight),
                20, FontStyle.Normal, Color.green, TextAnchor.MiddleLeft);

            Text scaleText = CreateText(canvasGo.transform, "ScaleText",
                "Scale: 1.00",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(20f, yOffset - lineHeight * 3), new Vector2(400f, lineHeight),
                20, FontStyle.Normal, Color.green, TextAnchor.MiddleLeft);

            Text inputText = CreateText(canvasGo.transform, "InputText",
                "Input: [none]",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(20f, yOffset - lineHeight * 4), new Vector2(400f, lineHeight),
                20, FontStyle.Normal, Color.yellow, TextAnchor.MiddleLeft);

            // Instructions (bottom)
            CreateText(canvasGo.transform, "Instructions",
                "WASD: Move | Mouse: Rotate | LClick: Red toggle | RClick: Blue toggle | Scroll: Scale",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 20f), new Vector2(900f, 30f),
                16, FontStyle.Normal, new Color(0.7f, 0.7f, 0.7f, 1f), TextAnchor.MiddleCenter);

            // Wire controller component onto the cube
            MeshRenderer? renderer = cube.GetComponent<MeshRenderer>();
            InputReplayVerificationController controller = cube.AddComponent<InputReplayVerificationController>();
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("_frameText").objectReferenceValue = frameText;
            so.FindProperty("_positionText").objectReferenceValue = positionText;
            so.FindProperty("_rotationText").objectReferenceValue = rotationText;
            so.FindProperty("_scaleText").objectReferenceValue = scaleText;
            so.FindProperty("_inputText").objectReferenceValue = inputText;
            so.FindProperty("_cubeRenderer").objectReferenceValue = renderer;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Text CreateText(
            Transform parent, string name, string content,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 sizeDelta,
            int fontSize, FontStyle fontStyle, Color color, TextAnchor alignment)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            Text text = go.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = alignment;

            return text;
        }
    }
}
#endif
