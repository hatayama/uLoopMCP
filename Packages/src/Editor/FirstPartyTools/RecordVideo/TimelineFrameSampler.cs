using System;
using System.Collections.Generic;

using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Keeps evenly spaced tiles across a whole recording for the timeline sheet. The recording's
    /// length is unknown while it runs, so when the tiles exceed the limit every other one is dropped
    /// and the interval doubles; the sheet then always covers the whole recording at a near-even pace.
    /// </summary>
    internal sealed class TimelineFrameSampler
    {
        internal const double BaseIntervalSeconds = 0.5;
        internal const int MaxTiles = 40;

        private List<CapturedTile> _tiles = new List<CapturedTile>();

        // Arrays of dropped tiles, reused so thinning does not leave garbage for the collector to pause on.
        private readonly Stack<Color32[]> _free = new Stack<Color32[]>();
        private double _nextAt;

        internal double IntervalSeconds { get; private set; } = BaseIntervalSeconds;

        internal IReadOnlyList<CapturedTile> Frames => _tiles;

        internal void Add(Color32[] tile, FrameChangeSample sample)
        {
            Debug.Assert(tile != null, "tile must not be null.");
            Debug.Assert(sample != null, "sample must not be null.");

            if (sample.Seconds < _nextAt)
            {
                return;
            }

            // The caller reuses its tile buffer for the next frame.
            Color32[] pixels = _free.Count > 0 && _free.Peek().Length == tile.Length
                ? _free.Pop()
                : new Color32[tile.Length];
            Array.Copy(tile, pixels, tile.Length);
            _tiles.Add(new CapturedTile(pixels, sample));
            // Measured from the kept frame, not from the slot it was due in: a slow Editor that skipped
            // the slot still gets the next tile one interval after this one.
            _nextAt = sample.Seconds + IntervalSeconds;

            if (_tiles.Count > MaxTiles)
            {
                Thin();
            }
        }

        private void Thin()
        {
            List<CapturedTile> kept = new List<CapturedTile>(MaxTiles / 2 + 1);
            for (int i = 0; i < _tiles.Count; i++)
            {
                if (i % 2 == 0)
                {
                    kept.Add(_tiles[i]);
                }
                else
                {
                    _free.Push(_tiles[i].Pixels);
                }
            }

            _tiles = kept;
            IntervalSeconds *= 2;
            _nextAt = _tiles[_tiles.Count - 1].Sample.Seconds + IntervalSeconds;
        }
    }
}
