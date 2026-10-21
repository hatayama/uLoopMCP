using System;
using System.Collections.Generic;
using UnityEngine;

using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Owns one recording: pacing, encode/skip counts, and encoder lifetime.
    /// </summary>
    internal sealed class VideoRecordingSession
    {
        private readonly IVideoFrameEncoder _encoder;
        private readonly IGameViewFrameSource _frameSource;
        private readonly Func<double> _clock;
        private readonly int _frameRate;
        private readonly double _maxDurationSeconds;
        private readonly string _outputPath;
        private readonly RecordVideoQuality _quality;
        private readonly double _startedAt;
        private readonly IRecordingFrameAnalyzer _analyzer;
        private readonly List<double> _markSeconds = new List<double>();
        private Texture2D _frameTexture;
        private int _encodedFrameCount;
        private int _skippedFrameCount;
        private bool _stopped;
        private string _stoppedBy;
        private double _elapsedAtStop;
        private RecordingAnalysisResult _analysis;

        internal VideoRecordingSession(
            IVideoFrameEncoder encoder,
            IGameViewFrameSource frameSource,
            Func<double> clock,
            int frameRate,
            double maxDurationSeconds,
            string outputPath,
            RecordVideoQuality quality,
            IRecordingFrameAnalyzer analyzer)
        {
            Debug.Assert(encoder.Width > 0, "encoder width must be positive.");
            Debug.Assert(encoder.Height > 0, "encoder height must be positive.");
            Debug.Assert(analyzer != null, "analyzer must not be null.");

            _encoder = encoder;
            _frameSource = frameSource;
            _clock = clock;
            _frameRate = frameRate;
            _maxDurationSeconds = maxDurationSeconds;
            _outputPath = outputPath;
            _quality = quality;
            _analyzer = analyzer;
            _startedAt = clock();
            _frameTexture = new Texture2D(encoder.Width, encoder.Height, TextureFormat.RGBA32, false);
            // A texture created during Play Mode is destroyed when Play Mode ends, and a window
            // recording outlives Play Mode, so the frame texture must not be owned by the scene.
            _frameTexture.hideFlags = HideFlags.HideAndDontSave;
        }

        internal void Tick()
        {
            if (_stopped)
            {
                return;
            }

            // Unity's null comparison reports a destroyed texture. Reading through it would throw
            // on every editor update and break the update delegate chain for every other subscriber.
            if (_frameTexture == null)
            {
                VibeLogger.LogError(
                    "record_video_frame_texture_lost",
                    "The recording frame texture was destroyed, so the recording was stopped.");
                Stop(RecordVideoConstants.StoppedByFrameTextureLost);
                return;
            }

            double elapsed = _clock() - _startedAt;
            if (elapsed >= _maxDurationSeconds)
            {
                Stop(RecordVideoConstants.StoppedByMaxDuration);
                return;
            }

            // Checked after max-duration so a run that hits both still reports max-duration,
            // and before FramesDue so a closed window's frames are not counted as skips.
            if (_frameSource.IsSourceClosed)
            {
                Stop(RecordVideoConstants.StoppedByWindowClosed);
                return;
            }

            int due = VideoRecordingFramePacer.FramesDue(
                elapsed,
                _frameRate,
                _encodedFrameCount + _skippedFrameCount);
            if (due == 0)
            {
                return;
            }

            if (!_frameSource.TryReadFrame(_frameTexture))
            {
                _skippedFrameCount += due;
                return;
            }

            // Analyzed once per captured frame, before the repeats below: a slow Editor repeats the
            // same frame to keep pace, and counting those repeats as still frames would read as stutter.
            _analyzer.OnCapturedFrame(_frameTexture, _encodedFrameCount, elapsed);
            for (int i = 0; i < due; i++)
            {
                if (_encoder.AddFrame(_frameTexture))
                {
                    _encodedFrameCount++;
                }
                else
                {
                    _skippedFrameCount++;
                }
            }
        }

        internal void Stop(string reason)
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;
            _stoppedBy = reason;
            _elapsedAtStop = _clock() - _startedAt;
            _encoder.Dispose();
            FinishAnalysis();
            UnityEngine.Object.DestroyImmediate(_frameTexture);
            _frameTexture = null;
        }

        internal void Mark()
        {
            if (_stopped)
            {
                return;
            }

            double elapsed = _clock() - _startedAt;
            _markSeconds.Add(elapsed);
            _analyzer.Mark(elapsed);
        }

        private void FinishAnalysis()
        {
            // The analysis is newer than the recording itself, so its failure must not cost the video.
            try
            {
                _analysis = _analyzer.Finish(_outputPath, _encoder.Width, _encoder.Height, _frameRate);
            }
            catch (Exception exception)
            {
                VibeLogger.LogError(
                    "record_video_analysis_failed",
                    "Analyzing the recorded frames failed; the video was kept without the analysis.",
                    new { Error = exception.Message });
                _analysis = null;
            }
        }

        internal Texture2D FrameTextureForTests => _frameTexture;

        internal VideoRecordingSnapshot Snapshot()
        {
            double elapsed = _stopped ? _elapsedAtStop : _clock() - _startedAt;
            return new VideoRecordingSnapshot(
                _outputPath,
                _encoder.Width,
                _encoder.Height,
                _frameRate,
                _encodedFrameCount,
                _skippedFrameCount,
                elapsed,
                _stoppedBy,
                !_stopped,
                _quality.ToString(),
                _analysis,
                _markSeconds.ToArray());
        }
    }
}
