using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using NUnit.Framework;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// EditMode coverage for the source snapshot capture the Editor startup runs before this
    /// domain serves any command. Works on a capture installed in place of the production one.
    /// </summary>
    public class HotReloadEditorStartupTests
    {
        /// <summary>
        /// What: the capture before serving commands runs the installed capture once and records the domain-load trigger.
        /// </summary>
        [Test]
        public void StartSourceSnapshotCaptureBeforeServingCommands_RunsTheInstalledCaptureOnceWithTheDomainLoadTrigger()
        {
            VibeLogger.ClearMemoryLogs();
            int captureCount = 0;
            HotReloadSourceSnapshotCapture capture = HotReloadSourceSnapshotCaptureTestDoubles.CompletingSynchronously(() =>
            {
                captureCount++;
                return true;
            });

            using (HotReloadServicesTestScope.BeginWithSourceSnapshotCapture(capture))
            {
                HotReloadEditorStartup.StartSourceSnapshotCaptureBeforeServingCommands(AllToolsEnabled);
            }

            Assert.That(captureCount, Is.EqualTo(1), "captures");
            JObject context = HotReloadWarmUpTestDoubles.ReadSingleVibeContext(HotReloadConstants.VibeLogSourceSnapshotCaptured);
            Assert.That((string)context["trigger"], Is.EqualTo(HotReloadConstants.SourceSnapshotCaptureTriggerDomainLoad));
        }

        /// <summary>
        /// What: a capture that fails before serving commands does not throw into the domain load,
        /// which never waits for it; it logs one failed entry and stays undone, so the first update
        /// tick's capture runs it again.
        /// </summary>
        [Test]
        public void StartSourceSnapshotCaptureBeforeServingCommands_WhenTheCaptureFails_LogsFailedAndLeavesItForTheNextCall()
        {
            VibeLogger.ClearMemoryLogs();
            int captureCount = 0;
            HotReloadSourceSnapshotCapture capture = HotReloadSourceSnapshotCaptureTestDoubles.CompletingSynchronously(() =>
            {
                captureCount++;
                if (captureCount == 1)
                {
                    throw new InvalidOperationException("The capture failed.");
                }

                return true;
            });

            using (HotReloadServicesTestScope.BeginWithSourceSnapshotCapture(capture))
            {
                Assert.DoesNotThrow(
                    () => HotReloadEditorStartup.StartSourceSnapshotCaptureBeforeServingCommands(AllToolsEnabled));
                JObject failed = HotReloadWarmUpTestDoubles.ReadSingleVibeContext(
                    HotReloadConstants.VibeLogSourceSnapshotCaptureFailed);
                Assert.That((string)failed["trigger"], Is.EqualTo(HotReloadConstants.SourceSnapshotCaptureTriggerDomainLoad));

                HotReloadEditorStartup.CaptureAtStartupUnlessReadersAreDisabled(
                    HotReloadConstants.SourceSnapshotCaptureTriggerFirstUpdateTick, AllToolsEnabled);
            }

            Assert.That(captureCount, Is.EqualTo(2), "captures");
            Assert.That(CountVibeEntries(HotReloadConstants.VibeLogSourceSnapshotCaptured), Is.EqualTo(1), "captured entries");
        }

        /// <summary>
        /// What: with both tools that read the snapshot disabled, the capture before serving
        /// commands is skipped and logged, and the skip leaves the capture undone, so the first
        /// reader that runs later captures through the gate.
        /// </summary>
        [Test]
        public void StartSourceSnapshotCaptureBeforeServingCommands_WhenNeitherReaderToolIsEnabled_SkipsAndLeavesTheCaptureForAReader()
        {
            VibeLogger.ClearMemoryLogs();
            int captureCount = 0;
            HotReloadSourceSnapshotCapture capture = CountingCapture(() => captureCount++);

            using (HotReloadServicesTestScope.BeginWithSourceSnapshotCapture(capture))
            {
                HotReloadEditorStartup.StartSourceSnapshotCaptureBeforeServingCommands(NoToolEnabled);

                Assert.That(captureCount, Is.EqualTo(0), "captures while both readers are disabled");
                JObject skipped = HotReloadWarmUpTestDoubles.ReadSingleVibeContext(HotReloadConstants.VibeLogSourceSnapshotSkipped);
                Assert.That((string)skipped["trigger"], Is.EqualTo(HotReloadConstants.SourceSnapshotCaptureTriggerDomainLoad));
                Assert.That((string)skipped["reason"], Is.EqualTo(HotReloadConstants.SourceSnapshotSkipReasonToolsDisabled));
                Assert.That(CountVibeEntries(HotReloadConstants.VibeLogSourceSnapshotCaptured), Is.EqualTo(0), "captured entries");

                HotReloadCompositionRoot.Services.SourceSnapshotCapture.EnsureCapturedBlocking(
                    HotReloadConstants.SourceSnapshotCaptureTriggerApply);
            }

            Assert.That(captureCount, Is.EqualTo(1), "captures once a reader asks the gate");
        }

        /// <summary>
        /// What: one enabled tool that reads the snapshot is enough for the capture before serving
        /// commands to run.
        /// </summary>
        [TestCase(UnityCliLoopConstants.TOOL_NAME_HOT_RELOAD)]
        [TestCase(UnityCliLoopConstants.SETTINGS_TOOL_NAME_PAUSE_POINT)]
        public void StartSourceSnapshotCaptureBeforeServingCommands_WhenOnlyOneReaderToolIsEnabled_Captures(string enabledTool)
        {
            VibeLogger.ClearMemoryLogs();
            int captureCount = 0;
            HotReloadSourceSnapshotCapture capture = CountingCapture(() => captureCount++);

            using (HotReloadServicesTestScope.BeginWithSourceSnapshotCapture(capture))
            {
                HotReloadEditorStartup.StartSourceSnapshotCaptureBeforeServingCommands(name => name == enabledTool);
            }

            Assert.That(captureCount, Is.EqualTo(1), "captures");
            JObject context = HotReloadWarmUpTestDoubles.ReadSingleVibeContext(HotReloadConstants.VibeLogSourceSnapshotCaptured);
            Assert.That((string)context["trigger"], Is.EqualTo(HotReloadConstants.SourceSnapshotCaptureTriggerDomainLoad));
        }

        /// <summary>
        /// What: the startup capture asks about both reader tools by the names the tool settings
        /// store them under, hot-reload first.
        /// </summary>
        [Test]
        public void CaptureAtStartupUnlessReadersAreDisabled_WhenNeitherIsEnabled_AsksBothReaderToolsBySettingsName()
        {
            VibeLogger.ClearMemoryLogs();
            List<string> askedNames = new List<string>();
            HotReloadSourceSnapshotCapture capture = CountingCapture(() => { });

            using (HotReloadServicesTestScope.BeginWithSourceSnapshotCapture(capture))
            {
                HotReloadEditorStartup.CaptureAtStartupUnlessReadersAreDisabled(
                    HotReloadConstants.SourceSnapshotCaptureTriggerDomainLoad,
                    name =>
                    {
                        askedNames.Add(name);
                        return false;
                    });
            }

            Assert.That(
                askedNames,
                Is.EqualTo(new[] { UnityCliLoopConstants.TOOL_NAME_HOT_RELOAD, UnityCliLoopConstants.SETTINGS_TOOL_NAME_PAUSE_POINT }));
        }

        /// <summary>
        /// What: the first update tick's capture is skipped the same way when both reader tools are
        /// disabled, and the skip names that trigger.
        /// </summary>
        [Test]
        public void CaptureAtStartupUnlessReadersAreDisabled_AtTheFirstUpdateTick_WhenNeitherIsEnabled_SkipsWithThatTrigger()
        {
            VibeLogger.ClearMemoryLogs();
            int captureCount = 0;
            HotReloadSourceSnapshotCapture capture = CountingCapture(() => captureCount++);

            using (HotReloadServicesTestScope.BeginWithSourceSnapshotCapture(capture))
            {
                HotReloadEditorStartup.CaptureAtStartupUnlessReadersAreDisabled(
                    HotReloadConstants.SourceSnapshotCaptureTriggerFirstUpdateTick,
                    NoToolEnabled);
            }

            Assert.That(captureCount, Is.EqualTo(0), "captures");
            JObject skipped = HotReloadWarmUpTestDoubles.ReadSingleVibeContext(HotReloadConstants.VibeLogSourceSnapshotSkipped);
            Assert.That((string)skipped["trigger"], Is.EqualTo(HotReloadConstants.SourceSnapshotCaptureTriggerFirstUpdateTick));
        }

        /// <summary>
        /// What: with the hot-reload tool disabled the warm-up is not started, and the skip is logged
        /// with its reason.
        /// </summary>
        [Test]
        public void StartWarmUpUnlessHotReloadIsDisabled_WhenHotReloadIsDisabled_DoesNotStartAndLogsTheReason()
        {
            VibeLogger.ClearMemoryLogs();
            int startCount = 0;

            HotReloadEditorStartup.StartWarmUpUnlessHotReloadIsDisabled(
                name => name != UnityCliLoopConstants.TOOL_NAME_HOT_RELOAD,
                () => startCount++);

            Assert.That(startCount, Is.EqualTo(0), "warm-up starts");
            JObject skipped = HotReloadWarmUpTestDoubles.ReadSingleVibeContext(HotReloadConstants.VibeLogWarmUpSkipped);
            Assert.That((string)skipped["reason"], Is.EqualTo(HotReloadConstants.WarmUpSkipReasonToolDisabled));
        }

        /// <summary>
        /// What: with the hot-reload tool enabled the warm-up is started once and no skip is logged.
        /// </summary>
        [Test]
        public void StartWarmUpUnlessHotReloadIsDisabled_WhenHotReloadIsEnabled_StartsOnce()
        {
            VibeLogger.ClearMemoryLogs();
            int startCount = 0;

            HotReloadEditorStartup.StartWarmUpUnlessHotReloadIsDisabled(
                name => name == UnityCliLoopConstants.TOOL_NAME_HOT_RELOAD,
                () => startCount++);

            Assert.That(startCount, Is.EqualTo(1), "warm-up starts");
            Assert.That(CountVibeEntries(HotReloadConstants.VibeLogWarmUpSkipped), Is.EqualTo(0), "skipped entries");
        }

        /// <summary>
        /// What: with the hot-reload tool disabled the list is not rebuilt even though the
        /// pause-point tool is enabled, because only hot reload reads the list.
        /// </summary>
        [Test]
        public void ReconstructAndFill_WhenHotReloadIsDisabled_DoesNotStartEvenIfPausePointIsEnabled()
        {
            VibeLogger.ClearMemoryLogs();
            int startCount = 0;

            Task<bool> refill = HotReloadEditorStartup.RefillCompilationListUnlessHotReloadIsDisabled(
                name => name == UnityCliLoopConstants.SETTINGS_TOOL_NAME_PAUSE_POINT,
                () =>
                {
                    startCount++;
                    return Task.FromResult(true);
                });

            Assert.That(startCount, Is.EqualTo(0), "rebuilds started");
            Assert.That(refill.Status, Is.EqualTo(TaskStatus.RanToCompletion), "refill status");
            Assert.That(refill.Result, Is.False, "kept");
            JObject logged = HotReloadWarmUpTestDoubles.ReadSingleVibeContext(HotReloadConstants.VibeLogCompilationListReconstructed);
            Assert.That((string)logged["reason"], Is.EqualTo(HotReloadConstants.CompilationListReasonToolDisabled));
        }

        /// <summary>
        /// What: with the hot-reload tool enabled the rebuild is started once.
        /// </summary>
        [Test]
        public void ReconstructAndFill_WhenHotReloadIsEnabled_StartsOnce()
        {
            int startCount = 0;

            HotReloadEditorStartup.RefillCompilationListUnlessHotReloadIsDisabled(
                name => name == UnityCliLoopConstants.TOOL_NAME_HOT_RELOAD,
                () =>
                {
                    startCount++;
                    return Task.FromResult(true);
                });

            Assert.That(startCount, Is.EqualTo(1), "rebuilds started");
        }

        private static bool AllToolsEnabled(string toolName) => true;

        private static bool NoToolEnabled(string toolName) => false;

        /// <summary>
        /// What: the warm-up does not start, so neither chooses its targets nor runs an item, until
        /// the domain's attempt to rebuild the compilation assembly list has ended; then it starts once.
        /// </summary>
        [Test]
        public async Task StartWarmUpOnceTheCompilationListIsReady_StartsOnlyAfterTheRebuildEnded()
        {
            TaskCompletionSource<bool> ready = new TaskCompletionSource<bool>();
            int startCount = 0;

            Task started = HotReloadEditorStartup.StartWarmUpOnceTheCompilationListIsReady(ready.Task, () => startCount++);

            Assert.That(startCount, Is.EqualTo(0), "started before the rebuild ended");
            Assert.That(started.IsCompleted, Is.False, "completed before the rebuild ended");
            ready.SetResult(false);
            await started;
            Assert.That(started.Status, Is.EqualTo(TaskStatus.RanToCompletion));
            Assert.That(startCount, Is.EqualTo(1));
        }

        /// <summary>
        /// What: when the rebuild ends on a pool thread, as a cancelled one does, the warm-up still
        /// starts on the main thread, where it reads Unity state and the kept list.
        /// </summary>
        [Test]
        public async Task StartWarmUpOnceTheCompilationListIsReady_WhenTheRebuildEndsOnAPoolThread_StartsOnTheMainThread()
        {
            List<bool> startedOnMainThread = new List<bool>();

            // Why a delay: its completion runs on a timer thread, so a missing switch would start there.
            Task started = HotReloadEditorStartup.StartWarmUpOnceTheCompilationListIsReady(
                Task.Delay(1), () => startedOnMainThread.Add(MainThreadSwitcher.IsMainThread));
            await started;

            Assert.That(started.Status, Is.EqualTo(TaskStatus.RanToCompletion));
            Assert.That(startedOnMainThread, Is.EqualTo(new[] { true }));
        }

        private static HotReloadSourceSnapshotCapture CountingCapture(Action onCapture)
        {
            return HotReloadSourceSnapshotCaptureTestDoubles.CompletingSynchronously(() =>
            {
                onCapture();
                return true;
            });
        }

        private static int CountVibeEntries(string operation)
        {
            return JArray.Parse(VibeLogger.GetLogsForAi(operation)).Count;
        }
    }
}
