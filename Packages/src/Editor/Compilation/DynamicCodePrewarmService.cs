using System;
using System.Threading;
using System.Threading.Tasks;
using io.github.hatayama.uLoopMCP.Factory;

namespace io.github.hatayama.uLoopMCP
{
    internal sealed class DynamicCodePrewarmService
    {
        private const int AutoPrewarmDelayFrameCount = 5;
        private const string AutoPrewarmCode = "return null;";
        private const string AutoPrewarmClassName = "DynamicCodeAutoPrewarmCommand";

        private readonly ExternalCompilerPathResolutionService _externalCompilerPathResolver;
        private readonly RegistryDynamicCodeExecutorFactory _executorFactory;
        private readonly object _autoPrewarmLock = new();
        private Task _autoPrewarmTask;
        private bool _hasCompletedAutoPrewarm;

        public DynamicCodePrewarmService(
            ExternalCompilerPathResolutionService externalCompilerPathResolver,
            RegistryDynamicCodeExecutorFactory executorFactory)
        {
            _externalCompilerPathResolver = externalCompilerPathResolver ?? throw new ArgumentNullException(nameof(externalCompilerPathResolver));
            _executorFactory = executorFactory ?? throw new ArgumentNullException(nameof(executorFactory));
        }

        public void Request()
        {
            RequestAsync().Forget();
        }

        public Task RequestAsync()
        {
            lock (_autoPrewarmLock)
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

        private async Task RunAsync()
        {
            if (_externalCompilerPathResolver.Resolve() == null)
            {
                lock (_autoPrewarmLock)
                {
                    _hasCompletedAutoPrewarm = true;
                }

                return;
            }

            await EditorDelay.DelayFrame(AutoPrewarmDelayFrameCount, CancellationToken.None);

            using IDynamicCodeExecutor executor = _executorFactory.Create(
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

            lock (_autoPrewarmLock)
            {
                _hasCompletedAutoPrewarm = true;
            }
        }
    }
}
