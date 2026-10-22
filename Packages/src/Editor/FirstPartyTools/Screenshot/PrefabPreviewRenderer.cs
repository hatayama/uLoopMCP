using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Renders a camera into a new texture of the given size; the caller owns and destroys the texture.
    /// </summary>
    internal interface IPrefabPreviewRenderer
    {
        Texture2D Render(Camera camera, Vector2Int size);
    }

    /// <summary>
    /// Renders a preview-scene camera off screen through a temporary RenderTexture.
    /// </summary>
    internal sealed class PrefabPreviewRenderer : IPrefabPreviewRenderer
    {
        public Texture2D Render(Camera camera, Vector2Int size)
        {
            RenderTextureDescriptor descriptor = new(size.x, size.y, RenderTextureFormat.ARGB32, 24);
            // Keeps PNG values equal to the requested sRGB colors in Linear projects; Gamma projects ignore it.
            descriptor.sRGB = true;
            RenderTexture target = new(descriptor);
            RenderTexture previousActive = RenderTexture.active;
            try
            {
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
