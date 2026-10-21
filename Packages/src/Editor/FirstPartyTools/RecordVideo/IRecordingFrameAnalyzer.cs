using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Turns the frames of one recording into evidence an agent can read: change measurements and sheets.
    /// </summary>
    internal interface IRecordingFrameAnalyzer
    {
        /// <summary>
        /// Measures one captured frame. Called once per captured frame, not once per encoded repeat.
        /// </summary>
        void OnCapturedFrame(Texture2D frame, int encodedFrame, double seconds);

        /// <summary>
        /// Records that the moment worth looking at is now, so the burst sheet is built around it.
        /// </summary>
        void Mark(double seconds);

        /// <summary>
        /// Writes the sidecar files beside the video and returns where they are. Uses only CPU arrays,
        /// so it can run while the Editor is reloading assemblies or quitting.
        /// </summary>
        RecordingAnalysisResult Finish(string videoPath, int width, int height, int frameRate);
    }
}
