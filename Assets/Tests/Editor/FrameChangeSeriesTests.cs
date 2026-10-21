using NUnit.Framework;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Verifies the per-frame change measurements and their summary.
    /// </summary>
    public sealed class FrameChangeSeriesTests
    {
        private const int Columns = 4;
        private const int Rows = 4;

        /// <summary>
        /// What: the same grid three times measures no change at all.
        /// </summary>
        [Test]
        public void Add_WhenGridRepeats_MeasuresNoChange()
        {
            FrameChangeSeries series = CreateSeries();
            byte[] grid = Grid(0);

            series.Add(0, 0.0, grid);
            series.Add(1, 0.1, grid);
            FrameChangeSample third = series.Add(2, 0.2, grid);

            Assert.That(third.ChangedCellsVsPrevious, Is.EqualTo(0));
            Assert.That(third.MeanVsPrevious, Is.EqualTo(0.0));
            FrameChangeSummary summary = series.Summarize();
            Assert.That(summary.FramesChangedVsPrevious, Is.EqualTo(0));
            Assert.That(summary.MeanVsPrevious, Is.EqualTo(0.0));
        }

        /// <summary>
        /// What: one cell changing by the threshold counts as a changed cell but not as a changed frame.
        /// </summary>
        [Test]
        public void Add_WhenOneCellChanges_CountsCellButNotFrame()
        {
            FrameChangeSeries series = CreateSeries();
            byte[] changed = Grid(0);
            changed[5] = 8;

            series.Add(0, 0.0, Grid(0));
            FrameChangeSample sample = series.Add(1, 0.1, changed);

            Assert.That(sample.ChangedCellsVsPrevious, Is.EqualTo(1));
            Assert.That(series.Summarize().FramesChangedVsPrevious, Is.EqualTo(0));
        }

        /// <summary>
        /// What: a difference of 7, one below the threshold, is not counted as a changed cell.
        /// </summary>
        [Test]
        public void Add_WhenDifferenceBelowThreshold_DoesNotCountCell()
        {
            FrameChangeSeries series = CreateSeries();

            series.Add(0, 0.0, Grid(0));
            FrameChangeSample sample = series.Add(1, 0.1, Grid(7));

            Assert.That(sample.ChangedCellsVsPrevious, Is.EqualTo(0));
            Assert.That(sample.MeanVsPrevious, Is.EqualTo(7.0 / 255.0).Within(1e-9));
        }

        /// <summary>
        /// What: A,B,A,B with ten changed cells counts frames that differ from the previous frame but match the one two before.
        /// </summary>
        [Test]
        public void Add_WhenAlternating_CountsMatchingTwoBack()
        {
            FrameChangeSeries series = CreateSeries();
            byte[] a = Grid(0);
            byte[] b = Grid(0);
            for (int i = 0; i < 10; i++)
            {
                b[i] = 100;
            }

            FrameChangeSample first = series.Add(0, 0.0, a);
            FrameChangeSample second = series.Add(1, 0.1, b);
            FrameChangeSample third = series.Add(2, 0.2, a);
            FrameChangeSample fourth = series.Add(3, 0.3, b);

            Assert.That(first.ChangedCellsVsPrevious, Is.EqualTo(0));
            Assert.That(second.ChangedCellsVsPrevious, Is.EqualTo(10));
            Assert.That(third.ChangedCellsVsPrevious, Is.EqualTo(10));
            Assert.That(third.ChangedCellsVsTwoBack, Is.EqualTo(0));
            Assert.That(fourth.ChangedCellsVsTwoBack, Is.EqualTo(0));
            FrameChangeSummary summary = series.Summarize();
            Assert.That(summary.FramesChangedVsPrevious, Is.EqualTo(3));
            Assert.That(summary.FramesChangedVsPreviousMatchingTwoBack, Is.EqualTo(2));
        }

        /// <summary>
        /// What: the largest change is the frame with the most changed cells, and the earlier one wins a tie.
        /// </summary>
        [Test]
        public void Summarize_PicksLargestChangeAndPrefersEarlierOnTie()
        {
            FrameChangeSeries series = CreateSeries();

            series.Add(10, 0.0, Grid(0));
            series.Add(11, 0.1, Grid(50));
            series.Add(12, 0.2, Grid(100));
            series.Add(13, 0.3, Grid(100));

            LargestFrameChange largest = series.Summarize().LargestChange;
            Assert.That(largest.CapturedIndex, Is.EqualTo(1));
            Assert.That(largest.EncodedFrame, Is.EqualTo(11));
            Assert.That(largest.Seconds, Is.EqualTo(0.1));
            Assert.That(largest.ChangedCellsVsPrevious, Is.EqualTo(Columns * Rows));
            Assert.That(largest.MeanVsPrevious, Is.EqualTo(50.0 / 255.0).Within(1e-9));
        }

        /// <summary>
        /// What: the summary mean skips the first frame, which has nothing to compare against.
        /// </summary>
        [Test]
        public void Summarize_MeanSkipsFirstFrame()
        {
            FrameChangeSeries series = CreateSeries();

            series.Add(0, 0.0, Grid(0));
            series.Add(1, 0.1, Grid(51));
            series.Add(2, 0.2, Grid(51));

            Assert.That(series.Summarize().MeanVsPrevious, Is.EqualTo(0.1).Within(1e-9));
        }

        /// <summary>
        /// What: with one frame or none there is no largest change.
        /// </summary>
        [Test]
        public void Summarize_WithOneFrame_HasNoLargestChange()
        {
            FrameChangeSeries series = CreateSeries();

            Assert.That(series.Summarize().LargestChange, Is.Null);
            series.Add(0, 0.0, Grid(0));
            FrameChangeSummary summary = series.Summarize();
            Assert.That(summary.LargestChange, Is.Null);
            Assert.That(summary.CapturedFrameCount, Is.EqualTo(1));
        }

        /// <summary>
        /// What: only the one cell that alternates A,B,A,B is reported, counted twice.
        /// </summary>
        [Test]
        public void Summarize_ReportsOnlyTheAlternatingCell()
        {
            FrameChangeSeries series = CreateSeries();
            byte[] a = Grid(0);
            byte[] b = Grid(0);
            b[6] = 200;

            series.Add(0, 0.0, a);
            series.Add(1, 0.1, b);
            series.Add(2, 0.2, a);
            series.Add(3, 0.3, b);

            FrameChangeSummary summary = series.Summarize();
            Assert.That(summary.TopAlternatingCells.Count, Is.EqualTo(1));
            Assert.That(summary.TopAlternatingCells[0].Column, Is.EqualTo(2));
            Assert.That(summary.TopAlternatingCells[0].Row, Is.EqualTo(1));
            Assert.That(summary.TopAlternatingCells[0].Count, Is.EqualTo(2));
        }

        /// <summary>
        /// What: grid row 1 (the top row of a 2x2 grid) maps to TopLeftY 0 and row 0 to TopLeftY 25 in a 100x50 video.
        /// </summary>
        [Test]
        public void Summarize_CellRectanglesUseTopLeftOrigin()
        {
            FrameChangeSeries series = new FrameChangeSeries(100, 50, 2, 2);
            byte[] a = { 0, 0, 0, 0 };
            byte[] b = { 200, 0, 200, 0 };

            series.Add(0, 0.0, a);
            series.Add(1, 0.1, b);
            series.Add(2, 0.2, a);

            FrameChangeSummary summary = series.Summarize();
            Assert.That(summary.TopAlternatingCells.Count, Is.EqualTo(2));
            AlternatingCell bottom = summary.TopAlternatingCells[0];
            AlternatingCell top = summary.TopAlternatingCells[1];
            Assert.That(bottom.Row, Is.EqualTo(0));
            Assert.That(bottom.TopLeftX, Is.EqualTo(0));
            Assert.That(bottom.TopLeftY, Is.EqualTo(25));
            Assert.That(bottom.Width, Is.EqualTo(50));
            Assert.That(bottom.Height, Is.EqualTo(25));
            Assert.That(top.Row, Is.EqualTo(1));
            Assert.That(top.TopLeftY, Is.EqualTo(0));
            Assert.That(top.Height, Is.EqualTo(25));
        }

        /// <summary>
        /// What: changing the grid passed in after Add does not change what the series compares against.
        /// </summary>
        [Test]
        public void Add_CopiesGridSoCallerCanReuseBuffer()
        {
            FrameChangeSeries series = CreateSeries();
            byte[] buffer = Grid(0);

            series.Add(0, 0.0, buffer);
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = 100;
            }

            FrameChangeSample sample = series.Add(1, 0.1, buffer);

            Assert.That(sample.ChangedCellsVsPrevious, Is.EqualTo(Columns * Rows));
        }

        private static FrameChangeSeries CreateSeries()
        {
            return new FrameChangeSeries(40, 40, Columns, Rows);
        }

        private static byte[] Grid(byte value)
        {
            byte[] grid = new byte[Columns * Rows];
            for (int i = 0; i < grid.Length; i++)
            {
                grid[i] = value;
            }

            return grid;
        }
    }
}
