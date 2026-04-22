using System.Collections.Generic;
using System.Diagnostics;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Captures the synchronous startup time so InitializeOnLoad regressions stay visible.
    /// Why: startup recovery is being deferred, so we need a stable metric for the remaining
    /// main-thread blocking work instead of relying on server-ready timing.
    /// </summary>
    internal static class EditorStartupTelemetry
    {
        private static readonly object SyncRoot = new();

        private static long _syncStartedTimestamp;
        private static long _syncFinishedTimestamp;

        internal static void Reset()
        {
            lock (SyncRoot)
            {
                _syncStartedTimestamp = 0;
                _syncFinishedTimestamp = 0;
            }
        }

        internal static void MarkSyncStarted()
        {
            lock (SyncRoot)
            {
                _syncStartedTimestamp = Stopwatch.GetTimestamp();
                _syncFinishedTimestamp = 0;
            }
        }

        internal static void MarkSyncCompleted()
        {
            lock (SyncRoot)
            {
                if (_syncStartedTimestamp == 0)
                {
                    return;
                }

                _syncFinishedTimestamp = Stopwatch.GetTimestamp();
            }
        }

        internal static double? GetLatestSyncDurationMilliseconds()
        {
            lock (SyncRoot)
            {
                if (_syncStartedTimestamp == 0 || _syncFinishedTimestamp == 0)
                {
                    return null;
                }

                return ToMilliseconds(_syncStartedTimestamp, _syncFinishedTimestamp);
            }
        }

        internal static List<string> CreateTimingEntries()
        {
            List<string> entries = new();
            double? syncDurationMilliseconds = GetLatestSyncDurationMilliseconds();
            if (!syncDurationMilliseconds.HasValue)
            {
                return entries;
            }

            entries.Add($"[Perf] StartupSyncDuration: {syncDurationMilliseconds.Value:F1}ms");
            return entries;
        }

        private static double ToMilliseconds(long startTimestamp, long endTimestamp)
        {
            long elapsedTicks = endTimestamp - startTimestamp;
            return elapsedTicks * 1000d / Stopwatch.Frequency;
        }
    }
}
