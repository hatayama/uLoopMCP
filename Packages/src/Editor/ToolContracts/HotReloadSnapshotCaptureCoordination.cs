using System;

namespace io.github.hatayama.UnityCliLoop.ToolContracts
{
    /// <summary>
    /// Editor-domain coordination point that lets the compile status report wait for the hot
    /// reload source snapshot capture. The hot-reload tool lives in a sibling assembly the
    /// server must not reference, so it publishes the hold through a delegate wired at its own
    /// startup.
    /// </summary>
    public static class HotReloadSnapshotCaptureCoordination
    {
        // Set by the hot-reload startup. Null means the hot-reload tool has not initialized in
        // this domain, so nothing holds the compile completion - callers treat null as false.
        // While it returns true, the compile completion is not reported: if the CLI returned
        // from compile before the capture finished, an edit made right after could be captured
        // as the source the compiler read.
        public static Func<bool> IsHoldingCompileCompletion { get; set; }
    }
}
