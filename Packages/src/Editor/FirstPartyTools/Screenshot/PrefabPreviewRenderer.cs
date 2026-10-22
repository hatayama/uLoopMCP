using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Renders a camera into a new texture of the given size; the caller owns and destroys the texture.
    /// </summary>
    internal interface IPrefabPreviewRenderer
    {
        Texture2D Render(Camera camera, Vector2Int size);

        /// <summary>
        /// Renders the camera with nothing visible and returns true when the pipeline painted every pixel opaque,
        /// which means it clears this camera to its background color.
        /// </summary>
        bool ClearsBackground(Camera camera, Vector2Int size);
    }

    /// <summary>
    /// Renders a preview-scene camera off screen through a temporary RenderTexture.
    /// </summary>
    internal sealed class PrefabPreviewRenderer : IPrefabPreviewRenderer
    {
        public Texture2D Render(Camera camera, Vector2Int size)
        {
            return RenderToTexture(camera, size);
        }

        public bool ClearsBackground(Camera camera, Vector2Int size)
        {
            int savedMask = camera.cullingMask;
            // A culling mask of 0 hides the prefab, so the probe shows only what the pipeline itself paints.
            camera.cullingMask = 0;
            Texture2D probe = null;
            try
            {
                probe = RenderToTexture(camera, size);
                foreach (Color32 pixel in probe.GetPixels32())
                {
                    if (pixel.a != 255)
                    {
                        return false;
                    }
                }

                return true;
            }
            finally
            {
                // Restored so the real render that follows draws the prefab.
                camera.cullingMask = savedMask;
                if (probe != null)
                {
                    Object.DestroyImmediate(probe);
                }
            }
        }

        private static Texture2D RenderToTexture(Camera camera, Vector2Int size)
        {
            RenderTextureDescriptor descriptor = new(size.x, size.y, RenderTextureFormat.ARGB32, 24);
            // Keeps PNG values equal to the requested sRGB colors in Linear projects; Gamma projects ignore it.
            descriptor.sRGB = true;
            RenderTexture target = new(descriptor);
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                // A new RenderTexture holds undefined contents, so pixels the pipeline does not paint are made alpha 0 first.
                RenderTexture.active = target;
                GL.Clear(true, true, Color.clear);
                camera.targetTexture = target;
                // Screen-space-camera Canvases lay out against the camera's pixel size, so they update after the target is set.
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                Texture2D texture = new(size.x, size.y, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                texture.Apply();
                return texture;
            }
            finally
            {
                RenderTexture.active = previousActive;
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }
}
