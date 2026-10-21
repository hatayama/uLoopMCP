using System;
using System.Collections.Generic;

using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// What a burst sheet is centered on: the last mark, or the largest change when nothing was marked.
    /// </summary>
    internal enum BurstAnchor
    {
        none,
        mark,
        largestChange
    }

    /// <summary>
    /// A copied tile together with the change measurement of the frame it came from.
    /// </summary>
    internal readonly struct CapturedTile
    {
        internal CapturedTile(Color32[] pixels, FrameChangeSample sample)
        {
            Pixels = pixels;
            Sample = sample;
        }

        internal Color32[] Pixels { get; }

        internal FrameChangeSample Sample { get; }
    }

    /// <summary>
    /// Keeps the consecutive tiles around the moment worth looking at as CPU arrays: the last mark
    /// when the caller marked one, otherwise the frame with the most changed cells. A mark wins over
    /// any later change, so a scene transition after the marked moment cannot replace it.
    /// </summary>
    internal sealed class BurstFrameBuffer
    {
        internal const int FramesBefore = 16;
        internal const int FramesAfterMark = 16;
        internal const int FramesAfterPeak = 15;

        private readonly Queue<CapturedTile> _ring = new Queue<CapturedTile>();
        private readonly List<double> _markSeconds = new List<double>();
        private List<CapturedTile> _burst = new List<CapturedTile>();
        private int _tailRemaining;
        private int _peakCells;
        private BurstAnchor _anchor = BurstAnchor.none;

        internal BurstAnchor Anchor => _anchor;

        internal IReadOnlyList<double> MarkSeconds => _markSeconds;

        internal IReadOnlyList<CapturedTile> Frames =>
            _anchor == BurstAnchor.none ? (IReadOnlyList<CapturedTile>)Array.Empty<CapturedTile>() : _burst;

        internal void Mark(double seconds)
        {
            _markSeconds.Add(seconds);
            _burst = new List<CapturedTile>(_ring);
            _tailRemaining = FramesAfterMark;
            _anchor = BurstAnchor.mark;
        }

        internal void Add(Color32[] tile, FrameChangeSample sample)
        {
            Debug.Assert(tile != null, "tile must not be null.");
            Debug.Assert(sample != null, "sample must not be null.");

            // The caller reuses its tile buffer for the next frame.
            Color32[] pixels = new Color32[tile.Length];
            Array.Copy(tile, pixels, tile.Length);
            CapturedTile captured = new CapturedTile(pixels, sample);

            if (_anchor != BurstAnchor.mark
                && sample.ChangedCellsVsPrevious > _peakCells)
            {
                _peakCells = sample.ChangedCellsVsPrevious;
                _anchor = BurstAnchor.largestChange;
                _burst = new List<CapturedTile>(_ring) { captured };
                _tailRemaining = FramesAfterPeak;
            }
            else if (_tailRemaining > 0)
            {
                _burst.Add(captured);
                _tailRemaining--;
            }

            _ring.Enqueue(captured);
            if (_ring.Count > FramesBefore)
            {
                _ring.Dequeue();
            }
        }
    }
}
