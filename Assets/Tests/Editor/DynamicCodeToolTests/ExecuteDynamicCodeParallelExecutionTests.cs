#if ULOOPMCP_HAS_ROSLYN
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    public class ExecuteDynamicCodeParallelExecutionTests
    {
        private IDynamicCodeExecutor _executor;

        [SetUp]
        public void SetUp()
        {
            _executor = Factory.DynamicCodeExecutorFactory.Create(
                DynamicCodeSecurityLevel.Restricted
            );
        }

        [Test]
        public async Task ExclusiveMode_SecondCallWaitsForFirst_Async()
        {
            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();

            string blockingCode = @"
var tcs = (System.Threading.Tasks.TaskCompletionSource<object>)parameters[""param0""];
await tcs.Task;
return ""first"";
";

            string quickCode = @"return ""second"";";

            Task<ExecutionResult> firstTask = _executor.ExecuteCodeAsync(
                blockingCode,
                "DynamicCommand",
                new object[] { tcs },
                CancellationToken.None,
                compileOnly: false,
                allowParallel: false
            );

            await Task.Delay(100);

            Task<ExecutionResult> secondTask = _executor.ExecuteCodeAsync(
                quickCode,
                "DynamicCommand",
                null,
                CancellationToken.None,
                compileOnly: false,
                allowParallel: false
            );

            await Task.Delay(100);

            Assert.IsFalse(firstTask.IsCompleted, "First task should still be running");
            Assert.IsFalse(secondTask.IsCompleted, "Second task should be waiting (exclusive mode)");

            tcs.SetResult(null);

            ExecutionResult firstResult = await firstTask;
            ExecutionResult secondResult = await secondTask;

            Assert.IsTrue(firstResult.Success, firstResult.ErrorMessage);
            Assert.AreEqual("first", firstResult.Result);
            Assert.IsTrue(secondResult.Success, secondResult.ErrorMessage);
            Assert.AreEqual("second", secondResult.Result);
        }

        [Test]
        public async Task ParallelMode_BothTasksRunConcurrently()
        {
            TaskCompletionSource<object> tcs1 = new TaskCompletionSource<object>();
            TaskCompletionSource<object> tcs2 = new TaskCompletionSource<object>();

            string code1 = @"
var tcs = (System.Threading.Tasks.TaskCompletionSource<object>)parameters[""param0""];
await tcs.Task;
return ""task1"";
";

            string code2 = @"
var tcs = (System.Threading.Tasks.TaskCompletionSource<object>)parameters[""param0""];
await tcs.Task;
return ""task2"";
";

            Task<ExecutionResult> task1 = _executor.ExecuteCodeAsync(
                code1,
                "DynamicCommand",
                new object[] { tcs1 },
                CancellationToken.None,
                compileOnly: false,
                allowParallel: true
            );

            Task<ExecutionResult> task2 = _executor.ExecuteCodeAsync(
                code2,
                "DynamicCommand",
                new object[] { tcs2 },
                CancellationToken.None,
                compileOnly: false,
                allowParallel: true
            );

            await Task.Delay(200);

            Assert.IsFalse(task1.IsCompleted, "Task1 should still be waiting");
            Assert.IsFalse(task2.IsCompleted, "Task2 should still be waiting (parallel mode allows both)");

            tcs2.SetResult(null);
            ExecutionResult result2 = await task2;

            Assert.IsFalse(task1.IsCompleted, "Task1 should still be waiting after task2 completes");
            Assert.IsTrue(result2.Success, result2.ErrorMessage);
            Assert.AreEqual("task2", result2.Result);

            tcs1.SetResult(null);
            ExecutionResult result1 = await task1;

            Assert.IsTrue(result1.Success, result1.ErrorMessage);
            Assert.AreEqual("task1", result1.Result);
        }


        [Test]
        public async Task ParallelMode_MixedWithExclusive_WorksCorrectly()
        {
            string quickCode = @"return ""done"";";

            ExecutionResult result1 = await _executor.ExecuteCodeAsync(
                quickCode,
                "DynamicCommand",
                null,
                CancellationToken.None,
                compileOnly: false,
                allowParallel: true
            );

            ExecutionResult result2 = await _executor.ExecuteCodeAsync(
                quickCode,
                "DynamicCommand",
                null,
                CancellationToken.None,
                compileOnly: false,
                allowParallel: false
            );

            ExecutionResult result3 = await _executor.ExecuteCodeAsync(
                quickCode,
                "DynamicCommand",
                null,
                CancellationToken.None,
                compileOnly: false,
                allowParallel: true
            );

            Assert.IsTrue(result1.Success, result1.ErrorMessage);
            Assert.IsTrue(result2.Success, result2.ErrorMessage);
            Assert.IsTrue(result3.Success, result3.ErrorMessage);
        }

        [Test]
        public async Task ParallelMode_MultipleSimultaneous_AllComplete()
        {
            const int taskCount = 5;
            List<Task<ExecutionResult>> tasks = new List<Task<ExecutionResult>>();

            for (int i = 0; i < taskCount; i++)
            {
                int index = i;
                string code = $@"return ""result_{index}"";";

                Task<ExecutionResult> task = _executor.ExecuteCodeAsync(
                    code,
                    "DynamicCommand",
                    null,
                    CancellationToken.None,
                    compileOnly: false,
                    allowParallel: true
                );
                tasks.Add(task);
            }

            ExecutionResult[] results = await Task.WhenAll(tasks);

            for (int i = 0; i < taskCount; i++)
            {
                Assert.IsTrue(results[i].Success, $"Task {i} failed: {results[i].ErrorMessage}");
                Assert.AreEqual($"result_{i}", results[i].Result);
            }
        }

        [Test]
        public async Task DefaultParameter_IsExclusiveMode()
        {
            string code = @"return ""test"";";

            ExecutionResult result = await _executor.ExecuteCodeAsync(
                code,
                "DynamicCommand",
                null,
                CancellationToken.None,
                compileOnly: false
            );

            Assert.IsTrue(result.Success, result.ErrorMessage);
        }

        [Test]
        public async Task NoWaitMode_ReturnsImmediatelyAfterCompile()
        {
            // Use simple code that doesn't use Task.Delay (blocked by Restricted security level)
            string code = @"return ""should_not_see_this"";";

            ExecutionResult result = await _executor.ExecuteCodeAsync(
                code,
                "DynamicCommand",
                null,
                CancellationToken.None,
                compileOnly: false,
                allowParallel: false,
                noWait: true
            );

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.IsNull(result.Result, "NoWait mode should not return execution result");
            Assert.IsTrue(result.Logs.Exists(log => log.Contains("background")), "Logs should mention background execution");
        }

        [Test]
        public async Task NoWaitMode_ReturnsCompileErrorImmediately()
        {
            string invalidCode = @"this is not valid C# code!!!";

            ExecutionResult result = await _executor.ExecuteCodeAsync(
                invalidCode,
                "DynamicCommand",
                null,
                CancellationToken.None,
                compileOnly: false,
                allowParallel: false,
                noWait: true
            );

            Assert.IsFalse(result.Success, "Compile error should fail even in NoWait mode");
            Assert.IsNotNull(result.ErrorMessage, "Should have error message");
        }

        [Test]
        public async Task NoWaitMode_WithAllowParallel_WorksTogether()
        {
            // Use simple code that doesn't use Task.Delay (blocked by Restricted security level)
            string code1 = @"return ""task1"";";
            string code2 = @"return ""task2"";";

            Task<ExecutionResult> task1 = _executor.ExecuteCodeAsync(
                code1,
                "DynamicCommand",
                null,
                CancellationToken.None,
                compileOnly: false,
                allowParallel: true,
                noWait: true
            );

            Task<ExecutionResult> task2 = _executor.ExecuteCodeAsync(
                code2,
                "DynamicCommand",
                null,
                CancellationToken.None,
                compileOnly: false,
                allowParallel: true,
                noWait: true
            );

            ExecutionResult result1 = await task1;
            ExecutionResult result2 = await task2;

            Assert.IsTrue(result1.Success, result1.ErrorMessage);
            Assert.IsTrue(result2.Success, result2.ErrorMessage);
            Assert.IsNull(result1.Result, "NoWait should not return execution result");
            Assert.IsNull(result2.Result, "NoWait should not return execution result");
        }

        [Test]
        public async Task NoWaitMode_DefaultIsFalse()
        {
            string code = @"return ""sync_result"";";

            ExecutionResult result = await _executor.ExecuteCodeAsync(
                code,
                "DynamicCommand",
                null,
                CancellationToken.None,
                compileOnly: false,
                allowParallel: false
            );

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual("sync_result", result.Result, "Default (noWait=false) should return execution result");
        }
    }
}
#endif
