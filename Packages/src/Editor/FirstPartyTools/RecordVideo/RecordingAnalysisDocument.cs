using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// One tile of a contact sheet, identified by the captured frame it shows.
    /// </summary>
    public sealed class SheetFrame
    {
        public int CapturedIndex { get; set; }

        public int EncodedFrame { get; set; }

        public double Seconds { get; set; }
    }

    /// <summary>
    /// A contact sheet PNG and the frames on it, listed from the top-left tile.
    /// </summary>
    public sealed class SheetInfo
    {
        public string Path { get; set; }

        public int Columns { get; set; }

        public List<SheetFrame> Frames { get; set; } = new List<SheetFrame>();
    }

    /// <summary>
    /// The sidecar JSON written next to a recording: every per-frame change measurement,
    /// their summary, and the sheets built when the recording stopped.
    /// </summary>
    public sealed class RecordingAnalysisDocument
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;

        public string VideoPath { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public int FrameRate { get; set; }

        public int TileWidth { get; set; }

        public int TileHeight { get; set; }

        public int GridColumns { get; set; }

        public int GridRows { get; set; }

        public int CellChangedThreshold { get; set; }

        public List<FrameChangeSample> Samples { get; set; } = new List<FrameChangeSample>();

        public FrameChangeSummary Summary { get; set; }

        public string BurstAnchor { get; set; }

        public List<double> MarkSeconds { get; set; } = new List<double>();

        public SheetInfo Burst { get; set; }

        public SheetInfo Timeline { get; set; }

        internal static void Write(string path, RecordingAnalysisDocument document)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(document, Formatting.Indented));
        }

        /// <summary>
        /// Returns null when the file is missing or unreadable, because a sidecar is optional
        /// evidence and its absence must not fail the stop or status that reads it.
        /// </summary>
        internal static RecordingAnalysisDocument TryRead(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<RecordingAnalysisDocument>(File.ReadAllText(path));
            }
            catch (JsonException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Where the sidecar of a stopped recording was written, and what it contains.
    /// </summary>
    public sealed class RecordingAnalysisResult
    {
        public RecordingAnalysisResult(string path, RecordingAnalysisDocument document)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("path must not be empty.", nameof(path));
            }

            Path = path;
            Document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public string Path { get; }

        public RecordingAnalysisDocument Document { get; }
    }
}
