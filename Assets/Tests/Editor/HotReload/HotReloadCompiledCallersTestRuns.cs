using System.Threading.Tasks;

using NUnit.Framework;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// Compiled-caller runs for tests that build an apply context by hand, outside any hot reload run.
    /// </summary>
    internal static class HotReloadCompiledCallersTestRuns
    {
        /// <summary>
        /// Opens a run on compiled callers of its own: a fresh cache and a backfill that reads
        /// nothing. Its hold is taken on that fresh cache, never on the shared one, so it keeps no
        /// shared entry alive and needs no Dispose; the caller scans it does still read the shared
        /// cache without a hold, as gate tests did before scans moved onto the run. The backfill is
        /// idle, so the run begins synchronously and is read without blocking.
        /// </summary>
        internal static HotReloadCompiledCallersRun OpenIsolated()
        {
            HotReloadCompiledCallers compiledCallers = new HotReloadCompiledCallers(
                new HotReloadCompiledCallSiteCache(HotReloadCompiledCallSiteCache.DefaultBudgetBytes),
                new HotReloadCallSiteBackfill((dllPath, ct) => Task.CompletedTask));
            Task<HotReloadCompiledCallersRun> begin = compiledCallers.BeginRunAsync();
            Assert.That(begin.IsCompleted, Is.True, "Precondition: a run on an idle backfill begins synchronously.");
            return begin.Result;
        }
    }
}
