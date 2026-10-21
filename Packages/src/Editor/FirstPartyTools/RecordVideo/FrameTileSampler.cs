using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Shrinks a captured frame into a small tile and a luminance grid without Unity texture APIs,
    /// so the per-frame analysis runs on CPU arrays that outlive the frame texture.
    /// Pixel rows follow the Unity texture order: the first row is the bottom of the image.
    /// </summary>
    internal static class FrameTileSampler
    {
        internal const int MaxTileWidth = 320;
        internal const int MaxGridColumns = 64;

        internal static (int tileWidth, int tileHeight) ResolveTileSize(int width, int height)
        {
            Debug.Assert(width > 0 && height > 0, "frame size must be positive.");

            int tileWidth = Mathf.Min(MaxTileWidth, width);
            int tileHeight = Mathf.Max(1, Mathf.RoundToInt((float)height * tileWidth / width));
            return (tileWidth, tileHeight);
        }

        internal static (int gridColumns, int gridRows) ResolveGridSize(int tileWidth, int tileHeight)
        {
            Debug.Assert(tileWidth > 0 && tileHeight > 0, "tile size must be positive.");

            int gridColumns = Mathf.Min(MaxGridColumns, tileWidth);
            int gridRows = Mathf.Max(1, Mathf.RoundToInt((float)gridColumns * tileHeight / tileWidth));
            return (gridColumns, gridRows);
        }

        internal static void SampleTile(
            Color32[] source,
            int width,
            int height,
            Color32[] tile,
            int tileWidth,
            int tileHeight)
        {
            Debug.Assert(source.Length >= width * height, "source must hold width * height pixels.");
            Debug.Assert(tile.Length >= tileWidth * tileHeight, "tile must hold tileWidth * tileHeight pixels.");

            for (int y = 0; y < tileHeight; y++)
            {
                int sourceRow = y * height / tileHeight * width;
                int tileRow = y * tileWidth;
                for (int x = 0; x < tileWidth; x++)
                {
                    tile[tileRow + x] = source[sourceRow + x * width / tileWidth];
                }
            }
        }

        internal static void ComputeLuminanceGrid(
            Color32[] tile,
            int tileWidth,
            int tileHeight,
            byte[] grid,
            int gridColumns,
            int gridRows)
        {
            Debug.Assert(tile.Length >= tileWidth * tileHeight, "tile must hold tileWidth * tileHeight pixels.");
            Debug.Assert(grid.Length >= gridColumns * gridRows, "grid must hold gridColumns * gridRows cells.");

            for (int row = 0; row < gridRows; row++)
            {
                int y0 = row * tileHeight / gridRows;
                int y1 = (row + 1) * tileHeight / gridRows;
                for (int column = 0; column < gridColumns; column++)
                {
                    int x0 = column * tileWidth / gridColumns;
                    int x1 = (column + 1) * tileWidth / gridColumns;
                    grid[row * gridColumns + column] = CellLuminance(tile, tileWidth, x0, x1, y0, y1);
                }
            }
        }

        private static byte CellLuminance(Color32[] tile, int tileWidth, int x0, int x1, int y0, int y1)
        {
            // A tile smaller than the grid leaves some cells without pixels; the nearest pixel
            // keeps every cell measurable instead of dividing by zero.
            if (x1 <= x0 || y1 <= y0)
            {
                return Luminance(tile[y0 * tileWidth + x0]);
            }

            int sum = 0;
            for (int y = y0; y < y1; y++)
            {
                int rowStart = y * tileWidth;
                for (int x = x0; x < x1; x++)
                {
                    sum += Luminance(tile[rowStart + x]);
                }
            }

            return (byte)(sum / ((x1 - x0) * (y1 - y0)));
        }

        private static byte Luminance(Color32 color)
        {
            return (byte)((color.r * 299 + color.g * 587 + color.b * 114) / 1000);
        }
    }
}
