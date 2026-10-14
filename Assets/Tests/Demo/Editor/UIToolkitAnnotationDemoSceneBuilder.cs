#if ULOOP_HAS_INPUT_SYSTEM
#nullable enable
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;

using io.github.hatayama.UnityCliLoop.Tests.Demo;

namespace io.github.hatayama.UnityCliLoop.Tests.Demo.Editor
{
    /// <summary>
    /// Builds the scene the UI Toolkit screenshot annotation E2E runs against: two screen-space UI Toolkit
    /// panels driven by UIToolkitAnnotationDemo, plus one uGUI button so both UI systems are annotated together.
    /// </summary>
    public static class UIToolkitAnnotationDemoSceneBuilder
    {
        private const string SCENE_PATH = "Assets/Scenes/UIToolkitAnnotationDemoScene.unity";
        private const string ASSET_FOLDER = "Assets/Tests/Demo/UIToolkitAnnotation";
        private const string BACK_PANEL_PATH = ASSET_FOLDER + "/UIToolkitAnnotationDemoBackPanel.asset";
        private const string FRONT_PANEL_PATH = ASSET_FOLDER + "/UIToolkitAnnotationDemoFrontPanel.asset";
        private const string THEME_PATH = "Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss";
        private const float BACK_SORTING_ORDER = 0f;
        private const float FRONT_SORTING_ORDER = 10f;

        [MenuItem("UnityCliLoop/Build UI Toolkit Annotation Demo Scene")]
        public static void Build()
        {
            PanelSettings backPanel = CreateOrUpdatePanelSettings(BACK_PANEL_PATH, BACK_SORTING_ORDER);
            PanelSettings frontPanel = CreateOrUpdatePanelSettings(FRONT_PANEL_PATH, FRONT_SORTING_ORDER);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            CreateEventSystem();
            CreateDemo(backPanel, frontPanel);
            CreateUguiButton();

            bool saved = EditorSceneManager.SaveScene(scene, SCENE_PATH);
            if (!saved)
            {
                Debug.Assert(false, $"[UIToolkitAnnotationDemoSceneBuilder] Failed to save scene to {SCENE_PATH}");
                return;
            }
            Debug.Log($"[UIToolkitAnnotationDemoSceneBuilder] Scene saved to {SCENE_PATH}");
        }

        // The theme is assigned explicitly so the panels do not depend on which theme Unity finds first.
        private static PanelSettings CreateOrUpdatePanelSettings(string path, float sortingOrder)
        {
            if (!AssetDatabase.IsValidFolder(ASSET_FOLDER))
            {
                AssetDatabase.CreateFolder("Assets/Tests/Demo", "UIToolkitAnnotation");
            }

            PanelSettings? settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, path);
            }

            ThemeStyleSheet theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(THEME_PATH);
            Debug.Assert(theme != null, $"[UIToolkitAnnotationDemoSceneBuilder] Missing theme at {THEME_PATH}");
            settings.themeStyleSheet = theme;
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = 1f;
            settings.sortingOrder = sortingOrder;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static void CreateCamera()
        {
            GameObject cameraGo = new("Main Camera");
            cameraGo.tag = "MainCamera";
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.12f, 1f);
            camera.transform.position = new Vector3(0f, 1f, -10f);
        }

        private static void CreateEventSystem()
        {
            GameObject eventSystemGo = new("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<InputSystemUIInputModule>();
        }

        // The two documents are siblings: a document under another document's GameObject would join its
        // parent's panel instead of using its own panel settings.
        private static void CreateDemo(PanelSettings backPanel, PanelSettings frontPanel)
        {
            GameObject demoGo = new("UIToolkitAnnotationDemo");
            UIDocument backDocument = CreateDocument("BackDocument", demoGo.transform, backPanel);
            UIDocument frontDocument = CreateDocument("FrontDocument", demoGo.transform, frontPanel);

            UIToolkitAnnotationDemo demo = demoGo.AddComponent<UIToolkitAnnotationDemo>();
            SerializedObject serializedDemo = new(demo);
            serializedDemo.FindProperty("backDocument").objectReferenceValue = backDocument;
            serializedDemo.FindProperty("frontDocument").objectReferenceValue = frontDocument;
            serializedDemo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static UIDocument CreateDocument(string name, Transform parent, PanelSettings panelSettings)
        {
            GameObject documentGo = new(name);
            documentGo.transform.SetParent(parent, false);
            UIDocument document = documentGo.AddComponent<UIDocument>();
            // Through the serialized field: assigning the property from a builder did not reach the saved scene.
            SerializedObject serializedDocument = new(document);
            serializedDocument.FindProperty("m_PanelSettings").objectReferenceValue = panelSettings;
            serializedDocument.ApplyModifiedPropertiesWithoutUndo();
            return document;
        }

        // Placed in the bottom-right corner, away from every UI Toolkit control.
        private static void CreateUguiButton()
        {
            GameObject canvasGo = new("Canvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            GameObject buttonGo = DefaultControls.CreateButton(new DefaultControls.Resources());
            buttonGo.name = "ugui-button";
            buttonGo.transform.SetParent(canvasGo.transform, false);
            RectTransform rectTransform = buttonGo.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(1f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 0f);
            rectTransform.pivot = new Vector2(1f, 0f);
            rectTransform.anchoredPosition = new Vector2(-40f, 40f);
            rectTransform.sizeDelta = new Vector2(200f, 50f);
        }
    }
}
#endif
