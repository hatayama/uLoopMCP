using System.Collections.Generic;
using Newtonsoft.Json;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// How much one captured frame differs from the frame before it and the frame two before it.
    /// </summary>
    public sealed class FrameChangeSample
    {
        public int CapturedIndex { get; set; }

        public int EncodedFrame { get; set; }

        public double Seconds { get; set; }

        public double MeanVsPrevious { get; set; }

        public int ChangedCellsVsPrevious { get; set; }

        public double MeanVsTwoBack { get; set; }

        public int ChangedCellsVsTwoBack { get; set; }
    }

    /// <summary>
    /// A grid cell that changed against the previous frame while matching the frame two before,
    /// with its rectangle in video pixels measured from the top-left corner.
    /// </summary>
    public sealed class AlternatingCell
    {
        public int Column { get; set; }

        public int Row { get; set; }

        public int Count { get; set; }

        public int TopLeftX { get; set; }

        public int TopLeftY { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }
    }

    /// <summary>
    /// The captured frame whose number of cells changed against the previous frame is the largest.
    /// </summary>
    public sealed class LargestFrameChange
    {
        public int CapturedIndex { get; set; }

        public int EncodedFrame { get; set; }

        public double Seconds { get; set; }

        public int ChangedCellsVsPrevious { get; set; }

        public double MeanVsPrevious { get; set; }
    }

    /// <summary>
    /// Summary of the frame change measurements of one recording. These are measurements, not verdicts.
    /// </summary>
    public sealed class FrameChangeSummary
    {
        public int CapturedFrameCount { get; set; }

        public double MeanVsPrevious { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public LargestFrameChange LargestChange { get; set; }

        public int FramesChangedVsPrevious { get; set; }

        public int FramesChangedVsPreviousMatchingTwoBack { get; set; }

        public List<AlternatingCell> TopAlternatingCells { get; set; } = new List<AlternatingCell>();

        public int GridColumns { get; set; }

        public int GridRows { get; set; }

        public int CellChangedThreshold { get; set; }

        public int TileWidth { get; set; }

        public int TileHeight { get; set; }
    }
}
