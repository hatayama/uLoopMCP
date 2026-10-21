using System.Collections.Generic;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Immutable view of a video recording session at a point in time.
    /// </summary>
    internal readonly struct VideoRecordingSnapshot
    {
        internal VideoRecordingSnapshot(
            string outputPath,
            int width,
            int height,
            int frameRate,
            int encodedFrameCount,
            int skippedFrameCount,
            double elapsedSeconds,
            string stoppedBy,
            bool isRecording,
            string quality,
            RecordingAnalysisResult analysis,
            IReadOnlyList<double> markSeconds)
        {
            OutputPath = outputPath;
            Width = width;
            Height = height;
            FrameRate = frameRate;
            EncodedFrameCount = encodedFrameCount;
            SkippedFrameCount = skippedFrameCount;
            ElapsedSeconds = elapsedSeconds;
            StoppedBy = stoppedBy;
            IsRecording = isRecording;
            Quality = quality;
            Analysis = analysis;
            MarkSeconds = markSeconds;
        }

        internal string OutputPath { get; }

        internal int Width { get; }

        internal int Height { get; }

        internal int FrameRate { get; }

        internal int EncodedFrameCount { get; }

        internal int SkippedFrameCount { get; }

        internal double ElapsedSeconds { get; }

        internal string StoppedBy { get; }

        internal bool IsRecording { get; }

        internal string Quality { get; }

        /// <summary>
        /// The sidecar written when the recording stopped; null while recording or when the analysis failed.
        /// </summary>
        internal RecordingAnalysisResult Analysis { get; }

        /// <summary>
        /// Recording-relative seconds of every mark; null for a default snapshot.
        /// </summary>
        internal IReadOnlyList<double> MarkSeconds { get; }
    }
}
