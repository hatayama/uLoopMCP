using System;
using System.Diagnostics;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Records editor startup timings for InitializeOnLoad entrypoints.
    /// The log format is intentionally stable so Before/After measurements can be compared.
    /// </summary>
    internal static class InitializeOnLoadTiming
    {
        private const string LogPrefix = "[Perf] InitializeOnLoad";

        internal static void Measure(string label, Action action)
        {
            UnityEngine.Debug.Assert(!string.IsNullOrWhiteSpace(label), "label must not be empty");
            UnityEngine.Debug.Assert(action != null, "action must not be null");

            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                action();
            }
            finally
            {
                stopwatch.Stop();
                UnityEngine.Debug.Log($"{LogPrefix} {label}: {stopwatch.Elapsed.TotalMilliseconds:F1}ms");
            }
        }
    }
}
