using NUnit.Framework;
using UnityEngine;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Verifies the tile downsampling and luminance grid used to measure frame changes.
    /// </summary>
    public sealed class FrameTileSamplerTests
    {
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);

        /// <summary>
        /// What: a source narrower than the tile cap keeps its own size as the tile size.
        /// </summary>
        [Test]
        public void ResolveTileSize_WhenSourceIsSmall_KeepsSourceSize()
        {
            (int tileWidth, int tileHeight) size = FrameTileSampler.ResolveTileSize(2, 2);

            Assert.That(size.tileWidth, Is.EqualTo(2));
            Assert.That(size.tileHeight, Is.EqualTo(2));
        }

        /// <summary>
        /// What: a wide source is capped at 320 pixels and keeps its aspect ratio.
        /// </summary>
        [Test]
        public void ResolveTileSize_WhenSourceIsWide_CapsWidthAndKeepsAspect()
        {
            (int tileWidth, int tileHeight) size = FrameTileSampler.ResolveTileSize(1280, 720);

            Assert.That(size.tileWidth, Is.EqualTo(320));
            Assert.That(size.tileHeight, Is.EqualTo(180));
        }

        /// <summary>
        /// What: the grid is at most 64 columns and its rows follow the tile aspect ratio.
        /// </summary>
        [Test]
        public void ResolveGridSize_CapsColumnsAndKeepsAspect()
        {
            (int gridColumns, int gridRows) grid = FrameTileSampler.ResolveGridSize(320, 180);

            Assert.That(grid.gridColumns, Is.EqualTo(64));
            Assert.That(grid.gridRows, Is.EqualTo(36));
        }

        /// <summary>
        /// What: ForFrame derives the tile and grid sizes from the frame size.
        /// </summary>
        [Test]
        public void ForFrame_UsesResolvedTileAndGridSizes()
        {
            FrameTileSampler sampler = FrameTileSampler.ForFrame(1280, 720);

            Assert.That(sampler.TileWidth, Is.EqualTo(320));
            Assert.That(sampler.TileHeight, Is.EqualTo(180));
            Assert.That(sampler.GridColumns, Is.EqualTo(64));
            Assert.That(sampler.GridRows, Is.EqualTo(36));
        }

        /// <summary>
        /// What: sampling a 2x2 source into a 2x2 tile copies every pixel unchanged.
        /// </summary>
        [Test]
        public void Sample_WhenSameSize_CopiesPixels()
        {
            Color32[] source =
            {
                new Color32(1, 0, 0, 255),
                new Color32(2, 0, 0, 255),
                new Color32(3, 0, 0, 255),
                new Color32(4, 0, 0, 255)
            };
            Color32[] tile = new Color32[4];

            new FrameTileSampler(2, 2, 2, 2, 2, 2).Sample(source, tile, new byte[4]);

            Assert.That(tile, Is.EqualTo(source));
        }

        /// <summary>
        /// What: downsampling 4x4 to 2x2 picks the nearest source pixel at even coordinates.
        /// </summary>
        [Test]
        public void Sample_WhenDownsampling_PicksNearestPixel()
        {
            Color32[] source = new Color32[16];
            for (int i = 0; i < source.Length; i++)
            {
                source[i] = new Color32((byte)i, 0, 0, 255);
            }

            Color32[] tile = new Color32[4];

            new FrameTileSampler(4, 4, 2, 2, 2, 2).Sample(source, tile, new byte[4]);

            Assert.That(tile[0].r, Is.EqualTo(0));
            Assert.That(tile[1].r, Is.EqualTo(2));
            Assert.That(tile[2].r, Is.EqualTo(8));
            Assert.That(tile[3].r, Is.EqualTo(10));
        }

        /// <summary>
        /// What: a single-color frame gives the same luminance in every grid cell.
        /// </summary>
        [Test]
        public void Sample_WhenSingleColor_AllCellsEqual()
        {
            Color32[] source = Fill(8, 8, new Color32(100, 100, 100, 255));
            byte[] grid = new byte[4];

            new FrameTileSampler(8, 8, 8, 8, 2, 2).Sample(source, new Color32[64], grid);

            Assert.That(grid, Is.All.EqualTo(100));
        }

        /// <summary>
        /// What: a cell averages the luminance of the tile pixels inside it.
        /// </summary>
        [Test]
        public void Sample_AveragesLuminanceInsideCell()
        {
            Color32[] source = { Black, White, Black, White };
            byte[] grid = new byte[1];

            new FrameTileSampler(2, 2, 2, 2, 1, 1).Sample(source, new Color32[4], grid);

            Assert.That(grid[0], Is.EqualTo(127));
        }

        /// <summary>
        /// What: a frame whose upper half is white keeps that half in the upper grid rows (rows count from the bottom).
        /// </summary>
        [Test]
        public void Sample_KeepsRowOrderFromBottom()
        {
            Color32[] source = new Color32[16];
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    source[y * 4 + x] = y >= 2 ? White : Black;
                }
            }

            byte[] grid = new byte[4];

            new FrameTileSampler(4, 4, 4, 4, 2, 2).Sample(source, new Color32[16], grid);

            Assert.That(grid, Is.EqualTo(new byte[] { 0, 0, 255, 255 }));
        }

        /// <summary>
        /// What: a grid larger than the tile falls back to the nearest pixel instead of an empty average.
        /// </summary>
        [Test]
        public void Sample_WhenGridLargerThanTile_UsesNearestPixel()
        {
            Color32[] source = { Black, White };
            byte[] grid = new byte[4];

            new FrameTileSampler(2, 1, 2, 1, 4, 1).Sample(source, new Color32[2], grid);

            Assert.That(grid, Is.EqualTo(new byte[] { 0, 0, 255, 255 }));
        }

        /// <summary>
        /// What: sampling a frame twice gives the same grid, so cell sums do not carry over between frames.
        /// </summary>
        [Test]
        public void Sample_Twice_DoesNotCarryOverSums()
        {
            FrameTileSampler sampler = new FrameTileSampler(2, 2, 2, 2, 1, 1);
            Color32[] source = Fill(2, 2, new Color32(100, 100, 100, 255));
            byte[] grid = new byte[1];

            sampler.Sample(source, new Color32[4], grid);
            sampler.Sample(source, new Color32[4], grid);

            Assert.That(grid[0], Is.EqualTo(100));
        }

        private static Color32[] Fill(int width, int height, Color32 color)
        {
            Color32[] pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            return pixels;
        }
    }
}
