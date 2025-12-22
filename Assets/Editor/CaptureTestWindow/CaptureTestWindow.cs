using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace io.github.hatayama.uLoopMCP
{
    public class CaptureTestWindow : EditorWindow
    {
        private CaptureTarget _captureTarget = CaptureTarget.GameView;
        private float _resolutionScale = 1.0f;

        private int _width = 1920;
        private int _height = 1080;

        private Texture2D _previewTexture;
        private string _lastSavedPath = "";
        private string _statusMessage = "";
        private Vector2 _scrollPosition;

        private const string OUTPUT_DIRECTORY_NAME = "CaptureTestOutputs";

        [MenuItem("uLoopMCP/Windows/Capture Test Window")]
        public static void ShowWindow()
        {
            CaptureTestWindow window = GetWindow<CaptureTestWindow>();
            window.titleContent = new GUIContent("Capture Test");
            window.minSize = new Vector2(400, 600);
            window.Show();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            GUILayout.Label("Capture Test Window", EditorStyles.boldLabel);
            GUILayout.Space(10);

            DrawCaptureTargetSection();
            GUILayout.Space(10);

            DrawCommonParametersSection();
            GUILayout.Space(10);

            if (_captureTarget == CaptureTarget.SceneView)
            {
                DrawSceneViewParametersSection();
                GUILayout.Space(10);
            }

            DrawCaptureButton();
            GUILayout.Space(10);

            DrawStatusSection();
            GUILayout.Space(10);

            DrawPreviewSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawCaptureTargetSection()
        {
            GUILayout.Label("Capture Target", EditorStyles.boldLabel);
            _captureTarget = (CaptureTarget)EditorGUILayout.EnumPopup("Target", _captureTarget);
        }

        private void DrawCommonParametersSection()
        {
            GUILayout.Label("Common Parameters", EditorStyles.boldLabel);
            _resolutionScale = EditorGUILayout.Slider("Resolution Scale", _resolutionScale, 0.1f, 1.0f);
        }

        private void DrawSceneViewParametersSection()
        {
            GUILayout.Label("Output Size", EditorStyles.boldLabel);
            _width = EditorGUILayout.IntField("Width", _width);
            _height = EditorGUILayout.IntField("Height", _height);

            _width = Mathf.Clamp(_width, 64, 8192);
            _height = Mathf.Clamp(_height, 64, 8192);
        }

        private void DrawCaptureButton()
        {
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Capture", GUILayout.Height(40)))
            {
                ExecuteCapture();
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawStatusSection()
        {
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.HelpBox(_statusMessage, MessageType.Info);
            }

            if (!string.IsNullOrEmpty(_lastSavedPath))
            {
                EditorGUILayout.LabelField("Last Saved:", _lastSavedPath);
                if (GUILayout.Button("Open in Explorer"))
                {
                    EditorUtility.RevealInFinder(_lastSavedPath);
                }
            }
        }

        private void DrawPreviewSection()
        {
            GUILayout.Label("Preview", EditorStyles.boldLabel);
            if (_previewTexture != null)
            {
                float aspectRatio = (float)_previewTexture.width / _previewTexture.height;
                float previewWidth = Mathf.Min(position.width - 20, 400);
                float previewHeight = previewWidth / aspectRatio;

                Rect previewRect = GUILayoutUtility.GetRect(previewWidth, previewHeight);
                GUI.DrawTexture(previewRect, _previewTexture, ScaleMode.ScaleToFit);

                EditorGUILayout.LabelField($"Size: {_previewTexture.width} x {_previewTexture.height}");
            }
            else
            {
                EditorGUILayout.HelpBox("No preview available. Capture an image first.", MessageType.None);
            }
        }

        private void ExecuteCapture()
        {
            _statusMessage = "Capturing...";
            Repaint();

            switch (_captureTarget)
            {
                case CaptureTarget.GameView:
                    CaptureGameView();
                    break;
                case CaptureTarget.SceneView:
                    CaptureSceneView();
                    break;
            }
        }

        private void CaptureGameView()
        {
            string outputDirectory = EnsureOutputDirectoryExists();
            string fileName = GenerateFileName("gameview");
            string fullPath = Path.Combine(outputDirectory, fileName);

            ScreenCapture.CaptureScreenshot(fullPath, Mathf.RoundToInt(1.0f / _resolutionScale));

            Assembly asm = typeof(EditorWindow).Assembly;
            EditorWindow gameView = EditorWindow.GetWindow(asm.GetType("UnityEditor.GameView"));
            gameView.Repaint();

            EditorApplication.delayCall += () =>
            {
                EditorApplication.delayCall += () =>
                {
                    LoadPreviewAndUpdateStatus(fullPath);
                };
            };
        }

        private void CaptureSceneView()
        {
            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                CapturePrefabView(prefabStage);
                return;
            }

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                _statusMessage = "Error: No active SceneView found";
                return;
            }

            List<CanvasRenderModeBackup> canvasBackups = ConvertOverlayCanvasesInActiveScene();

            sceneView.Repaint();

            Camera camera = sceneView.camera;
            if (camera == null)
            {
                RestoreCanvasRenderModes(canvasBackups);
                _statusMessage = "Error: SceneView camera not available";
                return;
            }

            int captureWidth = _width;
            int captureHeight = _height;

            if (canvasBackups.Count > 0)
            {
                Rect cameraRect = sceneView.camera.pixelRect;
                captureWidth = Mathf.RoundToInt(cameraRect.width);
                captureHeight = Mathf.RoundToInt(cameraRect.height);
            }

            Texture2D texture = RenderCameraToTexture(camera, captureWidth, captureHeight);

            RestoreCanvasRenderModes(canvasBackups);

            if (texture == null)
            {
                _statusMessage = "Error: Failed to render camera";
                return;
            }

            if (!Mathf.Approximately(_resolutionScale, 1.0f))
            {
                texture = ApplyResolutionScaling(texture, _resolutionScale);
            }

            string outputDirectory = EnsureOutputDirectoryExists();
            string fileName = GenerateFileName("sceneview");
            string fullPath = Path.Combine(outputDirectory, fileName);

            SaveTextureAsPng(texture, fullPath);
            LoadPreviewAndUpdateStatus(fullPath);

            if (_previewTexture != texture)
            {
                DestroyImmediate(texture);
            }
        }

        private void CapturePrefabView(PrefabStage prefabStage)
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                sceneView = SceneView.GetWindow<SceneView>();
            }

            if (sceneView == null)
            {
                _statusMessage = "Error: No active SceneView found";
                return;
            }

            GameObject prefabRoot = prefabStage.prefabContentsRoot;

            List<CanvasRenderModeBackup> canvasBackups = ConvertOverlayCanvasesToWorldSpace(prefabRoot);

            Canvas.ForceUpdateCanvases();

            sceneView.Focus();
            sceneView.Repaint();

            int frameCount = 0;
            int requiredFrames = 5;

            EditorApplication.CallbackFunction waitForFrames = null;
            waitForFrames = () =>
            {
                frameCount++;
                if (frameCount >= requiredFrames)
                {
                    EditorApplication.update -= waitForFrames;
                    ExecutePrefabCapture(sceneView, prefabStage, canvasBackups);
                }
                else
                {
                    sceneView.Repaint();
                }
            };
            EditorApplication.update += waitForFrames;
        }

        private void ExecutePrefabCapture(SceneView sceneView, PrefabStage prefabStage, List<CanvasRenderModeBackup> canvasBackups)
        {
            Camera camera = sceneView.camera;
            if (camera == null)
            {
                RestoreCanvasRenderModes(canvasBackups);
                ClearPrefabDirtiness();
                _statusMessage = "Error: SceneView camera not available";
                Repaint();
                return;
            }

            int captureWidth = _width;
            int captureHeight = _height;

            if (canvasBackups.Count > 0)
            {
                Rect cameraRect = sceneView.camera.pixelRect;
                captureWidth = Mathf.RoundToInt(cameraRect.width);
                captureHeight = Mathf.RoundToInt(cameraRect.height);
            }

            GameObject tempLight = null;
            if (canvasBackups.Count == 0)
            {
                tempLight = new GameObject("TempCaptureLight");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(tempLight, prefabStage.scene);
                Light light = tempLight.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 0.5f;
                light.color = Color.white;
                tempLight.transform.rotation = camera.transform.rotation;
            }

            sceneView.Repaint();

            Texture2D texture = RenderCameraToTexture(camera, captureWidth, captureHeight);

            if (tempLight != null)
            {
                DestroyImmediate(tempLight);
            }

            RestoreCanvasRenderModes(canvasBackups);
            ClearPrefabDirtiness();

            if (texture == null)
            {
                _statusMessage = "Error: Failed to render camera";
                Repaint();
                return;
            }

            if (!Mathf.Approximately(_resolutionScale, 1.0f))
            {
                texture = ApplyResolutionScaling(texture, _resolutionScale);
            }

            string outputDirectory = EnsureOutputDirectoryExists();
            string prefabName = Path.GetFileNameWithoutExtension(prefabStage.assetPath);
            string fileName = GenerateFileName($"prefab_{prefabName}");
            string fullPath = Path.Combine(outputDirectory, fileName);

            SaveTextureAsPng(texture, fullPath);
            LoadPreviewAndUpdateStatus(fullPath);

            if (_previewTexture != texture)
            {
                DestroyImmediate(texture);
            }
        }

        private struct CanvasRenderModeBackup
        {
            public Canvas Canvas;
            public RenderMode OriginalRenderMode;
            public Camera OriginalWorldCamera;
            public Vector3 OriginalLocalPosition;
            public Vector3 OriginalLocalScale;
            public Quaternion OriginalRotation;
        }

        private List<CanvasRenderModeBackup> ConvertOverlayCanvasesToWorldSpace(GameObject root)
        {
            List<CanvasRenderModeBackup> backups = new List<CanvasRenderModeBackup>();

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null)
            {
                return backups;
            }

            GameObject[] rootObjects = prefabStage.scene.GetRootGameObjects();

            List<Canvas> allCanvases = new List<Canvas>();
            foreach (GameObject rootObj in rootObjects)
            {
                Canvas[] canvasesInRoot = rootObj.GetComponentsInChildren<Canvas>(true);
                allCanvases.AddRange(canvasesInRoot);
            }

            foreach (Canvas canvas in allCanvases)
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    RectTransform rt = canvas.GetComponent<RectTransform>();
                    Vector3 originalPosition = rt != null ? rt.position : Vector3.zero;
                    Quaternion originalRotation = rt != null ? rt.rotation : Quaternion.identity;
                    Vector3 originalScale = rt != null ? rt.localScale : Vector3.one;

                    CanvasRenderModeBackup backup = new CanvasRenderModeBackup
                    {
                        Canvas = canvas,
                        OriginalRenderMode = canvas.renderMode,
                        OriginalWorldCamera = canvas.worldCamera,
                        OriginalLocalPosition = originalPosition,
                        OriginalLocalScale = originalScale,
                        OriginalRotation = originalRotation
                    };
                    backups.Add(backup);

                    canvas.renderMode = RenderMode.WorldSpace;
                }
            }

            return backups;
        }

        private List<CanvasRenderModeBackup> ConvertOverlayCanvasesInActiveScene()
        {
            List<CanvasRenderModeBackup> backups = new List<CanvasRenderModeBackup>();

            Canvas[] allCanvases = FindObjectsOfType<Canvas>(true);

            foreach (Canvas canvas in allCanvases)
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    RectTransform rt = canvas.GetComponent<RectTransform>();
                    Vector3 originalPosition = rt != null ? rt.position : Vector3.zero;
                    Quaternion originalRotation = rt != null ? rt.rotation : Quaternion.identity;
                    Vector3 originalScale = rt != null ? rt.localScale : Vector3.one;

                    CanvasRenderModeBackup backup = new CanvasRenderModeBackup
                    {
                        Canvas = canvas,
                        OriginalRenderMode = canvas.renderMode,
                        OriginalWorldCamera = canvas.worldCamera,
                        OriginalLocalPosition = originalPosition,
                        OriginalLocalScale = originalScale,
                        OriginalRotation = originalRotation
                    };
                    backups.Add(backup);

                    canvas.renderMode = RenderMode.WorldSpace;
                }
            }

            return backups;
        }

        private void RestoreCanvasRenderModes(List<CanvasRenderModeBackup> backups)
        {
            foreach (CanvasRenderModeBackup backup in backups)
            {
                if (backup.Canvas != null)
                {
                    backup.Canvas.renderMode = backup.OriginalRenderMode;
                    backup.Canvas.worldCamera = backup.OriginalWorldCamera;

                    RectTransform rt = backup.Canvas.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.position = backup.OriginalLocalPosition;
                        rt.rotation = backup.OriginalRotation;
                        rt.localScale = backup.OriginalLocalScale;
                    }
                }
            }
        }

        private void ClearPrefabDirtiness()
        {
            PrefabStage currentPrefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (currentPrefabStage != null)
            {
                currentPrefabStage.ClearDirtiness();
            }
        }

        private Texture2D RenderCameraToTexture(Camera camera, int width, int height, bool preserveClearFlags = true)
        {
            RenderTexture renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            renderTexture.Create();

            RenderTexture originalTargetTexture = camera.targetTexture;
            CameraClearFlags originalClearFlags = camera.clearFlags;
            Color originalBackgroundColor = camera.backgroundColor;
            float originalAspect = camera.aspect;

            camera.targetTexture = renderTexture;
            camera.aspect = (float)width / height;

            if (!preserveClearFlags)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.125f, 0.224f, 0.322f, 1f);
            }

            camera.Render();

            RenderTexture.active = renderTexture;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();

            camera.targetTexture = originalTargetTexture;
            camera.clearFlags = originalClearFlags;
            camera.backgroundColor = originalBackgroundColor;
            camera.aspect = originalAspect;
            RenderTexture.active = null;

            DestroyImmediate(renderTexture);

            return texture;
        }

        private Texture2D ApplyResolutionScaling(Texture2D originalTexture, float scale)
        {
            if (Mathf.Approximately(scale, 1.0f))
            {
                return originalTexture;
            }

            int newWidth = Mathf.RoundToInt(originalTexture.width * scale);
            int newHeight = Mathf.RoundToInt(originalTexture.height * scale);

            Texture2D scaledTexture = new Texture2D(newWidth, newHeight, originalTexture.format, false);

            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
            Graphics.Blit(originalTexture, rt);

            RenderTexture.active = rt;
            scaledTexture.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            scaledTexture.Apply();
            RenderTexture.active = null;

            RenderTexture.ReleaseTemporary(rt);

            DestroyImmediate(originalTexture);

            return scaledTexture;
        }

        private string EnsureOutputDirectoryExists()
        {
            string projectRoot = Application.dataPath.Replace("/Assets", "");
            string outputDirectory = Path.Combine(projectRoot, "uLoopMCPOutputs", OUTPUT_DIRECTORY_NAME);

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            return outputDirectory;
        }

        private string GenerateFileName(string prefix)
        {
            DateTime now = DateTime.Now;
            return $"{prefix}_{now:yyyyMMdd_HHmmss_fff}.png";
        }

        private void SaveTextureAsPng(Texture2D texture, string fullPath)
        {
            byte[] pngData = texture.EncodeToPNG();
            File.WriteAllBytes(fullPath, pngData);
        }

        private void LoadPreviewAndUpdateStatus(string fullPath)
        {
            if (_previewTexture != null)
            {
                DestroyImmediate(_previewTexture);
            }

            if (File.Exists(fullPath))
            {
                byte[] fileData = File.ReadAllBytes(fullPath);
                _previewTexture = new Texture2D(2, 2);
                _previewTexture.LoadImage(fileData);

                _lastSavedPath = fullPath;
                _statusMessage = $"Capture successful! Size: {_previewTexture.width}x{_previewTexture.height}";
            }
            else
            {
                _statusMessage = "Error: Failed to save capture file";
            }

            Repaint();
        }

        private void OnDestroy()
        {
            if (_previewTexture != null)
            {
                DestroyImmediate(_previewTexture);
            }
        }
    }
}

