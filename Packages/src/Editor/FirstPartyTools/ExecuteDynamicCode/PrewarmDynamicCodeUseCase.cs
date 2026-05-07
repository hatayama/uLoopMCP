using System;
using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.UnityCliLoop
{
    internal sealed class PrewarmDynamicCodeUseCase : IPrewarmDynamicCodeUseCase
    {
        private const int AutoPrewarmDelayFrameCount = 1;
        private const string AutoPrewarmCode =
            "UnityEngine.LogType previous = UnityEngine.Debug.unityLogger.filterLogType; UnityEngine.Debug.unityLogger.filterLogType = UnityEngine.LogType.Warning; try { UnityEngine.Debug.Log(\"Unity CLI Loop dynamic code prewarm\"); return \"Unity CLI Loop dynamic code prewarm\"; } finally { UnityEngine.Debug.unityLogger.filterLogType = previous; }";

        private readonly IDynamicCodeExecutionRuntime _runtime;
        private readonly CancellationToken _lifecycleCancellationToken;
        private readonly object _syncRoot = new object();
        private Task _prewarmTask;

        public PrewarmDynamicCodeUseCase(
            IDynamicCodeExecutionRuntime runtime,
            CancellationToken lifecycleCancellationToken = default)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _lifecycleCancellationToken = lifecycleCancellationToken;
        }

        public void Request()
        {
            RequestAsync().Forget();
        }

        public Task RequestAsync()
        {
            _lifecycleCancellationToken.ThrowIfCancellationRequested();

            lock (_syncRoot)
            {
                if (_prewarmTask != null && !_prewarmTask.IsCompleted)
                {
                    return _prewarmTask;
                }

                DynamicCodeStartupTelemetry.MarkPrewarmQueued();
                _prewarmTask = RunAsync();
                return _prewarmTask;
            }
        }

        private async Task RunAsync()
        {
            _lifecycleCancellationToken.ThrowIfCancellationRequested();
            if (!_runtime.SupportsAutoPrewarm())
            {
                DynamicCodeStartupTelemetry.MarkPrewarmSkipped("fast_path_unavailable");
                return;
            }

            await EditorDelay.DelayFrame(AutoPrewarmDelayFrameCount, _lifecycleCancellationToken);
            DynamicCodeStartupTelemetry.MarkPrewarmStarted();

            DynamicCodeExecutionRequest request = new()            {
                Code = AutoPrewarmCode,
                ClassName = DynamicCodeConstants.DEFAULT_CLASS_NAME,
                CompileOnly = false,
                SecurityLevel = FirstPartyDynamicCodeSettings.GetDynamicCodeSecurityLevel(),
                YieldToForegroundRequests = true
            };

            (bool entered, ExecutionResult result) = await _runtime.TryExecuteIfIdleAsync(
                request,
                _lifecycleCancellationToken);
            if (!entered)
            {
                DynamicCodeStartupTelemetry.MarkPrewarmYielded("foreground_request_preempted");
                return;
            }

            if (!result.Success)
            {
                DynamicCodeStartupTelemetry.MarkPrewarmFailed(result.ErrorMessage);
                return;
            }

            DynamicCodeStartupTelemetry.MarkPrewarmCompleted();
            DynamicCodeForegroundWarmupState.MarkCompletedByBackgroundWarmup();
        }
    }
}
