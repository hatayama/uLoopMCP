using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    [TestFixture]
    public class DynamicCodeStartupTelemetryTests
    {
        [SetUp]
        public void SetUp()
        {
            DynamicCodeStartupTelemetry.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            DynamicCodeStartupTelemetry.Reset();
        }

        [Test]
        public void CreateTimingEntries_WhenPrewarmCompletes_ShouldReportWarmReady()
        {
            DynamicCodeStartupTelemetry.MarkRecoveryStarted();
            DynamicCodeStartupTelemetry.MarkRecoveryBindCompleted();
            DynamicCodeStartupTelemetry.MarkRecoveryConfigCompleted();
            DynamicCodeStartupTelemetry.MarkServerReady();
            DynamicCodeStartupTelemetry.MarkPrewarmQueued();
            DynamicCodeStartupTelemetry.MarkPrewarmStarted();
            DynamicCodeStartupTelemetry.MarkPrewarmCompleted();

            System.Collections.Generic.List<string> entries =
                DynamicCodeStartupTelemetry.CreateTimingEntries();

            Assert.That(entries, Has.Member("[Perf] WarmReady: True"));
            Assert.That(entries, Has.Member("[Perf] PrewarmState: Completed"));
            Assert.That(entries, Has.Some.Contains("[Perf] RecoveryDuration:"));
            Assert.That(entries, Has.Some.Contains("[Perf] RecoveryBindDuration:"));
            Assert.That(entries, Has.Some.Contains("[Perf] RecoveryConfigDuration:"));
            Assert.That(entries, Has.Some.Contains("[Perf] RecoveryFinalizeDuration:"));
            Assert.That(entries, Has.Some.Contains("[Perf] ServerReadyAge:"));
            Assert.That(entries, Has.Some.Contains("[Perf] PrewarmDuration:"));
        }

        [Test]
        public void CreateTimingEntries_WhenPrewarmIsSkipped_ShouldExposeDetail()
        {
            DynamicCodeStartupTelemetry.MarkServerReady();
            DynamicCodeStartupTelemetry.MarkPrewarmQueued();
            DynamicCodeStartupTelemetry.MarkPrewarmSkipped("fast_path_unavailable");

            System.Collections.Generic.List<string> entries =
                DynamicCodeStartupTelemetry.CreateTimingEntries();

            Assert.That(entries, Has.Member("[Perf] WarmReady: False"));
            Assert.That(entries, Has.Member("[Perf] PrewarmState: Skipped"));
            Assert.That(entries, Has.Member("[Perf] PrewarmDetail: fast_path_unavailable"));
        }

        [Test]
        public void MarkServerReady_WhenPreviousServerWasWarm_ShouldResetPrewarmState()
        {
            DynamicCodeStartupTelemetry.MarkServerReady();
            DynamicCodeStartupTelemetry.MarkPrewarmQueued();
            DynamicCodeStartupTelemetry.MarkPrewarmStarted();
            DynamicCodeStartupTelemetry.MarkPrewarmCompleted();

            DynamicCodeStartupTelemetry.MarkServerReady();

            System.Collections.Generic.List<string> entries =
                DynamicCodeStartupTelemetry.CreateTimingEntries();

            Assert.That(entries, Has.Member("[Perf] WarmReady: False"));
            Assert.That(entries, Has.Member("[Perf] PrewarmState: NotRequested"));
            Assert.That(entries, Has.Some.Contains("[Perf] ServerReadyAge:"));
            Assert.That(entries, Has.None.Contains("[Perf] PrewarmDuration:"));
        }

        [Test]
        public void Reset_WhenPreviousServerWasWarm_ShouldClearServerReadyAge()
        {
            DynamicCodeStartupTelemetry.MarkServerReady();
            DynamicCodeStartupTelemetry.MarkPrewarmQueued();
            DynamicCodeStartupTelemetry.MarkPrewarmStarted();
            DynamicCodeStartupTelemetry.MarkPrewarmCompleted();

            DynamicCodeStartupTelemetry.Reset();

            System.Collections.Generic.List<string> entries =
                DynamicCodeStartupTelemetry.CreateTimingEntries();

            Assert.That(entries, Has.Member("[Perf] WarmReady: False"));
            Assert.That(entries, Has.Member("[Perf] PrewarmState: NotRequested"));
            Assert.That(entries, Has.None.Contains("[Perf] ServerReadyAge:"));
            Assert.That(entries, Has.None.Contains("[Perf] PrewarmDuration:"));
        }

        [Test]
        public void MarkPrewarmQueued_WhenPreviousAttemptCompleted_ShouldClearFinishedTimestamp()
        {
            DynamicCodeStartupTelemetry.MarkServerReady();
            DynamicCodeStartupTelemetry.MarkPrewarmStarted();
            DynamicCodeStartupTelemetry.MarkPrewarmCompleted();

            DynamicCodeStartupTelemetry.MarkPrewarmQueued();

            System.Collections.Generic.List<string> entries =
                DynamicCodeStartupTelemetry.CreateTimingEntries();

            Assert.That(entries, Has.Member("[Perf] PrewarmState: Queued"));
            Assert.That(entries, Has.None.Contains("[Perf] PrewarmDuration:"));
        }

        [Test]
        public void Reset_WhenRecoveryWasMeasured_ShouldClearRecoveryEntries()
        {
            DynamicCodeStartupTelemetry.MarkRecoveryStarted();
            DynamicCodeStartupTelemetry.MarkRecoveryBindCompleted();
            DynamicCodeStartupTelemetry.MarkRecoveryConfigCompleted();
            DynamicCodeStartupTelemetry.MarkServerReady();

            DynamicCodeStartupTelemetry.Reset();

            System.Collections.Generic.List<string> entries =
                DynamicCodeStartupTelemetry.CreateTimingEntries();

            Assert.That(entries, Has.None.Contains("[Perf] RecoveryDuration:"));
            Assert.That(entries, Has.None.Contains("[Perf] RecoveryBindDuration:"));
            Assert.That(entries, Has.None.Contains("[Perf] RecoveryConfigDuration:"));
            Assert.That(entries, Has.None.Contains("[Perf] RecoveryFinalizeDuration:"));
        }

        [Test]
        public void CreateTimingEntries_WhenConfigUpdateFinishesAfterServerReady_ShouldReportDeferredDuration()
        {
            DynamicCodeStartupTelemetry.MarkRecoveryStarted();
            DynamicCodeStartupTelemetry.MarkRecoveryBindCompleted();
            DynamicCodeStartupTelemetry.MarkServerReady();
            DynamicCodeStartupTelemetry.MarkRecoveryConfigCompleted();

            System.Collections.Generic.List<string> entries =
                DynamicCodeStartupTelemetry.CreateTimingEntries();

            Assert.That(entries, Has.Some.Contains("[Perf] RecoveryDuration:"));
            Assert.That(entries, Has.Some.Contains("[Perf] RecoveryBindDuration:"));
            Assert.That(entries, Has.Some.Contains("[Perf] RecoveryFinalizeDuration:"));
            Assert.That(entries, Has.Some.Contains("[Perf] DeferredConfigUpdateDuration:"));
            Assert.That(entries, Has.None.Contains("[Perf] RecoveryConfigDuration:"));
        }

        [Test]
        public void GetRecoveryFinalizeStartedTimestamp_WhenConfigCompletedBeforeServerReady_ReturnsConfigTimestamp()
        {
            long recoveryFinalizeStartedTimestamp =
                DynamicCodeStartupTelemetry.GetRecoveryFinalizeStartedTimestamp(10, 20, 30);

            Assert.That(recoveryFinalizeStartedTimestamp, Is.EqualTo(20));
        }

        [Test]
        public void GetRecoveryFinalizeStartedTimestamp_WhenConfigCompletedAfterServerReady_ReturnsBindTimestamp()
        {
            long recoveryFinalizeStartedTimestamp =
                DynamicCodeStartupTelemetry.GetRecoveryFinalizeStartedTimestamp(10, 40, 30);

            Assert.That(recoveryFinalizeStartedTimestamp, Is.EqualTo(10));
        }
    }
}
