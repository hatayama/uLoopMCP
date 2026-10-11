using System;
using System.IO;
using System.Threading.Tasks;

using UnityEditor;
using UnityEngine;

using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    // Keeps hot-reload startup wiring inside the hot-reload assembly so the composition
    // root only depends on the bundled-tool facade.
    internal static class HotReloadEditorStartup
    {
        public static void Initialize(Func<string, bool> isToolEnabled)
        {
            Debug.Assert(isToolEnabled != null, "isToolEnabled must not be null.");

            // Why first: the snapshot capture no longer touches the compilation assembly list, so
            // the list's static constructor, which subscribes to compilationStarted, would otherwise
            // run for whichever caller touches the list first, which need not be on the main thread.
            HotReloadCompilationAssemblies.EnsureInitialized();
            // Why at domain load: the rebuild then runs while the Editor finishes loading, so the
            // first caller of the domain finds the list kept instead of asking Unity on the main thread.
            _ = RefillCompilationListUnlessHotReloadIsDisabled(isToolEnabled, HotReloadCompilationAssemblies.RefillInBackground);

            // Why not EditorApplication.delayCall: a cold-start session that hits Unity's native
            // "Scripts have compiler errors" dialog never flushes delayCall again for the rest of
            // that process's lifetime, even for later registrations — while
            // EditorApplication.update keeps ticking (see SetupWizardWindow.cs:56-70). The
            // capture starts at the end of the domain load; this tick starts another only when that
            // one failed, found no compiled assembly, or was cancelled, and the gate does nothing
            // when it is still running or already succeeded.
            void CaptureOnFirstUpdateTick()
            {
                EditorApplication.update -= CaptureOnFirstUpdateTick;
                CaptureAtStartupUnlessReadersAreDisabled(
                    HotReloadConstants.SourceSnapshotCaptureTriggerFirstUpdateTick, isToolEnabled);
            }

            // Why a callback of its own rather than a line in the capture above: an exception in
            // one of the two must not keep the other from running. Why unsubscribe first: a sweep
            // that throws is then not retried on every later tick.
            void SweepArtifactsOnFirstUpdateTick()
            {
                EditorApplication.update -= SweepArtifactsOnFirstUpdateTick;
                HotReloadIntroducedTypePreparation.SweepArtifactsOfEarlierDomains(
                    Path.GetFullPath(Path.Combine(Application.dataPath, "..")));
            }

            // Why after the capture and the sweep: the first run of this domain reads the snapshot
            // the capture writes, and the warm-up only reads what a run would read; neither must
            // wait for the other to be scheduled.
            void StartWarmUpOnFirstUpdateTick()
            {
                EditorApplication.update -= StartWarmUpOnFirstUpdateTick;
                StartWarmUpUnlessHotReloadIsDisabled(
                    isToolEnabled, () => HotReloadCompositionRoot.Services.WarmUp.Start());
            }

            // The services are rebuilt here rather than on first use because the introduced type
            // resolver subscribes to AppDomain.AssemblyResolve when it is built, and that
            // subscription is lost on every domain reload.
            HotReloadCompositionRoot.Initialize();
            // Why right after the services exist: a reload after Play entry has to see the
            // companions the reloads before it were given, and nothing else can name them.
            HotReloadCompanionSourceSessionStore.Load(HotReloadCompositionRoot.Services.Domain.CompanionSources);
            // Why here and not in a static constructor of the counting side: a static constructor
            // runs when something first touches that type, which a domain that only introduced a
            // type may never do, and the tools that warn about a domain reload would then read a
            // null delegate as "nothing to lose".
            HotReloadRuntimeChangeCoordination.GetActiveRuntimeChangeCount =
                () => HotReloadCompositionRoot.Services.Domain.CountActiveChanges().RuntimeChangeTotal;
            // Why here, like the count above: get-compile-status reads it on every poll, which can
            // come before anything in this assembly is touched.
            HotReloadSnapshotCaptureCoordination.IsHoldingCompileCompletion =
                () => HotReloadCompositionRoot.Services.SourceSnapshotCapture.IsHoldingCompileCompletion;
            // Why here, like the count above: the dynamic-code tool reads it after each server
            // reset, which can come before anything in this assembly is touched.
            string warmUpProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            HotReloadSharedCompilerWarmUpReferenceSource warmUpReferences = new HotReloadSharedCompilerWarmUpReferenceSource(
                warmUpProjectRoot,
                HotReloadWarmUpTargetLedger.Read,
                HotReloadCompilationAssemblies.FindByName);
            SharedCompilerWarmUpCoordination.CollectWarmUpReferencePaths = warmUpReferences.CollectAsync;
            // Why the same shape for these two: both run from Editor callbacks that take no
            // argument, so they have to read whichever services are installed when they fire.
            HotReloadAutoRefreshHold.GetServices = () => HotReloadCompositionRoot.Services;
            HotReloadPlayModeEntryDropRecorder.GetServices = () => HotReloadCompositionRoot.Services;
            HotReloadWarmUpEditorHooks.GetServices = () => HotReloadCompositionRoot.Services;
            HotReloadUnityMessageForwardingEditorHooks.GetForwarding =
                () => HotReloadCompositionRoot.Services.UnityMessageForwarding;
            HotReloadWiredValueEditorHooks.GetPersistence =
                () => HotReloadCompositionRoot.Services.WiredValuePersistence;
            EditorApplication.update += CaptureOnFirstUpdateTick;
            EditorApplication.update += SweepArtifactsOnFirstUpdateTick;
            EditorApplication.update += StartWarmUpOnFirstUpdateTick;
            HotReloadPlayModeEntryDropRecorder.Initialize();
            HotReloadAutoRefreshHold.Initialize();
            HotReloadWarmUpEditorHooks.Initialize();
            HotReloadCompileStartRecord.Initialize();
            HotReloadUnityMessageForwardingEditorHooks.Initialize();
            HotReloadWiredValueEditorHooks.Initialize();
            TransformWorkerHostLifecycle.RegisterForEditorStartup();
        }

        /// <summary>
        /// Starts the source snapshot capture of the current compile before this domain answers any
        /// uloop command, without waiting for it. The guarantee that an edit made after the compile
        /// returned is never captured as compiled source comes from get-compile-status holding the
        /// compile completion while the capture runs (up to 30 seconds).
        /// </summary>
        internal static void StartSourceSnapshotCaptureBeforeServingCommands(Func<string, bool> isToolEnabled)
        {
            CaptureAtStartupUnlessReadersAreDisabled(
                HotReloadConstants.SourceSnapshotCaptureTriggerDomainLoad, isToolEnabled);
        }

        /// <summary>
        /// Starts the source snapshot capture at a startup trigger unless both tools that read it are
        /// disabled in the tool settings.
        /// </summary>
        internal static void CaptureAtStartupUnlessReadersAreDisabled(string trigger, Func<string, bool> isToolEnabled)
        {
            Debug.Assert(!string.IsNullOrEmpty(trigger), "trigger must not be null or empty.");
            Debug.Assert(isToolEnabled != null, "isToolEnabled must not be null.");

            // Why only the startup triggers ask: a reader that runs (a hot reload run, a pause point)
            // belongs to an enabled tool and captures through the gate itself. A capture that comes
            // late stays correct, because a source written after the compile started is checked
            // against the PDB.
            bool anyReaderEnabled = isToolEnabled(UnityCliLoopConstants.TOOL_NAME_HOT_RELOAD)
                || isToolEnabled(UnityCliLoopConstants.SETTINGS_TOOL_NAME_PAUSE_POINT);
            if (!anyReaderEnabled)
            {
                // Why the gate is not marked: a tool enabled later in this domain must still
                // capture through it before its first read.
                VibeLogger.LogInfo(
                    HotReloadConstants.VibeLogSourceSnapshotSkipped,
                    "Hot reload source snapshot capture skipped.",
                    new { trigger, reason = HotReloadConstants.SourceSnapshotSkipReasonToolsDisabled });
                return;
            }

            // Why the task is not awaited: the domain load must not wait for the capture, and the
            // gate logs how it ended. A caller that needs the snapshot waits for it itself.
            _ = HotReloadCompositionRoot.Services.SourceSnapshotCapture.StartIfNeeded(trigger);
        }

        /// <summary>
        /// Starts rebuilding the compilation assembly list unless the hot-reload tool is disabled in
        /// the tool settings. Only hot reload reads the list, so the pause-point tool does not count.
        /// Completes with whether the rebuilt list was kept.
        /// </summary>
        internal static Task<bool> RefillCompilationListUnlessHotReloadIsDisabled(
            Func<string, bool> isToolEnabled,
            Func<Task<bool>> startRefill)
        {
            Debug.Assert(isToolEnabled != null, "isToolEnabled must not be null.");
            Debug.Assert(startRefill != null, "startRefill must not be null.");

            if (!isToolEnabled(UnityCliLoopConstants.TOOL_NAME_HOT_RELOAD))
            {
                VibeLogger.LogInfo(
                    HotReloadConstants.VibeLogCompilationListReconstructed,
                    "Hot reload did not rebuild the compilation assembly list.",
                    new { filled = false, reason = HotReloadConstants.CompilationListReasonToolDisabled });
                return Task.FromResult(false);
            }

            return startRefill();
        }

        /// <summary>
        /// Starts the warm-up unless the hot-reload tool is disabled in the tool settings.
        /// </summary>
        internal static void StartWarmUpUnlessHotReloadIsDisabled(Func<string, bool> isToolEnabled, Action startWarmUp)
        {
            Debug.Assert(isToolEnabled != null, "isToolEnabled must not be null.");
            Debug.Assert(startWarmUp != null, "startWarmUp must not be null.");

            if (!isToolEnabled(UnityCliLoopConstants.TOOL_NAME_HOT_RELOAD))
            {
                VibeLogger.LogInfo(
                    HotReloadConstants.VibeLogWarmUpSkipped,
                    "Hot reload warm-up skipped.",
                    new { reason = HotReloadConstants.WarmUpSkipReasonToolDisabled });
                return;
            }

            startWarmUp();
        }
    }
}
