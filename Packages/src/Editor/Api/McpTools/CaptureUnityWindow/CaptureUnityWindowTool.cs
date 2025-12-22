using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace io.github.hatayama.uLoopMCP
{
    [McpTool(Description = "Capture Unity Game View and save as PNG image")]
    public class CaptureUnityWindowTool : AbstractUnityTool<CaptureUnityWindowSchema, CaptureUnityWindowResponse>
    {
        public override string ToolName => "capture-unity-window";

        private const string OUTPUT_DIRECTORY_NAME = "UnityWindowCaptures";

        protected override async Task<CaptureUnityWindowResponse> ExecuteAsync(
            CaptureUnityWindowSchema parameters,
            CancellationToken ct)
        {
            string correlationId = McpConstants.GenerateCorrelationId();

            VibeLogger.LogInfo(
                "capture_unity_window_start",
                "Unity window capture started",
                new { Target = parameters.Target.ToString(), ResolutionScale = parameters.ResolutionScale },
                correlationId: correlationId,
                humanNote: "User requested Unity window screenshot",
                aiTodo: "Monitor capture performance and file size"
            );

            ValidateParameters(parameters);

            CaptureUnityWindowResponse response;

            switch (parameters.Target)
            {
                case CaptureWindowTarget.GameView:
                    response = await CaptureGameViewAsync(parameters.ResolutionScale, correlationId, ct);
                    break;
                case CaptureWindowTarget.SceneView:
                    response = await CaptureSceneViewAsync(parameters.ResolutionScale, correlationId, ct);
                    break;
                default:
                    throw new ArgumentException($"Unknown capture target: {parameters.Target}");
            }

            VibeLogger.LogInfo(
                "capture_unity_window_complete",
                "Unity window capture operation completed",
                new { Target = parameters.Target.ToString(), ImagePath = response.ImagePath },
                correlationId: correlationId
            );

            return response;
        }

        private void ValidateParameters(CaptureUnityWindowSchema parameters)
        {
            if (parameters.ResolutionScale < 0.1f || parameters.ResolutionScale > 1.0f)
            {
                throw new ArgumentException(
                    $"ResolutionScale must be between 0.1 and 1.0, got: {parameters.ResolutionScale}");
            }
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

        private async Task<CaptureUnityWindowResponse> CaptureGameViewAsync(
            float resolutionScale,
            string correlationId,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            string tempFileName = $"temp_screenshot_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
            string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), tempFileName);

            ScreenCapture.CaptureScreenshot(tempFileName, 1);

            if (!Application.isPlaying)
            {
                Assembly asm = typeof(EditorWindow).Assembly;
                EditorWindow gameView = EditorWindow.GetWindow(asm.GetType("UnityEditor.GameView"));
                gameView.Repaint();
            }
            else
            {
                EditorApplication.QueuePlayerLoopUpdate();
            }

            int maxAttempts = 50;
            int attempts = 0;
            while (!File.Exists(fullPath) && attempts < maxAttempts)
            {
                await TimerDelay.Wait(100, ct);
                attempts++;
            }

            if (!File.Exists(fullPath))
            {
                VibeLogger.LogError(
                    "capture_gameview_error",
                    "Failed to capture Game View - screenshot file was not created",
                    new { Path = fullPath },
                    correlationId: correlationId
                );
                return new CaptureUnityWindowResponse(failure: true);
            }

            FileInfo fileInfo = new FileInfo(fullPath);
            if (fileInfo.Length == 0)
            {
                File.Delete(fullPath);
                VibeLogger.LogError(
                    "capture_gameview_error",
                    "Failed to capture Game View - screenshot file is empty",
                    correlationId: correlationId
                );
                return new CaptureUnityWindowResponse(failure: true);
            }

            byte[] fileData = File.ReadAllBytes(fullPath);
            Texture2D texture = new Texture2D(2, 2);

            if (!texture.LoadImage(fileData))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                File.Delete(fullPath);
                VibeLogger.LogError(
                    "capture_gameview_error",
                    "Failed to load captured screenshot as texture",
                    correlationId: correlationId
                );
                return new CaptureUnityWindowResponse(failure: true);
            }

            File.Delete(fullPath);

            if (!Mathf.Approximately(resolutionScale, 1.0f))
            {
                texture = ApplyResolutionScaling(texture, resolutionScale);
            }

            string outputDirectory = EnsureOutputDirectoryExists();
            string fileName = $"gameview_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
            string savedPath = Path.Combine(outputDirectory, fileName);

            SaveTextureAsPng(texture, savedPath);

            int width = texture.width;
            int height = texture.height;
            UnityEngine.Object.DestroyImmediate(texture);

            FileInfo savedFileInfo = new FileInfo(savedPath);

            VibeLogger.LogInfo(
                "capture_gameview_success",
                "Game view captured successfully",
                new { ImagePath = savedPath, FileSizeBytes = savedFileInfo.Length, Width = width, Height = height },
                correlationId: correlationId
            );

            return new CaptureUnityWindowResponse(savedPath, savedFileInfo.Length, width, height);
        }

        private async Task<CaptureUnityWindowResponse> CaptureSceneViewAsync(
            float resolutionScale,
            string correlationId,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                return await CapturePrefabViewAsync(prefabStage, resolutionScale, correlationId, ct);
            }

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                VibeLogger.LogError(
                    "capture_sceneview_error",
                    "No active SceneView found",
                    correlationId: correlationId
                );
                return new CaptureUnityWindowResponse(failure: true);
            }

            List<CanvasRenderModeBackup> canvasBackups = ConvertOverlayCanvasesInActiveScene();

            sceneView.Repaint();

            Camera camera = sceneView.camera;
            if (camera == null)
            {
                RestoreCanvasRenderModes(canvasBackups);
                VibeLogger.LogError(
                    "capture_sceneview_error",
                    "SceneView camera not available",
                    correlationId: correlationId
                );
                return new CaptureUnityWindowResponse(failure: true);
            }

            Rect cameraRect = sceneView.camera.pixelRect;
            int captureWidth = Mathf.RoundToInt(cameraRect.width);
            int captureHeight = Mathf.RoundToInt(cameraRect.height);

            Texture2D texture = RenderCameraToTexture(camera, captureWidth, captureHeight, preserveClearFlags: true);

            RestoreCanvasRenderModes(canvasBackups);

            if (texture == null)
            {
                VibeLogger.LogError(
                    "capture_sceneview_error",
                    "Failed to render camera",
                    correlationId: correlationId
                );
                return new CaptureUnityWindowResponse(failure: true);
            }

            if (!Mathf.Approximately(resolutionScale, 1.0f))
            {
                texture = ApplyResolutionScaling(texture, resolutionScale);
            }

            string outputDirectory = EnsureOutputDirectoryExists();
            string fileName = $"sceneview_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
            string savedPath = Path.Combine(outputDirectory, fileName);

            SaveTextureAsPng(texture, savedPath);

            int width = texture.width;
            int height = texture.height;
            UnityEngine.Object.DestroyImmediate(texture);

            FileInfo savedFileInfo = new FileInfo(savedPath);

            VibeLogger.LogInfo(
                "capture_sceneview_success",
                "Scene view captured successfully",
                new { ImagePath = savedPath, FileSizeBytes = savedFileInfo.Length, Width = width, Height = height },
                correlationId: correlationId
            );

            return new CaptureUnityWindowResponse(savedPath, savedFileInfo.Length, width, height);
        }

        private async Task<CaptureUnityWindowResponse> CapturePrefabViewAsync(
            PrefabStage prefabStage,
            float resolutionScale,
            string correlationId,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                sceneView = SceneView.GetWindow<SceneView>();
            }

            if (sceneView == null)
            {
                VibeLogger.LogError(
                    "capture_prefabview_error",
                    "No active SceneView found",
                    correlationId: correlationId
                );
                return new CaptureUnityWindowResponse(failure: true);
            }

            GameObject prefabRoot = prefabStage.prefabContentsRoot;

            List<CanvasRenderModeBackup> canvasBackups = ConvertOverlayCanvasesToWorldSpace(prefabStage);

            Canvas.ForceUpdateCanvases();

            sceneView.Focus();
            sceneView.Repaint();

            await TimerDelay.Wait(200, ct);

            Camera camera = sceneView.camera;
            if (camera == null)
            {
                RestoreCanvasRenderModes(canvasBackups);
                ClearPrefabDirtiness();
                VibeLogger.LogError(
                    "capture_prefabview_error",
                    "SceneView camera not available",
                    correlationId: correlationId
                );
                return new CaptureUnityWindowResponse(failure: true);
            }

            Rect cameraRect = sceneView.camera.pixelRect;
            int captureWidth = Mathf.RoundToInt(cameraRect.width);
            int captureHeight = Mathf.RoundToInt(cameraRect.height);

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

            Texture2D texture = RenderCameraToTexture(camera, captureWidth, captureHeight, preserveClearFlags: true);

            if (tempLight != null)
            {
                UnityEngine.Object.DestroyImmediate(tempLight);
            }

            RestoreCanvasRenderModes(canvasBackups);
            ClearPrefabDirtiness();

            if (texture == null)
            {
                VibeLogger.LogError(
                    "capture_prefabview_error",
                    "Failed to render camera",
                    correlationId: correlationId
                );
                return new CaptureUnityWindowResponse(failure: true);
            }

            if (!Mathf.Approximately(resolutionScale, 1.0f))
            {
                texture = ApplyResolutionScaling(texture, resolutionScale);
            }

            string outputDirectory = EnsureOutputDirectoryExists();
            string prefabName = Path.GetFileNameWithoutExtension(prefabStage.assetPath);
            string fileName = $"prefab_{prefabName}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
            string savedPath = Path.Combine(outputDirectory, fileName);

            SaveTextureAsPng(texture, savedPath);

            int width = texture.width;
            int height = texture.height;
            UnityEngine.Object.DestroyImmediate(texture);

            FileInfo savedFileInfo = new FileInfo(savedPath);

            VibeLogger.LogInfo(
                "capture_prefabview_success",
                "Prefab view captured successfully",
                new { ImagePath = savedPath, FileSizeBytes = savedFileInfo.Length, Width = width, Height = height, PrefabPath = prefabStage.assetPath },
                correlationId: correlationId
            );

            return new CaptureUnityWindowResponse(savedPath, savedFileInfo.Length, width, height);
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

        private List<CanvasRenderModeBackup> ConvertOverlayCanvasesToWorldSpace(PrefabStage prefabStage)
        {
            List<CanvasRenderModeBackup> backups = new List<CanvasRenderModeBackup>();

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

            Canvas[] allCanvases = UnityEngine.Object.FindObjectsOfType<Canvas>(true);

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

        private Texture2D RenderCameraToTexture(Camera camera, int width, int height, bool preserveClearFlags)
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

            UnityEngine.Object.DestroyImmediate(renderTexture);

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

            UnityEngine.Object.DestroyImmediate(originalTexture);

            return scaledTexture;
        }

        private void SaveTextureAsPng(Texture2D texture, string fullPath)
        {
            byte[] pngData = texture.EncodeToPNG();
            File.WriteAllBytes(fullPath, pngData);
        }
    }
}
