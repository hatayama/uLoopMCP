using System;
using System.Collections.Generic;
using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Accumulates how much each captured frame's luminance grid differs from the previous frame
    /// and the frame two before, plus a per-cell count of "changed against the previous frame
    /// but back to the frame two before", which locates one-frame alternation.
    /// Grid rows follow the Unity texture order (row 0 is the bottom of the image).
    /// </summary>
    internal sealed class FrameChangeSeries
    {
        internal const int CellChangedThreshold = 8;
        internal const int ChangedFrameMinCells = 2;
        internal const int MatchingTwoBackMaxCells = 1;
        internal const int TopAlternatingCellCount = 3;

        private readonly int _videoWidth;
        private readonly int _videoHeight;
        private readonly int _gridColumns;
        private readonly int _gridRows;
        private readonly int[] _alternationCounts;
        private readonly List<FrameChangeSample> _samples = new List<FrameChangeSample>();
        private byte[] _previous;
        private byte[] _twoBack;

        internal FrameChangeSeries(int videoWidth, int videoHeight, int gridColumns, int gridRows)
        {
            Debug.Assert(videoWidth > 0 && videoHeight > 0, "video size must be positive.");
            Debug.Assert(gridColumns > 0 && gridRows > 0, "grid size must be positive.");

            _videoWidth = videoWidth;
            _videoHeight = videoHeight;
            _gridColumns = gridColumns;
            _gridRows = gridRows;
            _alternationCounts = new int[gridColumns * gridRows];
        }

        internal IReadOnlyList<FrameChangeSample> Samples => _samples;

        internal FrameChangeSample Add(int encodedFrame, double seconds, byte[] grid)
        {
            Debug.Assert(grid.Length == _alternationCounts.Length, "grid must match the series grid size.");

            FrameChangeSample sample = new FrameChangeSample
            {
                CapturedIndex = _samples.Count,
                EncodedFrame = encodedFrame,
                Seconds = seconds
            };
            (sample.MeanVsPrevious, sample.ChangedCellsVsPrevious) = Compare(grid, _previous);
            (sample.MeanVsTwoBack, sample.ChangedCellsVsTwoBack) = Compare(grid, _twoBack);
            CountAlternations(grid);

            _samples.Add(sample);
            // The caller reuses its grid buffer for the next frame, so keep a copy.
            byte[] copy = new byte[grid.Length];
            Array.Copy(grid, copy, grid.Length);
            _twoBack = _previous;
            _previous = copy;
            return sample;
        }

        internal FrameChangeSummary Summarize()
        {
            return new FrameChangeSummary
            {
                CapturedFrameCount = _samples.Count,
                MeanVsPrevious = MeanOfComparedFrames(),
                LargestChange = FindLargestChange(),
                FramesChangedVsPrevious = CountChangedFrames(false),
                FramesChangedVsPreviousMatchingTwoBack = CountChangedFrames(true),
                TopAlternatingCells = TopAlternatingCells(),
                GridColumns = _gridColumns,
                GridRows = _gridRows,
                CellChangedThreshold = CellChangedThreshold
            };
        }

        private static (double mean, int changedCells) Compare(byte[] grid, byte[] other)
        {
            if (other == null)
            {
                return (0.0, 0);
            }

            long sum = 0;
            int changed = 0;
            for (int i = 0; i < grid.Length; i++)
            {
                int difference = Math.Abs(grid[i] - other[i]);
                sum += difference;
                if (difference >= CellChangedThreshold)
                {
                    changed++;
                }
            }

            return ((double)sum / grid.Length / 255.0, changed);
        }

        private void CountAlternations(byte[] grid)
        {
            if (_twoBack == null)
            {
                return;
            }

            for (int i = 0; i < grid.Length; i++)
            {
                bool changedVsPrevious = Math.Abs(grid[i] - _previous[i]) >= CellChangedThreshold;
                bool matchesTwoBack = Math.Abs(grid[i] - _twoBack[i]) < CellChangedThreshold;
                if (changedVsPrevious && matchesTwoBack)
                {
                    _alternationCounts[i]++;
                }
            }
        }

        private double MeanOfComparedFrames()
        {
            // The first frame has nothing to compare against, so averaging its zero would dilute the mean.
            if (_samples.Count < 2)
            {
                return 0.0;
            }

            double sum = 0.0;
            for (int i = 1; i < _samples.Count; i++)
            {
                sum += _samples[i].MeanVsPrevious;
            }

            return sum / (_samples.Count - 1);
        }

        private LargestFrameChange FindLargestChange()
        {
            if (_samples.Count < 2)
            {
                return null;
            }

            FrameChangeSample largest = _samples[1];
            for (int i = 2; i < _samples.Count; i++)
            {
                if (_samples[i].ChangedCellsVsPrevious > largest.ChangedCellsVsPrevious)
                {
                    largest = _samples[i];
                }
            }

            // A still recording has nothing worth pointing at; naming frame 1 would send the agent to an
            // arbitrary frame, and the burst sheet already reports "no change" the same way.
            if (largest.ChangedCellsVsPrevious == 0)
            {
                return null;
            }

            return new LargestFrameChange
            {
                CapturedIndex = largest.CapturedIndex,
                EncodedFrame = largest.EncodedFrame,
                Seconds = largest.Seconds,
                ChangedCellsVsPrevious = largest.ChangedCellsVsPrevious,
                MeanVsPrevious = largest.MeanVsPrevious
            };
        }

        private int CountChangedFrames(bool requireMatchingTwoBack)
        {
            int count = 0;
            foreach (FrameChangeSample sample in _samples)
            {
                if (sample.ChangedCellsVsPrevious < ChangedFrameMinCells)
                {
                    continue;
                }

                // Frames 0 and 1 have no frame two before them, so their zero must not read as a match.
                bool matchesTwoBack = sample.CapturedIndex >= 2
                    && sample.ChangedCellsVsTwoBack <= MatchingTwoBackMaxCells;
                if (!requireMatchingTwoBack || matchesTwoBack)
                {
                    count++;
                }
            }

            return count;
        }

        private List<AlternatingCell> TopAlternatingCells()
        {
            List<int> indices = new List<int>();
            for (int i = 0; i < _alternationCounts.Length; i++)
            {
                if (_alternationCounts[i] > 0)
                {
                    indices.Add(i);
                }
            }

            indices.Sort((left, right) =>
            {
                int byCount = _alternationCounts[right].CompareTo(_alternationCounts[left]);
                return byCount != 0 ? byCount : left.CompareTo(right);
            });

            List<AlternatingCell> cells = new List<AlternatingCell>();
            for (int i = 0; i < indices.Count && i < TopAlternatingCellCount; i++)
            {
                cells.Add(CreateCell(indices[i]));
            }

            return cells;
        }

        private AlternatingCell CreateCell(int index)
        {
            int column = index % _gridColumns;
            int row = index / _gridColumns;
            int left = column * _videoWidth / _gridColumns;
            int right = (column + 1) * _videoWidth / _gridColumns;
            // Grid row 0 is the bottom of the image, and agents read rectangles from the top-left corner.
            int top = _videoHeight - (row + 1) * _videoHeight / _gridRows;
            int bottom = _videoHeight - row * _videoHeight / _gridRows;
            return new AlternatingCell
            {
                Column = column,
                Row = row,
                Count = _alternationCounts[index],
                TopLeftX = left,
                TopLeftY = top,
                Width = right - left,
                Height = bottom - top
            };
        }
    }
}
