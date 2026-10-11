using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using io.github.hatayama.UnityCliLoop.ToolContracts;

using Debug = UnityEngine.Debug;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Fills the compilation assembly cache at startup with a list rebuilt off the main thread from
    /// the saved list and the compiled files, so the first caller of the domain does not ask Unity
    /// on the main thread. A rebuilt list that was kept is saved as the next domain's baseline.
    /// </summary>
    internal sealed class HotReloadCompilationListRefill
    {
        private readonly HotReloadCompilationAssemblyListStore _store;
        private readonly HotReloadCompilationAssemblyCache _cache;
        private readonly Func<HotReloadSnapshotCaptureInputs> _readInputs;
        private readonly Func<bool> _isCompilationFailed;
        private readonly Func<Func<HotReloadCompilationListReconstruction>, Task<HotReloadCompilationListReconstruction>> _runInBackground;

        // Main thread only. Replaced by each run, so cancelling stops only the latest one.
        private CancellationTokenSource _cancellation = new CancellationTokenSource();

        internal HotReloadCompilationListRefill(
            HotReloadCompilationAssemblyListStore store,
            HotReloadCompilationAssemblyCache cache,
            Func<HotReloadSnapshotCaptureInputs> readInputs,
            Func<bool> isCompilationFailed,
            Func<Func<HotReloadCompilationListReconstruction>, Task<HotReloadCompilationListReconstruction>> runInBackground)
        {
            Debug.Assert(store != null, "store must not be null.");
            Debug.Assert(cache != null, "cache must not be null.");
            Debug.Assert(readInputs != null, "readInputs must not be null.");
            Debug.Assert(isCompilationFailed != null, "isCompilationFailed must not be null.");
            Debug.Assert(runInBackground != null, "runInBackground must not be null.");
            _store = store;
            _cache = cache;
            _readInputs = readInputs;
            _isCompilationFailed = isCompilationFailed;
            _runInBackground = runInBackground;
        }

        /// <summary>
        /// Rebuilds the list in the background and keeps it when the cache is still empty and no
        /// invalidation happened meanwhile. Returns whether the list was kept. Never throws: the
        /// cache asks Unity as before when nothing was kept. Cancels an earlier run that is still
        /// going. Call on the main thread.
        /// </summary>
        internal Task<bool> RunAsync()
        {
            _cancellation.Cancel();
            _cancellation = new CancellationTokenSource();
            return RunUntilCancelledAsync(_cancellation.Token);
        }

        /// <summary>
        /// Stops the run in progress, if any: the files it reads are about to change or the domain
        /// ends. Main thread only.
        /// </summary>
        internal void Cancel()
        {
            _cancellation.Cancel();
        }

        private async Task<bool> RunUntilCancelledAsync(CancellationToken ct)
        {
            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                int generation = _cache.Generation;
                HotReloadSnapshotCaptureInputs inputs = _readInputs();
                HotReloadCompilationListReconstruction reconstruction = await _runInBackground(
                    () => HotReloadCompilationAssemblyReconstruction.Reconstruct(_store, inputs, ct)).ConfigureAwait(false);

                // Why the main thread: the cache has no lock and is kept on the main thread only.
                await MainThreadSwitcher.SwitchToMainThread(ct);
                // Why not kept after a failed compile: the dlls are then older than the sources, so
                // every stamp still matches the saved list while Unity's list already names the
                // added and removed files.
                if (reconstruction.Assemblies != null && _isCompilationFailed())
                {
                    LogReconstructed(false, HotReloadConstants.CompilationListReasonCompilationFailed, watch.ElapsedMilliseconds);
                    return false;
                }

                bool filled = reconstruction.Assemblies != null && _cache.TryFill(reconstruction.Assemblies, generation);
                if (filled)
                {
                    // Why saved again: a domain whose cache was filled here never asks Unity, so the
                    // saved list would otherwise age and send more assemblies through their PDBs.
                    _store.SaveInBackgroundIfClean(reconstruction.Assemblies, generation, inputs.ProjectRoot);
                }

                string reason = filled
                    ? null
                    : reconstruction.FailureReason ?? HotReloadConstants.CompilationListReasonSuperseded;
                LogReconstructed(filled, reason, watch.ElapsedMilliseconds);
                return filled;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                LogReconstructed(false, HotReloadConstants.CompilationListReasonCancelled, watch.ElapsedMilliseconds);
                return false;
            }
            catch (Exception ex)
            {
                // Approved deviation: this is the boundary of work that started at Editor startup,
                // and a list that cannot be rebuilt only leaves the cache to ask Unity as before.
                string reason = ex.GetType().Name;
                LogReconstructed(false, reason, watch.ElapsedMilliseconds);
                return false;
            }
        }

        private static void LogReconstructed(bool filled, string reason, long reconstructMs)
        {
            VibeLogger.LogInfo(
                HotReloadConstants.VibeLogCompilationListReconstructed,
                "Hot reload rebuilt the compilation assembly list at startup.",
                new { filled, reason, reconstructMs });
        }
    }
}
