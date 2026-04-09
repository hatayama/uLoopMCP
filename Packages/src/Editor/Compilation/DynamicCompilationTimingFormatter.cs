using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP
{
    internal static class DynamicCompilationTimingFormatter
    {
        private const string CacheHitTimingEntry = "[Perf] CacheHit: true";

        public static List<string> CreateCompilationTimings(
            double referenceResolutionMilliseconds,
            double buildMilliseconds,
            double assemblyLoadMilliseconds)
        {
            return new List<string>
            {
                $"[Perf] ReferenceResolution: {referenceResolutionMilliseconds:F1}ms",
                $"[Perf] Build: {buildMilliseconds:F1}ms",
                $"[Perf] AssemblyLoad: {assemblyLoadMilliseconds:F1}ms"
            };
        }

        public static List<string> CreateCachedCompilationTimings()
        {
            List<string> timings = CreateCompilationTimings(0, 0, 0);
            timings.Add(CacheHitTimingEntry);
            return timings;
        }
    }
}
