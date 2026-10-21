using System.Collections.Generic;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Copies the marks and the stopped recording's analysis onto a record-video response.
    /// Leaves every analysis field unset when there is nothing to report, so start and status
    /// responses keep their earlier shape.
    /// </summary>
    internal static class RecordVideoResponseAnalysisMapper
    {
        internal static void Apply(RecordVideoResponse response, VideoRecordingSnapshot snapshot)
        {
            if (snapshot.MarkSeconds != null && snapshot.MarkSeconds.Count > 0)
            {
                response.MarkSeconds = new List<double>(snapshot.MarkSeconds);
            }

            if (snapshot.Analysis == null)
            {
                return;
            }

            RecordingAnalysisDocument document = snapshot.Analysis.Document;
            response.AnalysisPath = snapshot.Analysis.Path;
            response.FrameChange = document.Summary;
            if (document.Timeline != null)
            {
                response.TimelineSheetPath = document.Timeline.Path;
                response.TimelineSheetColumns = document.Timeline.Columns;
                response.TimelineFrames = document.Timeline.Frames;
            }

            if (document.Burst == null)
            {
                return;
            }

            response.BurstSheetPath = document.Burst.Path;
            response.BurstSheetColumns = document.Burst.Columns;
            response.BurstAnchor = document.BurstAnchor;
            response.BurstFrames = document.Burst.Frames;
        }
    }
}
