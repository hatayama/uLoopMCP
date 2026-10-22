using System;
using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Pixel checks and background compositing applied to a rendered prefab image before it is saved.
    /// </summary>
    internal static class PrefabImagePixels
    {
        private const int ClearColorTolerance = 1;

        /// <summary>
        /// Returns true when at least one visible pixel differs from the clear color by more than one step.
        /// </summary>
        internal static bool HasDrawnPixel(Color32[] pixels, Color32 clearColor)
        {
            foreach (Color32 pixel in pixels)
            {
                if (pixel.a == 0)
                {
                    continue;
                }

                if (DiffersFrom(pixel, clearColor))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Composites an opaque background under every pixel that is not fully opaque.
        /// Pixels drawn over a transparent clear hold premultiplied color, so this is a premultiplied over;
        /// linearBlending blends in linear space, matching how the GPU blends in a Linear project.
        /// </summary>
        internal static void FillBackground(Color32[] pixels, Color32 background, bool linearBlending)
        {
            if (background.a != 255)
            {
                throw new ArgumentException($"Background must be opaque, got alpha {background.a}", nameof(background));
            }

            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                if (pixel.a == 255)
                {
                    continue;
                }

                float coverage = pixel.a / 255f;
                pixels[i] = new Color32(
                    Over(pixel.r, background.r, coverage, linearBlending),
                    Over(pixel.g, background.g, coverage, linearBlending),
                    Over(pixel.b, background.b, coverage, linearBlending),
                    255);
            }
        }

        private static byte Over(byte source, byte background, float coverage, bool linearBlending)
        {
            float s = ToBlend(source / 255f, linearBlending);
            float b = ToBlend(background / 255f, linearBlending);
            float blended = Mathf.Min(1f, s + b * (1f - coverage));
            return (byte)Mathf.RoundToInt(FromBlend(blended, linearBlending) * 255f);
        }

        private static float ToBlend(float value, bool linearBlending)
        {
            return linearBlending ? Mathf.GammaToLinearSpace(value) : value;
        }

        private static float FromBlend(float value, bool linearBlending)
        {
            return linearBlending ? Mathf.LinearToGammaSpace(value) : value;
        }

        private static bool DiffersFrom(Color32 pixel, Color32 clearColor)
        {
            return Mathf.Abs(pixel.r - clearColor.r) > ClearColorTolerance
                || Mathf.Abs(pixel.g - clearColor.g) > ClearColorTolerance
                || Mathf.Abs(pixel.b - clearColor.b) > ClearColorTolerance
                || Mathf.Abs(pixel.a - clearColor.a) > ClearColorTolerance;
        }
    }
}
