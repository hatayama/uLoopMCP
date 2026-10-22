using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Verifies the prefab screenshot use case: its response, its output file, and that it leaves no preview scene or texture behind.
    /// </summary>
    public sealed class PrefabScreenshotUseCaseTests
    {
        private const string FixtureFolder = "Assets/Tests/Editor/PrefabScreenshotFixtures";
        private const string UiPartPath = FixtureFolder + "/UiPartWithOverflowingChild.prefab";
        private const string EmptyRootPath = FixtureFolder + "/EmptyRoot.prefab";
        private const string MissingPath = FixtureFolder + "/Missing.prefab";
        private const string NonGameObjectPath = "Assets/Tests/Editor/PrefabScreenshotUseCaseTests.cs";

        private string _outputDirectory;

        [SetUp]
        public void SetUp()
        {
            _outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_outputDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_outputDirectory))
            {
                Directory.Delete(_outputDirectory, true);
            }
        }

        /// <summary>
        /// Verifies a UI part prefab produces one PNG with prefab metadata and cleans up its scene and texture.
        /// </summary>
        [Test]
        public void CaptureAsync_WithAUiPartPrefab_ReturnsOnePrefabScreenshotAndCleansUp()
        {
            RecordingRenderer renderer = new();
            int previewScenesBefore = EditorSceneManager.previewSceneCount;

            ScreenshotResponse response = Capture(renderer, CreateRequest(UiPartPath));

            Assert.That(response.Success, Is.True);
            Assert.That(response.ResolvedCaptureMode, Is.EqualTo("prefab"));
            Assert.That(response.Message, Does.Contain("UI part"));
            Assert.That(response.Screenshots.Count, Is.EqualTo(1));
            ScreenshotInfo info = response.Screenshots[0];
            Assert.That(info.Width, Is.EqualTo(140));
            Assert.That(info.Height, Is.EqualTo(100));
            Assert.That(File.Exists(info.ImagePath), Is.True);
            Assert.That(Path.GetFileName(info.ImagePath), Does.StartWith("Prefab_UiPartWithOverflowingChild_"));
            Assert.That(info.ImageCoordinateSystem, Is.EqualTo("top-left-prefab-image"));
            Assert.That(info.ScreenshotToInputFormula, Is.EqualTo(UnityCliLoopConstants.SCREENSHOT_PREFAB_TO_INPUT_FORMULA_UNAVAILABLE));
            Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(previewScenesBefore));
            Assert.That(renderer.LastReturnedTexture == null, Is.True, "the rendered texture must be destroyed");
            Assert.That(renderer.CameraSceneWasPreview, Is.True);
        }

        /// <summary>
        /// Verifies ResolutionScale shrinks the rendered image after rendering.
        /// </summary>
        [Test]
        public void CaptureAsync_WithHalfResolutionScale_HalvesTheImage()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("Resolution scaling blits on the GPU; skipped on the Null device.");
            }

            ScreenshotSchema request = CreateRequest(UiPartPath);
            request.ResolutionScale = 0.5f;

            ScreenshotResponse response = Capture(new RecordingRenderer(), request);

            Assert.That(response.Screenshots[0].Width, Is.EqualTo(70));
            Assert.That(response.Screenshots[0].Height, Is.EqualTo(50));
        }

        /// <summary>
        /// Verifies WindowName is ignored for a prefab capture.
        /// </summary>
        [Test]
        public void CaptureAsync_WithAWindowName_IgnoresIt()
        {
            ScreenshotSchema request = CreateRequest(UiPartPath);
            request.WindowName = "Scene";

            ScreenshotResponse response = Capture(new RecordingRenderer(), request);

            Assert.That(response.Success, Is.True);
        }

        /// <summary>
        /// Verifies the camera clears to the default #808080 background when no color is given.
        /// </summary>
        [Test]
        public void CaptureAsync_WithoutABackgroundColor_ClearsToTheDefaultGray()
        {
            RecordingRenderer renderer = new();

            Capture(renderer, CreateRequest(UiPartPath));

            Assert.That(renderer.CameraClearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            AssertColor(new Color(0x80 / 255f, 0x80 / 255f, 0x80 / 255f, 1f), renderer.CameraBackgroundColor);
        }

        /// <summary>
        /// Verifies the camera clears to the requested background color including alpha.
        /// </summary>
        [Test]
        public void CaptureAsync_WithABackgroundColor_ClearsToThatColor()
        {
            RecordingRenderer renderer = new();
            ScreenshotSchema request = CreateRequest(UiPartPath);
            request.PrefabBackgroundColor = "#33669980";

            Capture(renderer, request);

            Assert.That(renderer.CameraClearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            AssertColor(new Color(0x33 / 255f, 0x66 / 255f, 0x99 / 255f, 0x80 / 255f), renderer.CameraBackgroundColor);
        }

        /// <summary>
        /// Verifies a malformed background color is rejected before any scene is created or file written.
        /// </summary>
        [Test]
        public void CaptureAsync_WithAMalformedBackgroundColor_RejectsWithoutSideEffects()
        {
            ScreenshotSchema request = CreateRequest(UiPartPath);
            request.PrefabBackgroundColor = "#FFF";

            AssertRejectedWithoutSideEffects<UnityCliLoopToolParameterValidationException>(request, CancellationToken.None);
        }

        /// <summary>
        /// Verifies a cancelled token stops the capture before any scene is created or file written.
        /// </summary>
        [Test]
        public void CaptureAsync_WithACancelledToken_ThrowsWithoutSideEffects()
        {
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();

            AssertRejectedWithoutSideEffects<OperationCanceledException>(CreateRequest(UiPartPath), cancellation.Token);
        }

        /// <summary>
        /// Verifies a path with no asset is rejected without rendering or writing a file.
        /// </summary>
        [Test]
        public void CaptureAsync_WithAMissingAsset_RejectsWithoutSideEffects()
        {
            AssertRejectedWithoutSideEffects<UnityCliLoopToolParameterValidationException>(CreateRequest(MissingPath), CancellationToken.None);
        }

        /// <summary>
        /// Verifies a path to an asset that is not a GameObject is rejected without rendering or writing a file.
        /// </summary>
        [Test]
        public void CaptureAsync_WithANonGameObjectAsset_RejectsWithoutSideEffects()
        {
            AssertRejectedWithoutSideEffects<UnityCliLoopToolParameterValidationException>(CreateRequest(NonGameObjectPath), CancellationToken.None);
        }

        /// <summary>
        /// Verifies a prefab with nothing to render is rejected and its preview scene is closed.
        /// </summary>
        [Test]
        public void CaptureAsync_WithAnEmptyPrefab_RejectsAndClosesThePreviewScene()
        {
            AssertRejectedWithoutSideEffects<UnityCliLoopToolParameterValidationException>(CreateRequest(EmptyRootPath), CancellationToken.None);
        }

        /// <summary>
        /// Verifies a rendering failure propagates unchanged and leaves no file or preview scene behind.
        /// </summary>
        [Test]
        public void CaptureAsync_WhenRenderingFails_PropagatesAndCleansUp()
        {
            RecordingRenderer renderer = new() { ThrowOnRender = true };
            int previewScenesBefore = EditorSceneManager.previewSceneCount;
            PrefabScreenshotUseCase useCase = CreateUseCase(renderer);

            Assert.Throws<InvalidOperationException>(() => useCase.CaptureAsync(CreateRequest(UiPartPath), CancellationToken.None));

            Assert.That(Directory.GetFiles(_outputDirectory), Is.Empty);
            Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(previewScenesBefore));
        }

        /// <summary>
        /// Verifies an image with nothing drawn is rejected, and no file, texture, or preview scene is left behind.
        /// </summary>
        [Test]
        public void CaptureAsync_WhenNothingIsDrawn_RejectsWithoutWritingAFile()
        {
            RecordingRenderer renderer = new() { ReturnBlank = true };
            int previewScenesBefore = EditorSceneManager.previewSceneCount;
            PrefabScreenshotUseCase useCase = CreateUseCase(renderer);

            Assert.Throws<UnityCliLoopToolParameterValidationException>(
                () => useCase.CaptureAsync(CreateRequest(UiPartPath), CancellationToken.None));

            Assert.That(Directory.GetFiles(_outputDirectory), Is.Empty);
            Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(previewScenesBefore));
            Assert.That(renderer.LastReturnedTexture == null, Is.True, "the rendered texture must be destroyed");
        }

        /// <summary>
        /// Verifies an image showing only the camera's background color is rejected as nothing drawn.
        /// </summary>
        [Test]
        public void CaptureAsync_WhenOnlyTheBackgroundIsDrawn_RejectsWithoutWritingAFile()
        {
            RecordingRenderer renderer = new() { FillWithCameraBackground = true };
            int previewScenesBefore = EditorSceneManager.previewSceneCount;
            PrefabScreenshotUseCase useCase = CreateUseCase(renderer);

            Assert.Throws<UnityCliLoopToolParameterValidationException>(
                () => useCase.CaptureAsync(CreateRequest(UiPartPath), CancellationToken.None));

            Assert.That(Directory.GetFiles(_outputDirectory), Is.Empty);
            Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(previewScenesBefore));
        }

        /// <summary>
        /// Verifies pixels the pipeline left transparent are filled with the default opaque gray while drawn pixels stay.
        /// </summary>
        [Test]
        public void CaptureAsync_WithTheDefaultBackground_FillsUndrawnPixelsWithGray()
        {
            ScreenshotResponse response = Capture(new RecordingRenderer(), CreateRequest(UiPartPath));

            Texture2D image = LoadSavedImage(response);
            try
            {
                AssertColor32Near(image.GetPixel(0, 0), new Color32(128, 128, 128, 255), 1);
                AssertColor32Near(image.GetPixel(image.width / 2, image.height / 2), new Color32(255, 0, 0, 255), 0);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        /// <summary>
        /// Verifies a transparent background is not composited, so undrawn pixels stay transparent.
        /// </summary>
        [Test]
        public void CaptureAsync_WithATransparentBackground_KeepsUndrawnPixelsTransparent()
        {
            ScreenshotSchema request = CreateRequest(UiPartPath);
            request.PrefabBackgroundColor = "#00000000";

            ScreenshotResponse response = Capture(new RecordingRenderer(), request);

            Texture2D image = LoadSavedImage(response);
            try
            {
                Assert.That(((Color32)image.GetPixel(0, 0)).a, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        /// <summary>
        /// Verifies capturing does not modify the prefab asset.
        /// </summary>
        [Test]
        public void CaptureAsync_WithAUiPartPrefab_LeavesTheAssetUnchanged()
        {
            Capture(new RecordingRenderer(), CreateRequest(UiPartPath));

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(UiPartPath);
            Assert.That(EditorUtility.IsDirty(asset), Is.False);
            Assert.That(asset.GetComponent<RectTransform>().rect.size, Is.EqualTo(new Vector2(100f, 100f)));
        }

        /// <summary>
        /// Verifies the real renderer produces a PNG sized to the whole UI part on any device with a GPU path.
        /// </summary>
        [Test]
        public void CaptureAsync_WithTheRealRenderer_WritesAnImageSizedToTheUiPart()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("Rendering needs a graphics device; skipped on the Null device.");
            }

            Texture2D image = CaptureWithTheRealRenderer();
            try
            {
                Assert.That(image.width, Is.EqualTo(140));
                Assert.That(image.height, Is.EqualTo(100));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        /// <summary>
        /// Verifies the real renderer draws the panel, the overflowing badge, and the gray background into the PNG.
        /// </summary>
        [Test]
        public void CaptureAsync_WithTheRealRenderer_DrawsTheWholeUiPart()
        {
            GraphicsDeviceType deviceType = SystemInfo.graphicsDeviceType;
            if (deviceType == GraphicsDeviceType.Null || deviceType == GraphicsDeviceType.OpenGLCore)
            {
                Assert.Ignore($"Pixel values need a real GPU with matching sRGB writes; skipped on {deviceType}.");
            }

            Texture2D image = CaptureWithTheRealRenderer();
            try
            {
                AssertColor32Near(image.GetPixel(2, 2), new Color32(128, 128, 128, 255), 1);
                AssertColor32Near(image.GetPixel(90, 50), new Color32(255, 255, 255, 255), 1);
                AssertColor32Near(image.GetPixel(20, 50), new Color32(255, 0, 0, 255), 1);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        private Texture2D CaptureWithTheRealRenderer()
        {
            PrefabScreenshotUseCase useCase = new(null, () => new Vector2(800f, 600f));
            ScreenshotResponse response = useCase.CaptureAsync(CreateRequest(UiPartPath), CancellationToken.None).Result;
            return LoadSavedImage(response);
        }

        private static Texture2D LoadSavedImage(ScreenshotResponse response)
        {
            Texture2D image = new(2, 2);
            if (!image.LoadImage(File.ReadAllBytes(response.Screenshots[0].ImagePath)))
            {
                UnityEngine.Object.DestroyImmediate(image);
                Assert.Fail("The saved PNG could not be decoded.");
            }

            return image;
        }

        private void AssertRejectedWithoutSideEffects<TException>(ScreenshotSchema request, CancellationToken ct)
            where TException : Exception
        {
            RecordingRenderer renderer = new();
            int previewScenesBefore = EditorSceneManager.previewSceneCount;
            PrefabScreenshotUseCase useCase = CreateUseCase(renderer);

            Assert.Throws<TException>(() => useCase.CaptureAsync(request, ct));

            Assert.That(renderer.RenderCount, Is.EqualTo(0));
            Assert.That(Directory.GetFiles(_outputDirectory), Is.Empty);
            Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(previewScenesBefore));
        }

        private ScreenshotResponse Capture(RecordingRenderer renderer, ScreenshotSchema request)
        {
            Task<ScreenshotResponse> task = CreateUseCase(renderer).CaptureAsync(request, CancellationToken.None);
            Assert.That(task.IsCompleted, Is.True);
            return task.Result;
        }

        private static PrefabScreenshotUseCase CreateUseCase(RecordingRenderer renderer)
        {
            return new PrefabScreenshotUseCase(renderer, () => new Vector2(800f, 600f));
        }

        private ScreenshotSchema CreateRequest(string prefabPath)
        {
            return new ScreenshotSchema { PrefabPath = prefabPath, OutputDirectory = _outputDirectory };
        }

        private static void AssertColor(Color expected, Color actual)
        {
            const float tolerance = 0.5f / 255f;
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(tolerance));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(tolerance));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(tolerance));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(tolerance));
        }

        private static void AssertColor32Near(Color32 actual, Color32 expected, int tolerance)
        {
            Assert.That(actual.r, Is.InRange(expected.r - tolerance, expected.r + tolerance), "r");
            Assert.That(actual.g, Is.InRange(expected.g - tolerance, expected.g + tolerance), "g");
            Assert.That(actual.b, Is.InRange(expected.b - tolerance, expected.b + tolerance), "b");
            Assert.That(actual.a, Is.InRange(expected.a - tolerance, expected.a + tolerance), "a");
        }

        /// <summary>
        /// Stands in for the GPU renderer and copies the camera settings it saw, because the camera is destroyed with the preview scene.
        /// </summary>
        private sealed class RecordingRenderer : IPrefabPreviewRenderer
        {
            public bool ThrowOnRender { get; set; }
            public bool ReturnBlank { get; set; }
            public bool FillWithCameraBackground { get; set; }
            public int RenderCount { get; private set; }
            public Texture2D LastReturnedTexture { get; private set; }
            public Color CameraBackgroundColor { get; private set; }
            public CameraClearFlags CameraClearFlags { get; private set; }
            public bool CameraSceneWasPreview { get; private set; }

            public Texture2D Render(Camera camera, Vector2Int size)
            {
                RenderCount++;
                CameraBackgroundColor = camera.backgroundColor;
                CameraClearFlags = camera.clearFlags;
                Scene cameraScene = camera.scene;
                CameraSceneWasPreview = EditorSceneManager.IsPreviewScene(cameraScene);
                if (ThrowOnRender)
                {
                    throw new InvalidOperationException("Simulated rendering failure");
                }

                LastReturnedTexture = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
                LastReturnedTexture.SetPixels32(CreatePixels(camera, size));
                LastReturnedTexture.Apply();
                return LastReturnedTexture;
            }

            // Transparent everywhere except one opaque red pixel in the middle, unless a blank or background-only image is asked for.
            private Color32[] CreatePixels(Camera camera, Vector2Int size)
            {
                Color32 fill = FillWithCameraBackground ? (Color32)camera.backgroundColor : new Color32(0, 0, 0, 0);
                Color32[] pixels = new Color32[size.x * size.y];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = fill;
                }

                if (!ReturnBlank && !FillWithCameraBackground)
                {
                    pixels[size.y / 2 * size.x + size.x / 2] = new Color32(255, 0, 0, 255);
                }

                return pixels;
            }
        }
    }
}
