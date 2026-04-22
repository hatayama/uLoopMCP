using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP
{
    public class McpServerControllerStartupLockTests
    {
        [SetUp]
        public void SetUp()
        {
            McpServerController.ResetConfigAutoUpdateBusyStateForTests();
        }

        [Test]
        public void CreateOptionalServerStartingLock_WhenLockCreationSucceeds_ShouldReturnOwnershipToken()
        {
            string token = McpServerController.CreateOptionalServerStartingLock(() => "token-123");

            Assert.That(token, Is.EqualTo("token-123"));
        }

        [Test]
        public void CreateOptionalServerStartingLock_WhenLockCreationFails_ShouldContinueWithoutThrowing()
        {
            string token = McpServerController.CreateOptionalServerStartingLock(() => null);

            Assert.That(token, Is.Null);
        }

        [Test]
        public void ScheduleConfigAutoUpdate_WhenCalled_ShouldDeferUpdateUntilCallbackRuns()
        {
            System.Action deferredAction = null;
            int updatedPort = 0;
            int completionCount = 0;
            int logCount = 0;

            bool scheduled = McpServerController.ScheduleConfigAutoUpdate(
                8901,
                action => deferredAction = action,
                port => updatedPort = port,
                () => completionCount++,
                () => logCount++);

            Assert.That(scheduled, Is.True);
            Assert.That(updatedPort, Is.EqualTo(0));
            Assert.That(completionCount, Is.EqualTo(0));
            Assert.That(logCount, Is.EqualTo(0));
            Assert.That(deferredAction, Is.Not.Null);

            deferredAction();

            Assert.That(updatedPort, Is.EqualTo(8901));
            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(logCount, Is.EqualTo(1));
        }

        [Test]
        public void ScheduleConfigAutoUpdate_WhenAlreadyScheduled_ShouldReturnFalseWithoutSchedulingAgain()
        {
            System.Action deferredAction = null;
            int scheduledCount = 0;

            bool firstScheduled = McpServerController.ScheduleConfigAutoUpdate(
                8901,
                action =>
                {
                    scheduledCount++;
                    deferredAction = action;
                },
                _ => { },
                () => { },
                () => { });

            bool secondScheduled = McpServerController.ScheduleConfigAutoUpdate(
                8901,
                action =>
                {
                    scheduledCount++;
                    deferredAction = action;
                },
                _ => { },
                () => { },
                () => { });

            Assert.That(firstScheduled, Is.True);
            Assert.That(secondScheduled, Is.False);
            Assert.That(scheduledCount, Is.EqualTo(1));
            Assert.That(deferredAction, Is.Not.Null);
        }

        [Test]
        public void ScheduleConfigAutoUpdate_WhenAlreadyRunning_ShouldReturnFalseUntilCurrentRunCompletes()
        {
            System.Action deferredAction = null;
            bool nestedScheduled = true;

            bool firstScheduled = McpServerController.ScheduleConfigAutoUpdate(
                8901,
                action => deferredAction = action,
                _ =>
                {
                    nestedScheduled = McpServerController.ScheduleConfigAutoUpdate(
                        8901,
                        __ => { },
                        ___ => { },
                        () => { },
                        () => { });
                },
                () => { },
                () => { });

            Assert.That(firstScheduled, Is.True);
            Assert.That(deferredAction, Is.Not.Null);

            deferredAction();

            Assert.That(nestedScheduled, Is.False);
        }

        [Test]
        public void ScheduleConfigAutoUpdate_WhenPreviousRunCompleted_ShouldAllowSchedulingAgain()
        {
            System.Action deferredAction = null;
            int scheduledCount = 0;

            bool firstScheduled = McpServerController.ScheduleConfigAutoUpdate(
                8901,
                action =>
                {
                    scheduledCount++;
                    deferredAction = action;
                },
                _ => { },
                () => { },
                () => { });

            Assert.That(firstScheduled, Is.True);
            Assert.That(deferredAction, Is.Not.Null);

            deferredAction();

            bool secondScheduled = McpServerController.ScheduleConfigAutoUpdate(
                8901,
                action =>
                {
                    scheduledCount++;
                    deferredAction = action;
                },
                _ => { },
                () => { },
                () => { });

            Assert.That(secondScheduled, Is.True);
            Assert.That(scheduledCount, Is.EqualTo(2));
            Assert.That(deferredAction, Is.Not.Null);
        }
    }
}
