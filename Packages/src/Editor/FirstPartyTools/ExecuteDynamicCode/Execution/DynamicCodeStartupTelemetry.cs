using System.Collections.Generic;
using System.Diagnostics;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    internal sealed class DynamicCodeStartupTelemetryService
    {
        private readonly object _syncRoot = new();

        private long _serverReadyTimestamp;
        private long _prewarmStateTimestamp;
        private long _prewarmStartedTimestamp;
        private long _prewarmFinishedTimestamp;
        private string _prewarmState = "NotRequested";
        private string _prewarmDetail = string.Empty;

        public void MarkServerReady()
        {
            lock (_syncRoot)
            {
                _serverReadyTimestamp = Stopwatch.GetTimestamp();
                _prewarmStateTimestamp = 0;
                _prewarmStartedTimestamp = 0;
                _prewarmFinishedTimestamp = 0;
                _prewarmState = "NotRequested";
                _prewarmDetail = string.Empty;
            }
        }

        public void MarkPrewarmQueued()
        {
            UpdatePrewarmState("Queued", string.Empty, false);
        }

        public void MarkPrewarmStarted()
        {
            lock (_syncRoot)
            {
                _prewarmStartedTimestamp = Stopwatch.GetTimestamp();
            }

            UpdatePrewarmState("Running", string.Empty, false);
        }

        public void MarkPrewarmCompleted()
        {
            UpdatePrewarmState("Completed", string.Empty, true);
        }

        public void MarkPrewarmSkipped(string detail)
        {
            UpdatePrewarmState("Skipped", detail, true);
        }

        public void MarkPrewarmYielded(string detail)
        {
            UpdatePrewarmState("Yielded", detail, true);
        }

        public void MarkPrewarmFailed(string detail)
        {
            UpdatePrewarmState("Failed", detail, true);
        }

        public List<string> CreateTimingEntries()
        {
            long serverReadyTimestamp;
            long prewarmStateTimestamp;
            long prewarmStartedTimestamp;
            long prewarmFinishedTimestamp;
            string prewarmState;
            string prewarmDetail;

            lock (_syncRoot)
            {
                serverReadyTimestamp = _serverReadyTimestamp;
                prewarmStateTimestamp = _prewarmStateTimestamp;
                prewarmStartedTimestamp = _prewarmStartedTimestamp;
                prewarmFinishedTimestamp = _prewarmFinishedTimestamp;
                prewarmState = _prewarmState;
                prewarmDetail = _prewarmDetail;
            }

            List<string> entries = new();
            if (serverReadyTimestamp > 0)
            {
                entries.Add($"[Perf] ServerReadyAge: {ToMilliseconds(serverReadyTimestamp, Stopwatch.GetTimestamp()):F1}ms");
            }

            entries.Add($"[Perf] WarmReady: {string.Equals(prewarmState, "Completed")}");
            entries.Add($"[Perf] PrewarmState: {prewarmState}");

            if (prewarmStateTimestamp > 0)
            {
                entries.Add($"[Perf] PrewarmStateAge: {ToMilliseconds(prewarmStateTimestamp, Stopwatch.GetTimestamp()):F1}ms");
            }

            if (prewarmStartedTimestamp > 0 && prewarmFinishedTimestamp > 0)
            {
                entries.Add($"[Perf] PrewarmDuration: {ToMilliseconds(prewarmStartedTimestamp, prewarmFinishedTimestamp):F1}ms");
            }

            if (!string.IsNullOrEmpty(prewarmDetail))
            {
                entries.Add($"[Perf] PrewarmDetail: {prewarmDetail}");
            }

            return entries;
        }

        internal void Reset()
        {
            lock (_syncRoot)
            {
                _serverReadyTimestamp = 0;
                _prewarmStateTimestamp = 0;
                _prewarmStartedTimestamp = 0;
                _prewarmFinishedTimestamp = 0;
                _prewarmState = "NotRequested";
                _prewarmDetail = string.Empty;
            }
        }

        private void UpdatePrewarmState(string state, string detail, bool terminal)
        {
            lock (_syncRoot)
            {
                _prewarmState = state;
                _prewarmDetail = detail ?? string.Empty;
                _prewarmStateTimestamp = Stopwatch.GetTimestamp();
                if (terminal)
                {
                    _prewarmFinishedTimestamp = _prewarmStateTimestamp;
                    return;
                }

                _prewarmFinishedTimestamp = 0;
            }
        }

        private static double ToMilliseconds(long startTimestamp, long endTimestamp)
        {
            long elapsedTicks = endTimestamp - startTimestamp;
            return elapsedTicks * 1000d / Stopwatch.Frequency;
        }
    }

    internal static class DynamicCodeStartupTelemetry
    {
        private static readonly DynamicCodeStartupTelemetryService ServiceValue =
            new DynamicCodeStartupTelemetryService();

        public static void MarkServerReady()
        {
            ServiceValue.MarkServerReady();
        }

        public static void MarkPrewarmQueued()
        {
            ServiceValue.MarkPrewarmQueued();
        }

        public static void MarkPrewarmStarted()
        {
            ServiceValue.MarkPrewarmStarted();
        }

        public static void MarkPrewarmCompleted()
        {
            ServiceValue.MarkPrewarmCompleted();
        }

        public static void MarkPrewarmSkipped(string detail)
        {
            ServiceValue.MarkPrewarmSkipped(detail);
        }

        public static void MarkPrewarmYielded(string detail)
        {
            ServiceValue.MarkPrewarmYielded(detail);
        }

        public static void MarkPrewarmFailed(string detail)
        {
            ServiceValue.MarkPrewarmFailed(detail);
        }

        public static List<string> CreateTimingEntries()
        {
            return ServiceValue.CreateTimingEntries();
        }

        internal static void Reset()
        {
            ServiceValue.Reset();
        }
    }
}
