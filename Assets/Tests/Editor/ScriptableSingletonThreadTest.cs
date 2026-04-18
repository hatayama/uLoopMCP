using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    public class ScriptableSingletonThreadTest
    {
        private TestSessionManager testManager;
        private static readonly int MainThreadId = Thread.CurrentThread.ManagedThreadId;

        [SetUp]
        public void SetUp()
        {
            testManager = TestSessionManager.instance;
            testManager.ResetAllValues();
        }

        [Test]
        public void MainThread_CanAccessScriptableSingleton()
        {
            testManager.TestBoolValue = true;
            testManager.TestIntValue = 42;
            testManager.TestStringValue = "main thread test";
            testManager.TestFloatValue = 3.14f;

            Assert.AreEqual(true, testManager.TestBoolValue);
            Assert.AreEqual(42, testManager.TestIntValue);
            Assert.AreEqual("main thread test", testManager.TestStringValue);
            Assert.AreEqual(3.14f, testManager.TestFloatValue, 0.001f);
        }

        [Test]
        public async Task BackgroundThread_CanReadFromScriptableSingleton()
        {
            testManager.TestBoolValue = true;
            testManager.TestIntValue = 123;
            testManager.TestStringValue = "background read test";
            testManager.TestFloatValue = 2.71f;

            bool backgroundReadSuccess = false;
            bool backgroundBoolValue = false;
            int backgroundIntValue = 0;
            string backgroundStringValue = "";
            float backgroundFloatValue = 0f;
            Exception backgroundException = null;

            await Task.Run(() =>
            {
                Thread.CurrentThread.Name = "BackgroundTestThread";
                Debug.Log($"Background thread ID: {Thread.CurrentThread.ManagedThreadId}, Name: {Thread.CurrentThread.Name}");
                Debug.Log($"Unity main thread ID: {MainThreadId}");
                
                backgroundBoolValue = testManager.TestBoolValue;
                backgroundIntValue = testManager.TestIntValue;
                backgroundStringValue = testManager.TestStringValue;
                backgroundFloatValue = testManager.TestFloatValue;
                backgroundReadSuccess = true;
                Debug.Log($"Background thread read: {testManager.GetAllValuesAsString()}");
            });

            Assert.IsNull(backgroundException, $"Background thread threw exception: {backgroundException?.Message}");
            Assert.IsTrue(backgroundReadSuccess, "Background thread failed to read values");
            Assert.AreEqual(true, backgroundBoolValue, "Bool value mismatch");
            Assert.AreEqual(123, backgroundIntValue, "Int value mismatch");
            Assert.AreEqual("background read test", backgroundStringValue, "String value mismatch");
            Assert.AreEqual(2.71f, backgroundFloatValue, 0.001f, "Float value mismatch");
        }

        [Test]
        public async Task BackgroundThread_CanWriteToScriptableSingleton()
        {
            Exception backgroundException = null;
            bool backgroundWriteSuccess = false;

            await Task.Run(() =>
            {
                Thread.CurrentThread.Name = "BackgroundWriteTestThread";
                Debug.Log($"Background write thread ID: {Thread.CurrentThread.ManagedThreadId}");
                
                testManager.TestBoolValue = false;
                testManager.TestIntValue = 999;
                testManager.TestStringValue = "background write test";
                testManager.TestFloatValue = 1.23f;
                backgroundWriteSuccess = true;
                Debug.Log($"Background thread wrote: {testManager.GetAllValuesAsString()}");
            });

            await TimerDelay.Wait(100);

            Assert.IsNull(backgroundException, $"Background thread threw exception: {backgroundException?.Message}");
            Assert.IsTrue(backgroundWriteSuccess, "Background thread failed to write values");
            Assert.AreEqual(false, testManager.TestBoolValue, "Bool value not updated");
            Assert.AreEqual(999, testManager.TestIntValue, "Int value not updated");
            Assert.AreEqual("background write test", testManager.TestStringValue, "String value not updated");
            Assert.AreEqual(1.23f, testManager.TestFloatValue, 0.001f, "Float value not updated");
        }

        [Test]
        public async Task MultipleBackgroundThreads_CanAccessScriptableSingleton()
        {
            int threadCount = 5;
            Task[] tasks = new Task[threadCount];
            bool[] results = new bool[threadCount];
            Exception[] exceptions = new Exception[threadCount];

            for (int i = 0; i < threadCount; i++)
            {
                int threadIndex = i;
                tasks[i] = Task.Run(() =>
                {
                    Thread.CurrentThread.Name = $"MultiTestThread{threadIndex}";
                    Debug.Log($"Multi thread {threadIndex} ID: {Thread.CurrentThread.ManagedThreadId}");
                    
                    testManager.TestIntValue = threadIndex * 10;
                    testManager.TestStringValue = $"thread-{threadIndex}";
                    
                    int readValue = testManager.TestIntValue;
                    string readString = testManager.TestStringValue;
                    
                    results[threadIndex] = true;
                    Debug.Log($"Thread {threadIndex} read: Int={readValue}, String='{readString}'");
                });
            }

            await Task.WhenAll(tasks);

            for (int i = 0; i < threadCount; i++)
            {
                Assert.IsNull(exceptions[i], $"Thread {i} threw exception: {exceptions[i]?.Message}");
                Assert.IsTrue(results[i], $"Thread {i} failed to complete");
            }

            Debug.Log($"Final values: {testManager.GetAllValuesAsString()}");
        }

        [TearDown]
        public void TearDown()
        {
            testManager?.ResetAllValues();
        }
    }
}
