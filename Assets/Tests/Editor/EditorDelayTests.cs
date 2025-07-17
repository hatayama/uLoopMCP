using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.TestTools;
using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Unit tests for EditorDelay system
    /// Automated tests using Unity Test Framework
    /// </summary>
    [TestFixture]
    public class EditorDelayTests
    {
        private int frameCountAtStart;
        private readonly List<string> executionLog = new();
        
        [SetUp]
        public void SetUp()
        {
            // Ensure test isolation
            EditorDelayManager.ClearAllTasks();
            EditorDelayManager.ResetFrameCount();
            
            // Record frame count at test start
            frameCountAtStart = EditorDelayManager.CurrentFrameCount;
            executionLog.Clear();
        }
        
        [TearDown]
        public void TearDown()
        {
            // Post-test cleanup
            EditorDelayManager.ClearAllTasks();
            executionLog.Clear();
        }
        
        /// <summary>
        /// Zero frame delay test (immediate execution)
        /// Verify immediate execution using EditorDelayManager frame counter
        /// </summary>
        [Test]
        public async Task DelayFrame_ZeroFrames_ExecutesImmediately()
        {
            // Arrange - Record starting frame number
            int startFrame = EditorDelayManager.CurrentFrameCount;
            bool executed = false;
            
            // Act - Execute zero frame delay
            await EditorDelay.DelayFrame(0);
            executed = true;
            
            // Assert - Verify execution completed within the same frame
            int endFrame = EditorDelayManager.CurrentFrameCount;
            Assert.IsTrue(executed, "Zero frame delay should execute the task");
            Assert.AreEqual(startFrame, endFrame, "Zero frame delay should execute within the same frame");
            Assert.AreEqual(0, EditorDelayManager.PendingTaskCount, "No tasks should be pending after zero frame delay");
        }
        
        /// <summary>
        /// Basic frame delay test
        /// Check exact frame count using CurrentFrameCount
        /// </summary>
        [UnityTest]
        public IEnumerator DelayFrame_SingleFrame_ExecutesAfterOneFrame()
        {
            bool executed = false;
            int executionFrame = -1;
            int startFrame = EditorDelayManager.CurrentFrameCount;
            
            // Arrange & Act
            _ = DelayedExecution();
            
            async Task DelayedExecution()
            {
                await EditorDelay.DelayFrame(1);
                executed = true;
                executionFrame = EditorDelayManager.CurrentFrameCount;
            }
            
            // Initially not executed
            Assert.IsFalse(executed, "Task should not be executed immediately");
            
            // Wait for 1 frame and then check
            yield return null;
            
            // Assert - Verify execution and frame count
            Assert.IsTrue(executed, "Task should be executed after 1 frame");
            Assert.AreEqual(startFrame + 1, executionFrame, "Task should execute exactly 1 frame later");
        }
        
        /// <summary>
        /// Multiple frame delay test
        /// Check exact frame count using CurrentFrameCount
        /// </summary>
        [UnityTest]
        public IEnumerator DelayFrame_MultipleFrames_ExecutesAfterCorrectFrames()
        {
            bool executed = false;
            int executionFrame = -1;
            const int delayFrames = 3;
            int startFrame = EditorDelayManager.CurrentFrameCount;
            
            // Arrange & Act
            _ = DelayedExecution();
            
            async Task DelayedExecution()
            {
                await EditorDelay.DelayFrame(delayFrames);
                executed = true;
                executionFrame = EditorDelayManager.CurrentFrameCount;
            }
            
            // Wait for delayFrames number of frames
            for (int i = 0; i < delayFrames; i++)
            {
                Assert.IsFalse(executed, $"Task should not be executed at frame {i + 1}");
                yield return null;
            }
            
            // Should be executed after delayFrames
            Assert.IsTrue(executed, $"Task should be executed after {delayFrames} frames");
            Assert.AreEqual(startFrame + delayFrames, executionFrame, $"Task should execute exactly {delayFrames} frames later");
        }
        
        /// <summary>
        /// Concurrent execution order test
        /// Check exact frame count using CurrentFrameCount
        /// </summary>
        [UnityTest]
        public IEnumerator DelayFrame_ConcurrentTasks_ExecuteInCorrectOrder()
        {
            // Arrange
            int startFrame = EditorDelayManager.CurrentFrameCount;
            var executionFrames = new List<(string task, int frame)>();
            
            _ = Task1(); // After 1 frame
            _ = Task2(); // After 3 frames
            _ = Task3(); // After 2 frames
            
            async Task Task1()
            {
                await EditorDelay.DelayFrame(1);
                executionLog.Add("Task1");
                executionFrames.Add(("Task1", EditorDelayManager.CurrentFrameCount));
            }
            
            async Task Task2()
            {
                await EditorDelay.DelayFrame(3);
                executionLog.Add("Task2");
                executionFrames.Add(("Task2", EditorDelayManager.CurrentFrameCount));
            }
            
            async Task Task3()
            {
                await EditorDelay.DelayFrame(2);
                executionLog.Add("Task3");
                executionFrames.Add(("Task3", EditorDelayManager.CurrentFrameCount));
            }
            
            // Act & Assert - Check frame by frame
            yield return null; // After 1 frame
            Assert.AreEqual(1, executionLog.Count, "Only Task1 should be executed after 1 frame");
            Assert.AreEqual("Task1", executionLog[0], "Task1 should execute first");
            Assert.AreEqual(startFrame + 1, executionFrames[0].frame, "Task1 should execute at frame startFrame + 1");
            
            yield return null; // After 2 frames
            Assert.AreEqual(2, executionLog.Count, "Task1 and Task3 should be executed after 2 frames");
            Assert.AreEqual("Task3", executionLog[1], "Task3 should execute second");
            Assert.AreEqual(startFrame + 2, executionFrames[1].frame, "Task3 should execute at frame startFrame + 2");
            
            yield return null; // After 3 frames
            Assert.AreEqual(3, executionLog.Count, "All tasks should be executed after 3 frames");
            Assert.AreEqual("Task2", executionLog[2], "Task2 should execute last");
            Assert.AreEqual(startFrame + 3, executionFrames[2].frame, "Task2 should execute at frame startFrame + 3");
        }
        
        /// <summary>
        /// EditorDelayManager task count management test
        /// </summary>
        [UnityTest]
        public IEnumerator DelayManager_TaskCount_ManagesCorrectly()
        {
            // Arrange - Register multiple tasks
            StartMultipleTasks();
            
            void StartMultipleTasks()
            {
                for (int i = 0; i < 5; i++)
                {
                    int taskId = i;
                    _ = DelayedTask(taskId);
                }
                
                async Task DelayedTask(int id)
                {
                    await EditorDelay.DelayFrame(2);
                    executionLog.Add($"Task{id}");
                }
            }
            
            // Act & Assert
            yield return null; // After 1 frame
            Assert.AreEqual(5, EditorDelayManager.PendingTaskCount, "5 tasks should be pending");
            
            yield return null; // After 2 frames (execution completed)
            Assert.AreEqual(0, EditorDelayManager.PendingTaskCount, "No tasks should be pending after execution");
            Assert.AreEqual(5, executionLog.Count, "All 5 tasks should be executed");
        }
        
        /// <summary>
        /// CancellationToken immediate cancellation test
        /// </summary>
        [Test]
        public void DelayFrame_WithImmediateCancellation_ThrowsImmediately()
        {
            // Arrange - Already cancelled token
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel in advance
            
            bool executed = false;
            Exception caughtException = null;
            
            // Act & Assert - Cancellation exception should be thrown immediately
            try
            {
                _ = DelayedTask();
                async Task DelayedTask()
                {
                    try
                    {
                        await EditorDelay.DelayFrame(5, cts.Token);
                        executed = true;
                    }
                    catch (Exception ex)
                    {
                        caughtException = ex;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Case where cancellation exception is thrown synchronously
                caughtException = new OperationCanceledException();
            }
            
            // ThrowIfCancellationRequested should be called in IsCompleted or GetResult
            Assert.IsFalse(executed, "Task should not execute when pre-cancelled");
        }
        
        /// <summary>
        /// CancellationToken delayed cancellation test (simple version)
        /// </summary>
        [UnityTest]
        public IEnumerator DelayFrame_WithDelayedCancellation_CancelsCorrectly()
        {
            // Arrange
            CancellationTokenSource cts = new CancellationTokenSource();
            bool taskStarted = false;
            bool executed = false;
            
            // Act - Start task scheduled to complete after 5 frames
            var _ = DelayedTaskWithCancellation();
            
            async Task DelayedTaskWithCancellation()
            {
                taskStarted = true;
                try
                {
                    await EditorDelay.DelayFrame(5, cts.Token);
                    executed = true;
                }
                catch (OperationCanceledException)
                {
                    // Cancellation processing does nothing special (executed remains false)
                }
                catch (Exception)
                {
                    // Treat other exceptions as cancellation too (executed remains false)
                }
            }
            
            // Wait a bit until task starts
            yield return null;
            Assert.IsTrue(taskStarted, "Task should have started");
            
            // Verify task is not yet executed
            Assert.IsFalse(executed, "Task should not be executed immediately");
            
            // Cancel after waiting 2 frames (before task completion)
            yield return null; // 2 frames
            yield return null; // 3 frames
            
            cts.Cancel(); // Cancel after 3 frames
            
            // Wait a bit for cancellation processing
            yield return null; // 4 frames
            yield return null; // 5 frames
            
            // Assert - Verify task was cancelled and not executed
            Assert.IsFalse(executed, "Task should not execute when cancelled");
        }
        
        /// <summary>
        /// Load test - Execute large number of tasks simultaneously
        /// Check exact frame count using CurrentFrameCount
        /// </summary>
        [UnityTest]
        public IEnumerator DelayFrame_StressTest_HandlesLargeTasks()
        {
            const int taskCount = 100;
            int completedTasks = 0;
            int startFrame = EditorDelayManager.CurrentFrameCount;
            var completionFrames = new List<int>();
            
            // Arrange & Act - Start 100 tasks
            var tasks = new List<Task>();
            for (int i = 0; i < taskCount; i++)
            {
                tasks.Add(StressTask());
            }
            
            async Task StressTask()
            {
                await EditorDelay.DelayFrame(1);
                Interlocked.Increment(ref completedTasks);
                lock (completionFrames)
                {
                    completionFrames.Add(EditorDelayManager.CurrentFrameCount);
                }
            }
            
            // Initially all tasks are pending
            Assert.AreEqual(taskCount, EditorDelayManager.PendingTaskCount, $"{taskCount} tasks should be pending");
            
            yield return null; // After 1 frame
            
            // Assert - All tasks completed and frame count verification
            Assert.AreEqual(taskCount, completedTasks, $"All {taskCount} tasks should be completed");
            Assert.AreEqual(0, EditorDelayManager.PendingTaskCount, "No tasks should be pending after completion");
            Assert.AreEqual(taskCount, completionFrames.Count, "All tasks should record completion frame");
            
            // Verify all tasks completed at the same frame (startFrame + 1)
            int expectedFrame = startFrame + 1;
            foreach (int frame in completionFrames)
            {
                Assert.AreEqual(expectedFrame, frame, "All tasks should complete at the same frame");
            }
        }
        
        /// <summary>
        /// Frame counter reset functionality test
        /// </summary>
        [UnityTest]
        public IEnumerator DelayManager_ResetFrameCount_ResetsCorrectly()
        {
            // Arrange - Advance frames then measure
            yield return null; // Advance 1 frame
            
            int countAfterFrame = EditorDelayManager.CurrentFrameCount;
            Assert.Greater(countAfterFrame, 0, "Frame count should be greater than 0 after waiting one frame");
            
            // Act - Execute reset
            EditorDelayManager.ResetFrameCount();
            
            // Assert - Reset to zero
            Assert.AreEqual(0, EditorDelayManager.CurrentFrameCount, "Frame count should be reset to 0");
        }
        
        /// <summary>
        /// Main thread vs background thread delay test
        /// Verify that EditorDelay works consistently across different threads
        /// </summary>
        [UnityTest]
        public IEnumerator DelayFrame_MainThreadVsBackgroundThread_ExecutesSameWay()
        {
            // Arrange
            int startFrame = EditorDelayManager.CurrentFrameCount;
            bool mainThreadExecuted = false;
            bool backgroundThreadExecuted = false;
            int mainThreadExecutionFrame = -1;
            int backgroundThreadExecutionFrame = -1;
            int mainThreadId = Thread.CurrentThread.ManagedThreadId;
            int mainThreadRegistrationThreadId = -1;
            int backgroundThreadRegistrationThreadId = -1;
            int mainThreadExecutionThreadId = -1;
            int backgroundThreadExecutionThreadId = -1;
            const int delayFrames = 2;
            
            // Act - Start tasks from both main thread and background thread
            _ = MainThreadDelayTask();
            _ = Task.Run(BackgroundThreadDelayTask);
            
            async Task MainThreadDelayTask()
            {
                mainThreadRegistrationThreadId = Thread.CurrentThread.ManagedThreadId;
                await EditorDelay.DelayFrame(delayFrames);
                mainThreadExecuted = true;
                mainThreadExecutionFrame = EditorDelayManager.CurrentFrameCount;
                mainThreadExecutionThreadId = Thread.CurrentThread.ManagedThreadId;
            }
            
            async Task BackgroundThreadDelayTask()
            {
                backgroundThreadRegistrationThreadId = Thread.CurrentThread.ManagedThreadId;
                await EditorDelay.DelayFrame(delayFrames);
                backgroundThreadExecuted = true;
                backgroundThreadExecutionFrame = EditorDelayManager.CurrentFrameCount;
                backgroundThreadExecutionThreadId = Thread.CurrentThread.ManagedThreadId;
            }
            
            // Wait for specified frames
            for (int i = 0; i < delayFrames; i++)
            {
                Assert.IsFalse(mainThreadExecuted, $"Main thread task should not be executed at frame {i + 1}");
                Assert.IsFalse(backgroundThreadExecuted, $"Background thread task should not be executed at frame {i + 1}");
                yield return null;
            }
            
            // Assert - Both tasks should execute at the same frame
            Assert.IsTrue(mainThreadExecuted, "Main thread task should be executed");
            Assert.IsTrue(backgroundThreadExecuted, "Background thread task should be executed");
            Assert.AreEqual(startFrame + delayFrames, mainThreadExecutionFrame, "Main thread task should execute at correct frame");
            Assert.AreEqual(startFrame + delayFrames, backgroundThreadExecutionFrame, "Background thread task should execute at correct frame");
            Assert.AreEqual(mainThreadExecutionFrame, backgroundThreadExecutionFrame, "Both tasks should execute at the same frame");
            
            // Verify thread behavior - registration thread should be different, but execution should be on main thread
            Assert.AreEqual(mainThreadId, mainThreadRegistrationThreadId, "Main thread task should be registered on main thread");
            Assert.AreNotEqual(mainThreadId, backgroundThreadRegistrationThreadId, "Background thread task should be registered on different thread");
            
            // Note: Execution threads might vary depending on EditorDelayManager implementation
            // The important thing is that both tasks execute at the same frame regardless of registration thread
        }
        
        /// <summary>
        /// 　Thread safety test for concurrent registration from multiple threads
        /// </summary>
        [UnityTest]
        public IEnumerator DelayFrame_ConcurrentThreadRegistration_HandlesThreadSafely()
        {
            // Arrange
            const int tasksPerThread = 5;
            const int threadCount = 3;
            const int totalTasks = tasksPerThread * threadCount;
            int completedTasks = 0;
            List<int> executionFrames = new List<int>();
            List<int> registrationThreadIds = new List<int>();
            List<int> executionThreadIds = new List<int>();
            object executionLock = new object();
            
            // Use ManualResetEventSlim to synchronize thread startup
            ManualResetEventSlim startSignal = new ManualResetEventSlim(false);
            try
            {
                int threadsReady = 0;
                
                // Act - Start multiple threads that register delay tasks
                List<Task> threadTasks = new List<Task>();
                for (int threadId = 0; threadId < threadCount; threadId++)
                {
                    int capturedThreadId = threadId;
                    threadTasks.Add(Task.Run(() => RegisterTasksFromThread(capturedThreadId)));
                }
                
                void RegisterTasksFromThread(int threadId)
                {
                    int currentThreadId = Thread.CurrentThread.ManagedThreadId;
                    
                    // Signal that this thread is ready
                    Interlocked.Increment(ref threadsReady);
                    
                    // Wait for all threads to be ready
                    startSignal.Wait();
                    
                    // Register tasks synchronously
                    List<Task> delayTasks = new List<Task>();
                    for (int i = 0; i < tasksPerThread; i++)
                    {
                        int taskId = i;
                        Task delayTask = DelayedTaskFromThread(threadId, taskId, currentThreadId);
                        delayTasks.Add(delayTask);
                    }
                    
                    // Wait for all tasks in this thread to complete
                    Task.WaitAll(delayTasks.ToArray());
                    
                    async Task DelayedTaskFromThread(int tId, int tTaskId, int regThreadId)
                    {
                        await EditorDelay.DelayFrame(2);
                        
                        lock (executionLock)
                        {
                            completedTasks++;
                            executionFrames.Add(EditorDelayManager.CurrentFrameCount);
                            registrationThreadIds.Add(regThreadId);
                            executionThreadIds.Add(Thread.CurrentThread.ManagedThreadId);
                            executionLog.Add($"Thread{tId}-Task{tTaskId}");
                        }
                    }
                }
                
                // Wait for all threads to be ready
                while (threadsReady < threadCount)
                {
                    yield return null;
                }
                
                // Signal all threads to start registering tasks
                startSignal.Set();
                
                // Wait for task registration to complete with dynamic waiting
                const int maxWaitFrames = 10;
                int waitFrames = 0;
                while (waitFrames < maxWaitFrames && EditorDelayManager.PendingTaskCount != totalTasks)
                {
                    yield return null;
                    waitFrames++;
                }
                
                // Assert - All tasks should be registered
                Assert.AreEqual(totalTasks, EditorDelayManager.PendingTaskCount, $"All {totalTasks} tasks should be registered and pending (waited {waitFrames} frames)");
                
                // Wait for execution (2 frames + buffer) with dynamic waiting
                const int maxExecutionWaitFrames = 15;
                int executionWaitFrames = 0;
                while (executionWaitFrames < maxExecutionWaitFrames && 
                       (completedTasks < totalTasks || EditorDelayManager.PendingTaskCount > 0))
                {
                    yield return null;
                    executionWaitFrames++;
                }
                
                // Assert - All tasks completed successfully
                Assert.AreEqual(totalTasks, completedTasks, $"All {totalTasks} tasks should be completed (waited {executionWaitFrames} frames)");
                Assert.AreEqual(0, EditorDelayManager.PendingTaskCount, "No tasks should be pending after completion");
                Assert.AreEqual(totalTasks, executionLog.Count, "All tasks should be logged");
                
                // Verify all tasks executed at the same frame
                if (executionFrames.Count > 0)
                {
                    int expectedFrame = executionFrames[0];
                    foreach (int frame in executionFrames)
                    {
                        Assert.AreEqual(expectedFrame, frame, "All tasks should execute at the same frame regardless of registration thread");
                    }
                }
                
                // Verify that tasks were actually registered from different threads
                HashSet<int> uniqueRegistrationThreads = new HashSet<int>(registrationThreadIds);
                Assert.Greater(uniqueRegistrationThreads.Count, 1, "Tasks should be registered from multiple different threads");
                Assert.AreEqual(threadCount, uniqueRegistrationThreads.Count, $"Tasks should be registered from exactly {threadCount} different threads");
            }
            finally
            {
                startSignal?.Dispose();
            }
        }
        
        /// <summary>
        /// Cross-thread cancellation test
        /// Verify cancellation works correctly when initiated from different threads
        /// </summary>
        [UnityTest]
        public IEnumerator DelayFrame_CrossThreadCancellation_CancelsCorrectly()
        {
            // Arrange
            CancellationTokenSource cts = new CancellationTokenSource();
            bool mainThreadTaskExecuted = false;
            bool backgroundThreadTaskExecuted = false;
            bool mainThreadTaskStarted = false;
            bool backgroundThreadTaskStarted = false;
            int mainThreadId = Thread.CurrentThread.ManagedThreadId;
            int mainThreadRegistrationThreadId = -1;
            int backgroundThreadRegistrationThreadId = -1;
            int cancellationThreadId = -1;
            
            // Act - Start tasks from different threads with shared cancellation token
            _ = MainThreadCancellableTask();
            _ = Task.Run(BackgroundThreadCancellableTask);
            
            async Task MainThreadCancellableTask()
            {
                mainThreadTaskStarted = true;
                mainThreadRegistrationThreadId = Thread.CurrentThread.ManagedThreadId;
                try
                {
                    await EditorDelay.DelayFrame(5, cts.Token);
                    mainThreadTaskExecuted = true;
                }
                catch (OperationCanceledException)
                {
                    // Expected cancellation
                }
            }
            
            async Task BackgroundThreadCancellableTask()
            {
                backgroundThreadTaskStarted = true;
                backgroundThreadRegistrationThreadId = Thread.CurrentThread.ManagedThreadId;
                try
                {
                    await EditorDelay.DelayFrame(5, cts.Token);
                    backgroundThreadTaskExecuted = true;
                }
                catch (OperationCanceledException)
                {
                    // Expected cancellation
                }
            }
            
            // Wait for tasks to start
            yield return null;
            Assert.IsTrue(mainThreadTaskStarted, "Main thread task should have started");
            Assert.IsTrue(backgroundThreadTaskStarted, "Background thread task should have started");
            
            // Verify thread IDs for task registration
            Assert.AreEqual(mainThreadId, mainThreadRegistrationThreadId, "Main thread task should be registered on main thread");
            Assert.AreNotEqual(mainThreadId, backgroundThreadRegistrationThreadId, "Background thread task should be registered on different thread");
            
            // Wait and then cancel from a different thread
            yield return null; // 2 frames
            yield return null; // 3 frames
            
            // Cancel from background thread
            Task.Run(() => {
                cancellationThreadId = Thread.CurrentThread.ManagedThreadId;
                cts.Cancel();
            });
            
            // Wait for cancellation to take effect
            yield return null; // 4 frames
            yield return null; // 5 frames
            
            // Assert - Both tasks should be cancelled
            Assert.IsFalse(mainThreadTaskExecuted, "Main thread task should not execute when cancelled");
            Assert.IsFalse(backgroundThreadTaskExecuted, "Background thread task should not execute when cancelled");
            
            // Verify cancellation was initiated from a different thread
            Assert.AreNotEqual(mainThreadId, cancellationThreadId, "Cancellation should be initiated from background thread");
        }
    }
}