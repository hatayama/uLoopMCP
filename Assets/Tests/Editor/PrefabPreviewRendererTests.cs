using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Verifies the off-screen prefab renderer returns a texture of the requested size and restores the render state it borrowed.
    /// </summary>
    public sealed class PrefabPreviewRendererTests
    {
        /// <summary>
        /// Verifies Render returns a texture of the requested size and restores the active RenderTexture and the camera target.
        /// </summary>
        [Test]
        public void Render_ReturnsTheRequestedSizeAndRestoresRenderState()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("Rendering needs a graphics device; skipped on the Null device.");
            }

            Scene scene = EditorSceneManager.NewPreviewScene();
            RenderTexture callerTarget = RenderTexture.GetTemporary(8, 8);
            RenderTexture previousActive = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                GameObject cameraObject = new("Camera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.scene = scene;
                RenderTexture.active = callerTarget;

                texture = new PrefabPreviewRenderer().Render(camera, new Vector2Int(64, 32));

                Assert.That(texture.width, Is.EqualTo(64));
                Assert.That(texture.height, Is.EqualTo(32));
                Assert.That(RenderTexture.active, Is.SameAs(callerTarget));
                Assert.That(camera.targetTexture, Is.Null);
            }
            finally
            {
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(callerTarget);
                if (texture != null)
                {
                    Object.DestroyImmediate(texture);
                }

                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        /// <summary>
        /// Verifies a camera cleared to an opaque solid color is reported as cleared by the pipeline.
        /// </summary>
        [Test]
        public void ClearsBackground_WithACameraClearedToAnOpaqueColor_ReturnsTrue()
        {
            IgnoreWithoutARealGpu();
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                Camera camera = CreateSolidColorCamera(scene);

                bool clears = new PrefabPreviewRenderer().ClearsBackground(camera, new Vector2Int(16, 16));

                Assert.That(clears, Is.True);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        /// <summary>
        /// Verifies the probe render leaves the camera's culling mask as it found it, so the real render still draws the prefab.
        /// </summary>
        [Test]
        public void ClearsBackground_RestoresTheCullingMask()
        {
            IgnoreWithoutARealGpu();
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                Camera camera = CreateSolidColorCamera(scene);
                camera.cullingMask = 1 << 5;

                new PrefabPreviewRenderer().ClearsBackground(camera, new Vector2Int(16, 16));

                Assert.That(camera.cullingMask, Is.EqualTo(1 << 5));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void IgnoreWithoutARealGpu()
        {
            GraphicsDeviceType device = SystemInfo.graphicsDeviceType;
            if (device == GraphicsDeviceType.Null || device == GraphicsDeviceType.OpenGLCore)
            {
                Assert.Ignore("Reading rendered pixels needs a real GPU; skipped on the Null and OpenGLCore devices.");
            }
        }

        private static Camera CreateSolidColorCamera(Scene scene)
        {
            GameObject cameraObject = new("Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            return camera;
        }
    }
}
