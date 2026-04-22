using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP
{
    public class McpServerControllerStartupLockTests
    {
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

            McpServerController.ScheduleConfigAutoUpdate(
                8901,
                action => deferredAction = action,
                port => updatedPort = port,
                () => completionCount++,
                () => logCount++);

            Assert.That(updatedPort, Is.EqualTo(0));
            Assert.That(completionCount, Is.EqualTo(0));
            Assert.That(logCount, Is.EqualTo(0));
            Assert.That(deferredAction, Is.Not.Null);

            deferredAction();

            Assert.That(updatedPort, Is.EqualTo(8901));
            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(logCount, Is.EqualTo(1));
        }
    }
}
