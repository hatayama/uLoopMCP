using System;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Manual testing class for EditorDelay
    /// Real-time test execution from Unity Editor menu
    /// </summary>
    public static class EditorDelayManualTests
    {
        private static int testFrameStart;
        private static int testCounter = 0;
        
        [MenuItem("uLoopMCP/Debug/EditorDelay Tests/Basic Delay Tests")]
        public static void TestBasicDelays()
        {
            Debug.Log("========================================");
            Debug.Log("=== EditorDelay Basic Tests Started ===");
            Debug.Log("========================================");
            
            testFrameStart = Time.frameCount;
            testCounter = 0;
            
            TestZeroFrameDelay();
            TestSingleFrameDelay();
            TestMultipleFrameDelay();
        }
        
        private static async void TestZeroFrameDelay()
        {
            int currentFrame = Time.frameCount;
            Debug.Log($"[Test {++testCounter}] Zero Frame Delay - Start (Frame: {currentFrame})");
            
            await EditorDelay.DelayFrame(0);
            
            int completionFrame = Time.frameCount;
            Debug.Log($"[Test {testCounter}] Zero Frame Delay - Complete (Frame: {completionFrame}) - Immediate: {currentFrame == completionFrame}");
        }
        
        private static async void TestSingleFrameDelay()
        {
            int currentFrame = Time.frameCount;
            Debug.Log($"[Test {++testCounter}] Single Frame Delay - Start (Frame: {currentFrame})");
            
            await EditorDelay.DelayFrame(1);
            
            int completionFrame = Time.frameCount;
            int framesDiff = completionFrame - currentFrame;
            Debug.Log($"[Test {testCounter}] Single Frame Delay - Complete (Frame: {completionFrame}) - Frames elapsed: {framesDiff}");
        }
        
        private static async void TestMultipleFrameDelay()
        {
            const int delayFrames = 5;
            int currentFrame = Time.frameCount;
            Debug.Log($"[Test {++testCounter}] Multiple Frame Delay ({delayFrames}) - Start (Frame: {currentFrame})");
            
            await EditorDelay.DelayFrame(delayFrames);
            
            int completionFrame = Time.frameCount;
            int framesDiff = completionFrame - currentFrame;
            Debug.Log($"[Test {testCounter}] Multiple Frame Delay - Complete (Frame: {completionFrame}) - Frames elapsed: {framesDiff}");
        }
        
        [MenuItem("uLoopMCP/Debug/EditorDelay Tests/Concurrent Execution Test")]
        public static void TestConcurrentExecution()
        {
            Debug.Log("=========================================");
            Debug.Log("=== Concurrent Execution Test Started ===");
            Debug.Log("=========================================");
            
            testFrameStart = Time.frameCount;
            Debug.Log($"Test Start Frame: {testFrameStart}");
            
            // Execute tasks with different frame delays concurrently
            ConcurrentTask("A", 1);
            ConcurrentTask("B", 3);
            ConcurrentTask("C", 2);
            ConcurrentTask("D", 5);
            
            Debug.Log("Expected order: A(1) → C(2) → B(3) → D(5)");
        }
        
        private static async void ConcurrentTask(string taskName, int frames)
        {
            int startFrame = Time.frameCount;
            Debug.Log($"Task {taskName}: Start (Frame: {startFrame}, Delay: {frames} frames)");
            
            await EditorDelay.DelayFrame(frames);
            
            int endFrame = Time.frameCount;
            int elapsed = endFrame - testFrameStart;
            Debug.Log($"Task {taskName}: Complete (Frame: {endFrame}, Total elapsed: {elapsed} frames)");
        }
        
        [MenuItem("uLoopMCP/Debug/EditorDelay Tests/Stress Test (100 Tasks)")]
        public static void TestStressLoad()
        {
            Debug.Log("==============================");
            Debug.Log("=== Stress Test Started ===");
            Debug.Log("==============================");
            
            const int taskCount = 100;
            int completedCount = 0;
            
            Debug.Log($"Starting {taskCount} concurrent tasks...");
            Debug.Log($"Initial Pending Tasks: {EditorDelayManager.PendingTaskCount}");
            
            for (int i = 0; i < taskCount; i++)
            {
                int taskId = i;
                StressTask(taskId, () =>
                {
                    completedCount++;
                    if (completedCount == taskCount)
                    {
                        Debug.Log($"=== Stress Test Complete ===");
                        Debug.Log($"Completed Tasks: {completedCount}/{taskCount}");
                        Debug.Log($"Remaining Pending Tasks: {EditorDelayManager.PendingTaskCount}");
                    }
                });
            }
            
            Debug.Log($"All tasks started. Pending Tasks: {EditorDelayManager.PendingTaskCount}");
        }
        
        private static async void StressTask(int taskId, Action onComplete)
        {
            await EditorDelay.DelayFrame(2); // All tasks execute after 2 frames
            onComplete?.Invoke();
        }
        
        [MenuItem("uLoopMCP/Debug/EditorDelay Tests/Cancellation Test")]
        public static void TestCancellation()
        {
            Debug.Log("==============================");
            Debug.Log("=== Cancellation Test Started ===");
            Debug.Log("==============================");
            
            CancellationTokenSource cts = new CancellationTokenSource();
            
            TestCancellableTask(cts.Token);
            
            // Cancel after 1 second
            EditorApplication.delayCall += () =>
            {
                Debug.Log("Cancelling task...");
                cts.Cancel();
            };
        }
        
        private static async void TestCancellableTask(CancellationToken cancellationToken)
        {
            try
            {
                Debug.Log("Cancellable Task: Start (will be cancelled)");
                await EditorDelay.DelayFrame(10, cancellationToken);
                Debug.Log("Cancellable Task: Complete (should not reach here)");
            }
            catch (OperationCanceledException)
            {
                Debug.Log("Cancellable Task: Successfully cancelled!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Cancellable Task: Unexpected exception: {ex.Message}");
            }
        }
        
        [MenuItem("uLoopMCP/Debug/EditorDelay Tests/Integration Test")]
        public static void TestMcpServerControllerIntegration()
        {
            Debug.Log("==========================================");
            Debug.Log("=== McpServerController Integration Test ===");
            Debug.Log("==========================================");
            
            Debug.Log("Testing EditorDelay integration with McpServerController...");
            Debug.Log("This will simulate the actual usage in server restoration.");
            
            SimulateServerRestoration();
        }
        
        private static async void SimulateServerRestoration()
        {
            Debug.Log("Simulation: Starting server restoration sequence...");
            
            // Test with the same pattern as McpServerController
            await EditorDelay.DelayFrame(1);
            Debug.Log("Simulation: Phase 1 - Port release wait completed");
            
            await EditorDelay.DelayFrame(1);
            Debug.Log("Simulation: Phase 2 - Server startup completed");
            
            await EditorDelay.DelayFrame(1);
            Debug.Log("Simulation: Phase 3 - Notification sent");
            
            Debug.Log("Simulation: Server restoration sequence completed!");
        }
        
        [MenuItem("uLoopMCP/Debug/EditorDelay Tests/Show Manager Status")]
        public static void ShowDelayManagerStatus()
        {
            Debug.Log("==============================");
            Debug.Log("=== EditorDelayManager Status ===");
            Debug.Log("==============================");
            Debug.Log($"Pending Tasks: {EditorDelayManager.PendingTaskCount}");
            Debug.Log($"Current Frame: {Time.frameCount}");
            Debug.Log($"Time Since Startup: {EditorApplication.timeSinceStartup:F2}s");
        }
        
        [MenuItem("uLoopMCP/Debug/EditorDelay Tests/Clear All Tasks")]
        public static void ClearAllTasks()
        {
            int clearedCount = EditorDelayManager.PendingTaskCount;
            EditorDelayManager.ClearAllTasks();
            Debug.Log($"Cleared {clearedCount} pending tasks from EditorDelayManager");
        }
        
        [MenuItem("uLoopMCP/Debug/EditorDelay Tests/Show Test Instructions")]
        public static void ShowTestInstructions()
        {
            Debug.Log("=======================================");
            Debug.Log("=== EditorDelay Test Instructions ===");
            Debug.Log("=======================================");
            Debug.Log("1. Basic Delay Tests - Test zero, single, and multiple frame delays");
            Debug.Log("2. Concurrent Execution Test - Test parallel task execution order");
            Debug.Log("3. Stress Test - Test 100 concurrent tasks");
            Debug.Log("4. Cancellation Test - Test CancellationToken functionality");
            Debug.Log("5. Integration Test - Test McpServerController integration");
            Debug.Log("");
            Debug.Log("Status Commands:");
            Debug.Log("- Show Manager Status - Display current state");
            Debug.Log("- Clear All Tasks - Emergency cleanup");
            Debug.Log("");
            Debug.Log("Watch the Console for test results and frame timing!");
        }
    }
}