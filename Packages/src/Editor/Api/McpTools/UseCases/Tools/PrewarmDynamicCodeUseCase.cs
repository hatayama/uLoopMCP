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
        private const string AutoPrewarmOperation = "dynamic_code_auto_prewarm";

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
                VibeLogger.LogInfo(
                    AutoPrewarmOperation,
                    "Skipping dynamic code auto prewarm because the fast path is unavailable",
                    new { reason = "fast_path_unavailable" });

                lock (_autoPrewarmLock)
                {
                    _hasCompletedAutoPrewarm = true;
                }

                return;
            }

            VibeLogger.LogInfo(
                AutoPrewarmOperation,
                "Starting dynamic code auto prewarm",
                new { delay_frames = AutoPrewarmDelayFrameCount, class_name = AutoPrewarmClassName });

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
                VibeLogger.LogWarning(
                    AutoPrewarmOperation,
                    "Dynamic code auto prewarm failed",
                    new
                    {
                        class_name = AutoPrewarmClassName,
                        error_message = result.ErrorMessage,
                        logs = result.Logs
                    });
                return;
            }

            VibeLogger.LogInfo(
                AutoPrewarmOperation,
                "Dynamic code auto prewarm completed successfully",
                new { class_name = AutoPrewarmClassName });

            lock (_autoPrewarmLock)
            {
                _hasCompletedAutoPrewarm = true;
            }
        }
    }
}
