using System.Collections.Generic;

using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP
{
    [TestFixture]
    public class EditorStartupCoordinatorTests
    {
        [SetUp]
        public void SetUp()
        {
            EditorStartupTelemetry.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            EditorStartupTelemetry.Reset();
        }

        [Test]
        public void InitializeOnEditorLoad_WhenAssetImportWorkerProcess_DoesNothing()
        {
            bool actionExecuted = false;

            bool initialized = EditorStartupCoordinator.InitializeOnEditorLoad(
                isAssetImportWorkerProcess: true,
                isBatchMode: false,
                () => actionExecuted = true,
                () => actionExecuted = true,
                () => actionExecuted = true,
                () => actionExecuted = true,
                _ => actionExecuted = true);

            Assert.That(initialized, Is.False);
            Assert.That(actionExecuted, Is.False);
            Assert.That(EditorStartupTelemetry.CreateTimingEntries(), Is.Empty);
        }

        [Test]
        public void InitializeOnEditorLoad_WhenBatchMode_DoesNothing()
        {
            bool actionExecuted = false;

            bool initialized = EditorStartupCoordinator.InitializeOnEditorLoad(
                isAssetImportWorkerProcess: false,
                isBatchMode: true,
                () => actionExecuted = true,
                () => actionExecuted = true,
                () => actionExecuted = true,
                () => actionExecuted = true,
                _ => actionExecuted = true);

            Assert.That(initialized, Is.False);
            Assert.That(actionExecuted, Is.False);
            Assert.That(EditorStartupTelemetry.CreateTimingEntries(), Is.Empty);
        }

        [Test]
        public void InitializeOnEditorLoad_WhenEditorStartup_RunsActionsInOrderAndLogsTiming()
        {
            List<string> callOrder = new();
            IReadOnlyCollection<string> loggedEntries = null;

            bool initialized = EditorStartupCoordinator.InitializeOnEditorLoad(
                isAssetImportWorkerProcess: false,
                isBatchMode: false,
                () => callOrder.Add("recover"),
                () => callOrder.Add("ensure-server"),
                () => callOrder.Add("schedule-setup"),
                () => callOrder.Add("schedule-recovery"),
                entries => loggedEntries = entries);

            Assert.That(initialized, Is.True);
            Assert.That(
                callOrder,
                Is.EqualTo(new[] { "recover", "ensure-server", "schedule-setup", "schedule-recovery" }));
            Assert.That(loggedEntries, Is.Not.Null);
            Assert.That(loggedEntries, Has.Count.EqualTo(1));
            Assert.That(new List<string>(loggedEntries)[0], Does.StartWith("[Perf] StartupSyncDuration: "));
        }

        [Test]
        public void ScheduleStartupRecovery_WhenCalled_OnlyRunsRecoveryAfterDeferredCallback()
        {
            System.Action deferredAction = null;
            bool recoveryExecuted = false;

            McpServerController.ScheduleStartupRecovery(
                scheduleDelayCall => deferredAction = scheduleDelayCall,
                () => recoveryExecuted = true);

            Assert.That(recoveryExecuted, Is.False);
            Assert.That(deferredAction, Is.Not.Null);

            deferredAction();

            Assert.That(recoveryExecuted, Is.True);
        }
    }
}
