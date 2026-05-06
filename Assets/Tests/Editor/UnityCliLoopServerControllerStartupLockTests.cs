using NUnit.Framework;
using System.Threading.Tasks;

namespace io.github.hatayama.UnityCliLoop
{
    public class UnityCliLoopServerControllerStartupLockTests
    {
        [Test]
        public void CreateOptionalServerStartingLock_WhenLockCreationSucceeds_ShouldReturnOwnershipToken()
        {
            string token = UnityCliLoopServerController.CreateOptionalServerStartingLock(() => "token-123");

            Assert.That(token, Is.EqualTo("token-123"));
        }

        [Test]
        public void CreateOptionalServerStartingLock_WhenLockCreationFails_ShouldContinueWithoutThrowing()
        {
            string token = UnityCliLoopServerController.CreateOptionalServerStartingLock(() => null);

            Assert.That(token, Is.Null);
        }

        [Test]
        public void ScheduleStartupRecovery_WhenCalled_ExposesRecoveryTaskBeforeDeferredActionRuns()
        {
            System.Action scheduledAction = null;
            bool recoveryExecuted = false;

            Task recoveryTask = UnityCliLoopServerController.ScheduleStartupRecovery(
                action => scheduledAction = action,
                () =>
                {
                    recoveryExecuted = true;
                    return Task.CompletedTask;
                });

            Assert.That(recoveryExecuted, Is.False);
            Assert.That(scheduledAction, Is.Not.Null);
            Assert.That(recoveryTask, Is.SameAs(UnityCliLoopServerController.RecoveryTask));
            Assert.That(recoveryTask.IsCompleted, Is.False);

            scheduledAction();

            Assert.That(recoveryExecuted, Is.True);
            Assert.That(recoveryTask.IsCompleted, Is.True);
            Assert.That(UnityCliLoopServerController.RecoveryTask, Is.Null);
        }

        [Test]
        public void ScheduleStartupRecovery_WhenRecoveryThrowsSynchronously_FaultsTaskAndClearsRecoveryTask()
        {
            System.Action scheduledAction = null;

            Task recoveryTask = UnityCliLoopServerController.ScheduleStartupRecovery(
                action => scheduledAction = action,
                () => throw new System.InvalidOperationException("restore failed"));

            scheduledAction();

            Assert.That(recoveryTask.IsFaulted, Is.True);
            Assert.That(UnityCliLoopServerController.RecoveryTask, Is.Null);
            Assert.ThrowsAsync<System.InvalidOperationException>(async () => await recoveryTask);
        }

        [Test]
        public async Task ScheduleStartupRecovery_WhenRecoveryIsAsync_KeepsTaskIncompleteUntilRecoveryCompletes()
        {
            System.Action scheduledAction = null;
            TaskCompletionSource<bool> recoveryCompletionSource = new TaskCompletionSource<bool>();

            Task recoveryTask = UnityCliLoopServerController.ScheduleStartupRecovery(
                action => scheduledAction = action,
                () => recoveryCompletionSource.Task);

            scheduledAction();

            Assert.That(recoveryTask.IsCompleted, Is.False);
            Assert.That(UnityCliLoopServerController.RecoveryTask, Is.SameAs(recoveryTask));

            recoveryCompletionSource.SetResult(true);
            await recoveryTask;

            Assert.That(recoveryTask.IsCompleted, Is.True);
            Assert.That(UnityCliLoopServerController.RecoveryTask, Is.Null);
        }
    }
}
