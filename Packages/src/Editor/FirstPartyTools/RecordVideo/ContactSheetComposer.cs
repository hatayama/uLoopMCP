using System.Collections.Generic;

using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Lays same-sized tiles out on a grid, first tile at the top-left, reading left to right and
    /// top to bottom. The sheet keeps the Unity texture row order (first row is the bottom),
    /// so it can be PNG-encoded the same way as a frame texture.
    /// </summary>
    internal static class ContactSheetComposer
    {
        internal const int DefaultGutter = 2;
        private static readonly Color32 Background = new Color32(40, 40, 40, 255);

        internal static (int columns, int rows, int width, int height) Layout(
            int tileCount,
            int columns,
            int tileWidth,
            int tileHeight,
            int gutter)
        {
            Debug.Assert(tileCount > 0, "a sheet needs at least one tile.");
            Debug.Assert(columns > 0, "columns must be positive.");

            int rows = (tileCount + columns - 1) / columns;
            int width = columns * tileWidth + (columns - 1) * gutter;
            int height = rows * tileHeight + (rows - 1) * gutter;
            return (columns, rows, width, height);
        }

        internal static Color32[] Compose(
            IReadOnlyList<Color32[]> tiles,
            int tileWidth,
            int tileHeight,
            int columns,
            int gutter)
        {
            Debug.Assert(tiles != null && tiles.Count > 0, "a sheet needs at least one tile.");

            (int columns, int rows, int width, int height) layout =
                Layout(tiles.Count, columns, tileWidth, tileHeight, gutter);
            Color32[] sheet = new Color32[layout.width * layout.height];
            for (int i = 0; i < sheet.Length; i++)
            {
                sheet[i] = Background;
            }

            for (int i = 0; i < tiles.Count; i++)
            {
                int tileColumn = i % columns;
                int tileRow = i / columns;
                int originX = tileColumn * (tileWidth + gutter);
                // Sheet rows count from the bottom, so the first tile row sits at the end of the array.
                int originY = layout.height - (tileRow + 1) * tileHeight - tileRow * gutter;
                CopyTile(tiles[i], tileWidth, tileHeight, sheet, layout.width, originX, originY);
            }

            return sheet;
        }

        private static void CopyTile(
            Color32[] tile,
            int tileWidth,
            int tileHeight,
            Color32[] sheet,
            int sheetWidth,
            int originX,
            int originY)
        {
            Debug.Assert(tile.Length == tileWidth * tileHeight, "every tile must have the same size.");

            for (int y = 0; y < tileHeight; y++)
            {
                System.Array.Copy(tile, y * tileWidth, sheet, (originY + y) * sheetWidth + originX, tileWidth);
            }
        }
    }
}
