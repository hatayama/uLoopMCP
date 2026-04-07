using System;
using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    internal sealed class PrewarmDynamicCodeUseCase : IPrewarmDynamicCodeUseCase
    {
        private const int AutoPrewarmDelayFrameCount = 5;
        private const string AutoPrewarmCode = "return null;";
        private const string AutoPrewarmClassName = "DynamicCodeAutoPrewarmCommand";

        private readonly IDynamicCodeExecutionRuntime _runtime;
        private readonly object _autoPrewarmLock = new();
        private Task _autoPrewarmTask;
        private bool _hasCompletedAutoPrewarm;

        public PrewarmDynamicCodeUseCase(IDynamicCodeExecutionRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
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
            if (!_runtime.SupportsAutoPrewarm())
            {
                lock (_autoPrewarmLock)
                {
                    _hasCompletedAutoPrewarm = true;
                }

                return;
            }

            await EditorDelay.DelayFrame(AutoPrewarmDelayFrameCount, CancellationToken.None);

            DynamicCodeExecutionRequest request = new DynamicCodeExecutionRequest
            {
                Code = AutoPrewarmCode,
                ClassName = AutoPrewarmClassName,
                SecurityLevel = DynamicCodeSecurityLevel.Restricted,
                CompileOnly = false
            };

            ExecutionResult result = await _runtime.ExecuteAsync(
                request,
                CancellationToken.None);

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
