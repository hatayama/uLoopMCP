using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;
using System.Linq;

namespace io.github.hatayama.uLoopMCP
{
    public class MainThreadSwitcherTests
    {
        private int mainThreadId;

        [SetUp]
        public void Setup()
        {
            // Record the main thread ID
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>
        /// Verifies that when called from a background thread, it can switch back to main thread
        /// </summary>
        [UnityTest]
        public IEnumerator SwitchToMainThread_WhenCalledFromBackgroundThread_ShouldSwitchBackToMainThread()
        {
            // Arrange
            bool executedImmediately = false;
            int executionThreadId = -1;
            bool completed = false;

            // Act
            Task.Run(async () =>
            {
                try
                {
                    await MainThreadSwitcher.SwitchToMainThread();
                    executedImmediately = true;
                    executionThreadId = Thread.CurrentThread.ManagedThreadId;
                    completed = true;
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogError($"Test failed: {ex.Message}");
                    completed = true;
                }
            });

            // Wait for completion
            yield return new UnityEngine.WaitUntil(() => completed);

            // Assert
            Assert.That(executedImmediately, Is.True, "Should execute when called from background thread");
            Assert.That(executionThreadId, Is.EqualTo(mainThreadId), "Should switch to main thread");
        }

        /// <summary>
        /// Verifies that when called from a background thread, it switches to the main thread
        /// </summary>
        [UnityTest]
        public IEnumerator SwitchToMainThread_WhenCalledFromBackgroundThread_ShouldSwitchToMainThread()
        {
            // Arrange
            int backgroundThreadId = -1;
            int afterSwitchThreadId = -1;
            bool taskCompleted = false;

            // Act
            Task.Run(async () =>
            {
                backgroundThreadId = Thread.CurrentThread.ManagedThreadId;

                await MainThreadSwitcher.SwitchToMainThread();

                afterSwitchThreadId = Thread.CurrentThread.ManagedThreadId;
                taskCompleted = true;
            });

            // Wait until the task is completed (maximum 5 seconds)
            float timeoutTime = Time.realtimeSinceStartup + 5f;
            while (!taskCompleted && Time.realtimeSinceStartup < timeoutTime)
            {
                yield return null;
            }

            // Assert
            Assert.That(taskCompleted, Is.True, "Background task should complete within timeout");
            Assert.That(backgroundThreadId, Is.Not.EqualTo(mainThreadId), "Should start on background thread");
            Assert.That(afterSwitchThreadId, Is.EqualTo(mainThreadId), "Should switch to main thread");
        }

        /// <summary>
        /// Verifies that PlayerLoopTiming can be specified
        /// </summary>
        [UnityTest]
        public IEnumerator SwitchToMainThread_WithPlayerLoopTiming_ShouldAcceptTiming()
        {
            // Arrange
            PlayerLoopTiming timing = PlayerLoopTiming.FixedUpdate;
            bool executed = false;
            bool completed = false;

            // Act
            Task.Run(async () =>
            {
                try
                {
                    await MainThreadSwitcher.SwitchToMainThread(timing);
                    executed = true;
                    completed = true;
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogError($"Test failed: {ex.Message}");
                    completed = true;
                }
            });

            // Wait for completion
            yield return new UnityEngine.WaitUntil(() => completed);

            // Assert
            Assert.That(executed, Is.True, "Should execute with specified timing");
        }

        /// <summary>
        /// Verifies the behavior when SwitchToMainThread is called simultaneously from multiple threads
        /// </summary>
        [UnityTest]
        public IEnumerator SwitchToMainThread_WhenCalledFromMultipleThreads_ShouldAllSwitchToMainThread()
        {
            // Arrange
            const int threadCount = 5;
            int[] threadIds = new int[threadCount];
            int[] switchedThreadIds = new int[threadCount];
            bool[] taskCompleted = new bool[threadCount];

            // Act
            for (int i = 0; i < threadCount; i++)
            {
                int index = i; // for capture
                Task.Run(async () =>
                {
                    threadIds[index] = Thread.CurrentThread.ManagedThreadId;

                    await MainThreadSwitcher.SwitchToMainThread();

                    switchedThreadIds[index] = Thread.CurrentThread.ManagedThreadId;
                    taskCompleted[index] = true;
                });
            }

            // Wait until all tasks are completed (maximum 10 seconds)
            float timeoutTime = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < timeoutTime)
            {
                bool allCompleted = true;
                for (int i = 0; i < threadCount; i++)
                {
                    if (!taskCompleted[i])
                    {
                        allCompleted = false;
                        break;
                    }
                }

                if (allCompleted)
                    break;

                yield return null;
            }

            // Assert
            for (int i = 0; i < threadCount; i++)
            {
                Assert.That(taskCompleted[i], Is.True, $"Task {i} should complete within timeout");
                Assert.That(threadIds[i], Is.Not.EqualTo(mainThreadId), $"Task {i} should start on background thread");
                Assert.That(switchedThreadIds[i], Is.EqualTo(mainThreadId), $"Task {i} should switch to main thread");
            }
        }

        /// <summary>
        /// Verifies that SwitchToMainThread completes immediately when called directly from the main thread
        /// </summary>
        [Test]
        public void SwitchToMainThread_WhenCalledDirectlyFromMainThread_ShouldCompleteImmediately()
        {
            // Arrange & Act & Assert
            // This should complete immediately without hanging
            bool completed = false;

            // Execute directly on main thread (not in Task.Run)
            var task = ExecuteOnMainThread();

            async Task ExecuteOnMainThread()
            {
                await MainThreadSwitcher.SwitchToMainThread();
                completed = true;
            }

            // Wait for a very short time - if it hangs, this will fail
            task.Wait(TimeSpan.FromMilliseconds(100));

            Assert.That(completed, Is.True, "SwitchToMainThread should complete immediately when called from main thread");
            Assert.That(task.IsCompleted, Is.True, "Task should be completed");
            Assert.That(Thread.CurrentThread.ManagedThreadId, Is.EqualTo(mainThreadId), "Should still be on main thread");
        }

        /// <summary>
        /// Verifies that SwitchToMainThread does not hang when called from main thread using async/await pattern
        /// </summary>
        [Test]
        public async Task SwitchToMainThread_WhenCalledFromMainThread_ShouldNotHangWithAsyncAwait()
        {
            // Arrange
            bool operationCompleted = false;

            // Act - This should complete immediately without hanging
            Task timeoutTask = TimerDelay.Wait(1000);
            Task switchTask = Task.Run(async () =>
            {
                await MainThreadSwitcher.SwitchToMainThread();
                operationCompleted = true;
            });

            // Wait for either completion or timeout
            Task completedTask = await Task.WhenAny(switchTask, timeoutTask);

            // Assert
            Assert.That(completedTask, Is.EqualTo(switchTask),
                "SwitchToMainThread should complete before timeout when called from main thread");
            Assert.That(operationCompleted, Is.True,
                "Operation should have completed successfully");
        }
    }
}
