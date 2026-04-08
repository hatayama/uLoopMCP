using System.Threading.Tasks;
using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    [TestFixture]
    public class DynamicCodeServicesTests
    {
        [Test]
        [Timeout(5000)]
        public async Task AwaitDrainTaskAsync_WhenDrainTaskIsIncomplete_ShouldWaitForCompletion()
        {
            TaskCompletionSource<bool> drainTaskCompletionSource =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task awaitTask = DynamicCodeServices.AwaitDrainTaskAsync(drainTaskCompletionSource.Task);

            Assert.That(awaitTask.IsCompleted, Is.False);

            drainTaskCompletionSource.SetResult(true);
            await awaitTask;

            Assert.That(awaitTask.IsCompleted, Is.True);
            Assert.That(drainTaskCompletionSource.Task.IsCompleted, Is.True);
        }
    }
}
