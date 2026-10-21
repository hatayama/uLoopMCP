using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Measures every captured frame of a recording on CPU copies and, when the recording stops,
    /// writes the burst sheet and the sidecar JSON beside the video.
    /// </summary>
    internal sealed class RecordingFrameAnalyzer : IRecordingFrameAnalyzer
    {
        internal const int BurstColumns = 4;

        private readonly int _width;
        private readonly int _height;
        private readonly int _tileWidth;
        private readonly int _tileHeight;
        private readonly int _gridColumns;
        private readonly int _gridRows;
        private readonly FrameTileSampler _sampler;
        private readonly Color32[] _source;
        private readonly Color32[] _tile;
        private readonly byte[] _grid;
        private readonly FrameChangeSeries _series;
        private readonly BurstFrameBuffer _burst = new BurstFrameBuffer();

        internal RecordingFrameAnalyzer(int width, int height)
        {
            Debug.Assert(width > 0 && height > 0, "frame size must be positive.");

            _width = width;
            _height = height;
            _sampler = FrameTileSampler.ForFrame(width, height);
            _tileWidth = _sampler.TileWidth;
            _tileHeight = _sampler.TileHeight;
            _gridColumns = _sampler.GridColumns;
            _gridRows = _sampler.GridRows;
            // Allocated once: a per-frame allocation would add GC pressure to every Editor update while recording.
            _source = new Color32[width * height];
            _tile = new Color32[_tileWidth * _tileHeight];
            _grid = new byte[_gridColumns * _gridRows];
            _series = new FrameChangeSeries(width, height, _gridColumns, _gridRows);
        }

        public void OnCapturedFrame(Texture2D frame, int encodedFrame, double seconds)
        {
            Debug.Assert(frame.width == _width && frame.height == _height, "frame size must match the recording.");

            // Why copy instead of reading the NativeArray in place: Unity 2022.3 has no span view of a
            // NativeArray and this assembly does not allow unsafe code.
            NativeArray<Color32> raw = frame.GetRawTextureData<Color32>();
            raw.CopyTo(_source);
            _sampler.Sample(_source, _tile, _grid);
            FrameChangeSample sample = _series.Add(encodedFrame, seconds, _grid);
            _burst.Add(_tile, sample);
        }

        public void Mark(double seconds)
        {
            _burst.Mark(seconds);
        }

        public RecordingAnalysisResult Finish(string videoPath, int width, int height, int frameRate)
        {
            FrameChangeSummary summary = _series.Summarize();
            summary.TileWidth = _tileWidth;
            summary.TileHeight = _tileHeight;
            RecordingAnalysisDocument document = new RecordingAnalysisDocument
            {
                VideoPath = videoPath,
                Width = width,
                Height = height,
                FrameRate = frameRate,
                TileWidth = _tileWidth,
                TileHeight = _tileHeight,
                GridColumns = _gridColumns,
                GridRows = _gridRows,
                CellChangedThreshold = FrameChangeSeries.CellChangedThreshold,
                Samples = new List<FrameChangeSample>(_series.Samples),
                Summary = summary,
                MarkSeconds = new List<double>(_burst.MarkSeconds)
            };

            // One frame is not a sequence; a sheet of it would only repeat what a screenshot shows.
            if (_burst.Frames.Count >= 2)
            {
                document.Burst = WriteSheet(RecordVideoSidecarPaths.Burst(videoPath), _burst.Frames, BurstColumns);
                document.BurstAnchor = _burst.Anchor == BurstAnchor.mark ? "mark" : "largest-change";
            }

            string analysisPath = RecordVideoSidecarPaths.Analysis(videoPath);
            RecordingAnalysisDocument.Write(analysisPath, document);
            return new RecordingAnalysisResult(analysisPath, document);
        }

        private SheetInfo WriteSheet(string path, IReadOnlyList<CapturedTile> frames, int columns)
        {
            List<Color32[]> tiles = new List<Color32[]>(frames.Count);
            List<SheetFrame> sheetFrames = new List<SheetFrame>(frames.Count);
            foreach (CapturedTile frame in frames)
            {
                tiles.Add(frame.Pixels);
                sheetFrames.Add(new SheetFrame
                {
                    CapturedIndex = frame.Sample.CapturedIndex,
                    EncodedFrame = frame.Sample.EncodedFrame,
                    Seconds = frame.Sample.Seconds
                });
            }

            (int columns, int rows, int width, int height) layout = ContactSheetComposer.Layout(
                tiles.Count,
                columns,
                _tileWidth,
                _tileHeight,
                ContactSheetComposer.DefaultGutter);
            Color32[] sheet = ContactSheetComposer.Compose(
                tiles,
                _tileWidth,
                _tileHeight,
                columns,
                ContactSheetComposer.DefaultGutter);
            // Encoding the array directly avoids creating a Texture2D, which is not possible while the
            // Editor is quitting or reloading assemblies.
            byte[] png = ImageConversion.EncodeArrayToPNG(
                sheet,
                GraphicsFormat.R8G8B8A8_SRGB,
                (uint)layout.width,
                (uint)layout.height);
            File.WriteAllBytes(path, png);
            return new SheetInfo { Path = path, Columns = columns, Frames = sheetFrames };
        }
    }
}
