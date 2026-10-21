using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Verifies how tiles are laid out on a contact sheet.
    /// </summary>
    public sealed class ContactSheetComposerTests
    {
        private const int Gutter = 2;
        private static readonly Color32 Background = new Color32(40, 40, 40, 255);

        /// <summary>
        /// What: 32 tiles in 4 columns make 8 rows, and the width includes the gutters between columns.
        /// </summary>
        [Test]
        public void Layout_With32TilesIn4Columns_Has8Rows()
        {
            (int columns, int rows, int width, int height) layout =
                ContactSheetComposer.Layout(32, 4, 10, 6, Gutter);

            Assert.That(layout.rows, Is.EqualTo(8));
            Assert.That(layout.width, Is.EqualTo(4 * 10 + 3 * Gutter));
            Assert.That(layout.height, Is.EqualTo(8 * 6 + 7 * Gutter));
        }

        /// <summary>
        /// What: 10 tiles in 4 columns make 3 rows, and the two empty slots of the last row stay background.
        /// </summary>
        [Test]
        public void Compose_WhenLastRowIsPartial_LeavesEmptySlotsAsBackground()
        {
            List<Color32[]> tiles = new List<Color32[]>();
            for (int i = 0; i < 10; i++)
            {
                tiles.Add(new[] { new Color32(200, 200, 200, 255) });
            }

            Color32[] sheet = ContactSheetComposer.Compose(tiles, 1, 1, 4, Gutter);

            (int columns, int rows, int width, int height) layout =
                ContactSheetComposer.Layout(10, 4, 1, 1, Gutter);
            Assert.That(layout.rows, Is.EqualTo(3));
            // The third tile row is the bottom of the image, which is array row 0.
            Assert.That(sheet[0 * layout.width + 1 * (1 + Gutter)], Is.EqualTo(new Color32(200, 200, 200, 255)));
            Assert.That(sheet[0 * layout.width + 2 * (1 + Gutter)], Is.EqualTo(Background));
            Assert.That(sheet[0 * layout.width + 3 * (1 + Gutter)], Is.EqualTo(Background));
        }

        /// <summary>
        /// What: the first tile lands at the top-left of the image, which is the last row block of the array.
        /// </summary>
        [Test]
        public void Compose_PutsFirstTileAtTopLeft()
        {
            Color32 first = new Color32(255, 0, 0, 255);
            Color32 other = new Color32(0, 0, 255, 255);
            List<Color32[]> tiles = new List<Color32[]>
            {
                new[] { first, first, first, first },
                new[] { other, other, other, other },
                new[] { other, other, other, other }
            };

            Color32[] sheet = ContactSheetComposer.Compose(tiles, 2, 2, 2, Gutter);

            (int columns, int rows, int width, int height) layout =
                ContactSheetComposer.Layout(3, 2, 2, 2, Gutter);
            int topRow = layout.height - 1;
            Assert.That(sheet[topRow * layout.width], Is.EqualTo(first));
            Assert.That(sheet[(topRow - 1) * layout.width + 1], Is.EqualTo(first));
            Assert.That(sheet[0], Is.EqualTo(other));
        }
    
        /// <summary>
        /// What: a tile keeps its own row order on the sheet, so the bottom row of a tile is not flipped to its top.
        /// </summary>
        [Test]
        public void Compose_KeepsRowOrderInsideTile()
        {
            Color32 bottom = new Color32(255, 0, 0, 255);
            Color32 top = new Color32(0, 0, 255, 255);
            List<Color32[]> tiles = new List<Color32[]> { new[] { bottom, top } };

            Color32[] sheet = ContactSheetComposer.Compose(tiles, 1, 2, 1, Gutter);

            (int columns, int rows, int width, int height) layout =
                ContactSheetComposer.Layout(1, 1, 1, 2, Gutter);
            Assert.That(sheet[0], Is.EqualTo(bottom));
            Assert.That(sheet[layout.width], Is.EqualTo(top));
        }
    }
}
