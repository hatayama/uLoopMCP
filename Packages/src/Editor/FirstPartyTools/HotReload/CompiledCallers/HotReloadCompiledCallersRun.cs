using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;

using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// One run of the compiled-caller analysis: the cached compiled assemblies stay cached until it
    /// ends. End it with <see cref="End"/> once the run finished normally; dispose it on every exit.
    /// </summary>
    public sealed class HotReloadCompiledCallersRun : IDisposable
    {
        private readonly IDisposable _hold;
        private readonly Func<IDisposable> _holdEntries;
        private readonly HotReloadCallSiteBackfill _backfill;
        private IReadOnlyList<string> _refusedDllPaths = Array.Empty<string>();
        private string _projectRoot;
        private IReadOnlyList<HotReloadOneShotCallerNoteRequest> _requests;
        private bool _notesBuilt;
        private bool _ended;

        internal HotReloadCompiledCallersRun(
            IDisposable hold,
            Func<IDisposable> holdEntries,
            HotReloadCallSiteBackfill backfill)
        {
            Debug.Assert(hold != null, "hold must not be null.");
            Debug.Assert(holdEntries != null, "holdEntries must not be null.");
            Debug.Assert(backfill != null, "backfill must not be null.");
            _hold = hold;
            _holdEntries = holdEntries;
            _backfill = backfill;
        }

        /// <summary>
        /// Builds the one-shot lifecycle note of each request: one entry per request, in the same
        /// order, null where no note can be proven. Reads at most the configured number of compiled
        /// assemblies that are not cached yet; the ones it could not read are read in the
        /// background after <see cref="End"/>. Call once per run, on the Unity main thread (the
        /// scan resolves compiled assemblies through Editor APIs).
        /// </summary>
        public IReadOnlyList<string> BuildOneShotCallerNotes(
            string projectRoot,
            IReadOnlyList<HotReloadOneShotCallerNoteRequest> requests,
            string correlationId)
        {
            Debug.Assert(!string.IsNullOrEmpty(projectRoot), "projectRoot must not be null or empty.");
            Debug.Assert(requests != null, "requests must not be null.");
            Debug.Assert(!string.IsNullOrEmpty(correlationId), "correlationId must not be null or empty.");
            Debug.Assert(!_notesBuilt, "one budget per run: notes are built once.");
            _notesBuilt = true;
            _projectRoot = projectRoot;
            _requests = requests;

            // Why one budget per run rather than per scan: the direct scan and every level of the
            // caller closure go through the same lambda, so one budget bounds the whole note step
            // however many candidate assemblies and levels it visits.
            HotReloadCallSiteLoadBudget budget =
                new HotReloadCallSiteLoadBudget(HotReloadConstants.CallerNoteUncachedDllLoadBudget);
            string[] notes = HotReloadOneShotCallerNoteEnricher.BuildNotes(
                requests,
                (ignoredAssemblyName, identities) => HotReloadCallSiteScanner.FindCallSites(projectRoot, identities, budget));
            if (budget.RefusedAssemblyNames.Count > 0)
            {
                LogLoadBudgetExhausted(
                    HotReloadConstants.CallerNoteUncachedDllLoadBudget,
                    budget.RefusedAssemblyNames,
                    correlationId);
            }

            _refusedDllPaths = budget.RefusedDllPaths;
            return notes;
        }

        /// <summary>
        /// Ends the run after it finished normally: stops holding the cached assemblies, then starts
        /// reading in the background the ones the notes could not read. Call once, after the run's
        /// result is built.
        /// </summary>
        public void End(string correlationId)
        {
            Debug.Assert(!string.IsNullOrEmpty(correlationId), "correlationId must not be null or empty.");
            Debug.Assert(!_ended, "End is called once per run.");
            if (_ended)
            {
                return;
            }

            _ended = true;
            // Why the hold ends here, before the backfill starts: its reads belong to no run, so they
            // must evict as reads outside a run do rather than pile up under this run's hold.
            _hold.Dispose();
            _backfill.Start(_refusedDllPaths, FindRefusedAgainAsync, correlationId);
        }

        /// <summary>
        /// Stops holding the cached assemblies without starting background reads, which is what a
        /// run that ends with an exception gets. Safe after <see cref="End"/>: the hold is released once.
        /// </summary>
        public void Dispose()
        {
            _hold.Dispose();
        }

        /// <summary>Sets the dlls <see cref="End"/> reads, for tests that cannot exhaust a real budget.</summary>
        internal void RememberRefusedDllPathsForTesting(IReadOnlyList<string> dllPaths)
        {
            Debug.Assert(dllPaths != null, "dllPaths must not be null.");
            _refusedDllPaths = dllPaths;
        }

        // Recomputes this run's notes without reading any dll and returns the dlls that recompute
        // refuses: the ones the next run would refuse once the backfill read the earlier ones.
        private async Task<IReadOnlyList<string>> FindRefusedAgainAsync(CancellationToken ct)
        {
            // A run that built no notes has nothing to recompute; that is a valid run, not a misuse.
            if (_requests == null)
            {
                return Array.Empty<string>();
            }

            // Why the main thread: the scan resolves compiled assemblies through Editor APIs.
            await MainThreadSwitcher.SwitchToMainThread(ct);
            // Why a hold only around the recompute: it keeps the dlls it scans from being evicted
            // mid-scan, and is released before the backfill reads, which belong to no run.
            using (IDisposable hold = _holdEntries())
            {
                // Why a zero budget: the recompute only collects the dlls not read yet; the backfill
                // reads them off the main thread. Its notes are discarded, and it logs no budget
                // exhaustion because no run's note was omitted.
                HotReloadCallSiteLoadBudget budget = new HotReloadCallSiteLoadBudget(0);
                HotReloadOneShotCallerNoteEnricher.BuildNotes(
                    _requests,
                    (ignoredAssemblyName, identities) => HotReloadCallSiteScanner.FindCallSites(_projectRoot, identities, budget));
                return budget.RefusedDllPaths;
            }
        }

        private static void LogLoadBudgetExhausted(
            int budgetLoads,
            IReadOnlyList<string> refusedAssemblyNames,
            string correlationId)
        {
            VibeLogger.LogInfo(
                HotReloadConstants.VibeLogCallerNoteLoadBudgetExhausted,
                "Caller-note resolution hit its uncached dll load budget; the note was omitted.",
                new
                {
                    budgetLoads,
                    refusedAssemblies = refusedAssemblyNames.ToArray()
                },
                correlationId);
        }
    }
}
