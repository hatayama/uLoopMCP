using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Shrinks a captured frame into a small tile and a luminance grid without Unity texture APIs,
    /// so the per-frame analysis runs on CPU arrays that outlive the frame texture.
    /// Pixel rows follow the Unity texture order: the first row is the bottom of the image.
    /// </summary>
    internal sealed class FrameTileSampler
    {
        internal const int MaxTileWidth = 320;
        internal const int MaxGridColumns = 64;

        private readonly int _width;
        private readonly int _height;
        private readonly int[] _sourceColumnOfTileX;
        private readonly int[] _sourceRowStartOfTileY;
        private readonly int[] _cellColumnOfTileX;
        private readonly int[] _cellRowOfTileY;
        private readonly int[] _cellPixelCounts;
        private readonly int[] _cellFallbackPixels;
        private readonly long[] _cellSums;

        /// <summary>
        /// Precomputes every index the per-frame pass needs, because the sizes do not change during a
        /// recording and integer division per pixel dominated the per-frame cost.
        /// </summary>
        internal FrameTileSampler(int width, int height, int tileWidth, int tileHeight, int gridColumns, int gridRows)
        {
            Debug.Assert(width > 0 && height > 0, "frame size must be positive.");
            Debug.Assert(tileWidth > 0 && tileHeight > 0, "tile size must be positive.");
            Debug.Assert(gridColumns > 0 && gridRows > 0, "grid size must be positive.");

            _width = width;
            _height = height;
            TileWidth = tileWidth;
            TileHeight = tileHeight;
            GridColumns = gridColumns;
            GridRows = gridRows;

            _sourceColumnOfTileX = new int[tileWidth];
            for (int x = 0; x < tileWidth; x++)
            {
                _sourceColumnOfTileX[x] = x * width / tileWidth;
            }

            _sourceRowStartOfTileY = new int[tileHeight];
            for (int y = 0; y < tileHeight; y++)
            {
                _sourceRowStartOfTileY[y] = y * height / tileHeight * width;
            }

            _cellColumnOfTileX = AssignCells(tileWidth, gridColumns);
            _cellRowOfTileY = AssignCells(tileHeight, gridRows);

            int cellCount = gridColumns * gridRows;
            _cellPixelCounts = new int[cellCount];
            _cellFallbackPixels = new int[cellCount];
            _cellSums = new long[cellCount];
            for (int y = 0; y < tileHeight; y++)
            {
                for (int x = 0; x < tileWidth; x++)
                {
                    _cellPixelCounts[_cellRowOfTileY[y] * gridColumns + _cellColumnOfTileX[x]]++;
                }
            }

            for (int row = 0; row < gridRows; row++)
            {
                for (int column = 0; column < gridColumns; column++)
                {
                    // A tile smaller than the grid leaves some cells without pixels; the nearest pixel
                    // keeps every cell measurable instead of dividing by zero.
                    int x0 = column * tileWidth / gridColumns;
                    int y0 = row * tileHeight / gridRows;
                    _cellFallbackPixels[row * gridColumns + column] = y0 * tileWidth + x0;
                }
            }
        }

        internal int TileWidth { get; }

        internal int TileHeight { get; }

        internal int GridColumns { get; }

        internal int GridRows { get; }

        internal static FrameTileSampler ForFrame(int width, int height)
        {
            (int tileWidth, int tileHeight) tile = ResolveTileSize(width, height);
            (int gridColumns, int gridRows) grid = ResolveGridSize(tile.tileWidth, tile.tileHeight);
            return new FrameTileSampler(width, height, tile.tileWidth, tile.tileHeight, grid.gridColumns, grid.gridRows);
        }

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

        /// <summary>
        /// Picks the nearest source pixel for every tile pixel and, in the same pass, averages the
        /// luminance of the tile pixels inside each grid cell.
        /// </summary>
        internal void Sample(Color32[] source, Color32[] tile, byte[] grid)
        {
            Debug.Assert(source.Length >= _width * _height, "source must hold width * height pixels.");
            Debug.Assert(tile.Length >= TileWidth * TileHeight, "tile must hold tileWidth * tileHeight pixels.");
            Debug.Assert(grid.Length >= GridColumns * GridRows, "grid must hold gridColumns * gridRows cells.");

            System.Array.Clear(_cellSums, 0, _cellSums.Length);
            for (int y = 0; y < TileHeight; y++)
            {
                int sourceRow = _sourceRowStartOfTileY[y];
                int tileRow = y * TileWidth;
                int cellRow = _cellRowOfTileY[y] * GridColumns;
                for (int x = 0; x < TileWidth; x++)
                {
                    Color32 color = source[sourceRow + _sourceColumnOfTileX[x]];
                    tile[tileRow + x] = color;
                    _cellSums[cellRow + _cellColumnOfTileX[x]] += color.r * 299 + color.g * 587 + color.b * 114;
                }
            }

            for (int cell = 0; cell < _cellSums.Length; cell++)
            {
                int count = _cellPixelCounts[cell];
                grid[cell] = count > 0
                    ? (byte)(_cellSums[cell] / (count * 1000L))
                    : Luminance(tile[_cellFallbackPixels[cell]]);
            }
        }

        private static int[] AssignCells(int pixelCount, int cellCount)
        {
            // Cell c covers pixels [c * pixelCount / cellCount, (c + 1) * pixelCount / cellCount).
            int[] cellOfPixel = new int[pixelCount];
            for (int cell = 0; cell < cellCount; cell++)
            {
                int start = cell * pixelCount / cellCount;
                int end = (cell + 1) * pixelCount / cellCount;
                for (int pixel = start; pixel < end; pixel++)
                {
                    cellOfPixel[pixel] = cell;
                }
            }

            return cellOfPixel;
        }

        private static byte Luminance(Color32 color)
        {
            return (byte)((color.r * 299 + color.g * 587 + color.b * 114) / 1000);
        }
    }
}
