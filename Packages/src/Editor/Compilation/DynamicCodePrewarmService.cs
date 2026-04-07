using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    internal static class DynamicCodePrewarmService
    {
        private const int AutoPrewarmDelayFrameCount = 5;
        private const string AutoPrewarmCode = "return null;";
        private const string AutoPrewarmClassName = "DynamicCodeAutoPrewarmCommand";

        private static readonly object AutoPrewarmLock = new();
        private static Task _autoPrewarmTask;
        private static bool _hasCompletedAutoPrewarm;

        public static void Request()
        {
            RequestAsync().Forget();
        }

        public static Task RequestAsync()
        {
            lock (AutoPrewarmLock)
            {
                if (_hasCompletedAutoPrewarm)
                {
                    return Task.CompletedTask;
                }

                if (_autoPrewarmTask != null && !_autoPrewarmTask.IsCompleted)
                {
                    return _autoPrewarmTask;
                }

                _autoPrewarmTask = RunAsync();
                return _autoPrewarmTask;
            }
        }

        private static async Task RunAsync()
        {
            if (ExternalCompilerPathResolver.Resolve() == null)
            {
                lock (AutoPrewarmLock)
                {
                    _hasCompletedAutoPrewarm = true;
                }

                return;
            }

            await EditorDelay.DelayFrame(AutoPrewarmDelayFrameCount, CancellationToken.None);

            using IDynamicCodeExecutor executor = Factory.DynamicCodeExecutorFactory.Create(
                DynamicCodeSecurityLevel.Restricted);
            ExecutionResult result = await executor.ExecuteCodeAsync(
                AutoPrewarmCode,
                AutoPrewarmClassName,
                null,
                CancellationToken.None,
                false);

            if (!result.Success)
            {
                return;
            }

            lock (AutoPrewarmLock)
            {
                _hasCompletedAutoPrewarm = true;
            }
        }
    }
}
