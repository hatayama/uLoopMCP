using System;
using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    internal sealed class DynamicCodePrewarmService
    {
        private const int AutoPrewarmDelayFrameCount = 5;
        private const string AutoPrewarmCode = "return null;";
        private const string AutoPrewarmClassName = "DynamicCodeAutoPrewarmCommand";

        private readonly DynamicCodeExecutionFacade _executionFacade;
        private readonly object _autoPrewarmLock = new();
        private Task _autoPrewarmTask;
        private bool _hasCompletedAutoPrewarm;

        public DynamicCodePrewarmService(DynamicCodeExecutionFacade executionFacade)
        {
            _executionFacade = executionFacade ?? throw new ArgumentNullException(nameof(executionFacade));
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
            if (!_executionFacade.SupportsAutoPrewarm())
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

            ExecutionResult result = await _executionFacade.ExecuteAsync(
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
