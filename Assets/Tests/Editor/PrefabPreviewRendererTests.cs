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
    }
}
