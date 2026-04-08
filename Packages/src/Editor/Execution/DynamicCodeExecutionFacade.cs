using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Provides the runtime-facing facade for execute-dynamic-code.
    /// Entry points depend on this facade instead of reaching into factory and executor wiring directly.
    /// </summary>
    internal sealed class DynamicCodeExecutionFacade : IShutdownAwareDynamicCodeExecutionRuntime, IDisposable
    {
        private const int BusyHandoffWindowMilliseconds = 50;

        internal static Action AfterSemaphoreEnteredForTests { get; set; }
        internal static Func<Task> AfterBackgroundExecutionStatePublishedForTests { get; set; }
        internal static Func<Task> AfterBusySemaphoreProbeFailedForTests { get; set; }

        private readonly ICompiledAssemblyBuilder _assemblyBuilder;
        private readonly IDynamicCodeExecutorPool _executorPool;
        private readonly SemaphoreSlim _executionSemaphore = new(1, 1);
        private readonly CancellationTokenSource _lifetimeCancellationTokenSource = new();
        private readonly TaskCompletionSource<bool> _shutdownCompletionSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object _disposeLock = new();
        private readonly object _executionStateLock = new();
        private readonly object _backgroundPrewarmTransitionLock = new();
        private CancellationTokenSource _backgroundPrewarmCancellationTokenSource;
        private bool _backgroundPrewarmInProgress;
        private bool _resourcesDisposed;
        private bool _disposed;

        public DynamicCodeExecutionFacade(
            ICompiledAssemblyBuilder assemblyBuilder,
            IDynamicCodeExecutorPool executorPool)
        {
            _assemblyBuilder = assemblyBuilder ?? throw new ArgumentNullException(nameof(assemblyBuilder));
            _executorPool = executorPool ?? throw new ArgumentNullException(nameof(executorPool));
        }

        public bool SupportsAutoPrewarm()
        {
            ThrowIfDisposed();
            return _assemblyBuilder.SupportsAutoPrewarm();
        }

        public async Task<ExecutionResult> ExecuteAsync(
            DynamicCodeExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            Debug.Assert(request != null, "request must not be null");
            Debug.Assert(!string.IsNullOrWhiteSpace(request.Code), "request.Code must not be empty");

            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            bool entered = await _executionSemaphore.WaitAsync(0, cancellationToken);
            if (!entered)
            {
                await InvokeAfterBusySemaphoreProbeFailedHookAsync();
                if (!TryCancelBackgroundPrewarmAfterTransition(request))
                {
                    entered = await TryAcquireAfterBusyHandoffAsync(cancellationToken);
                    if (!entered)
                    {
                        return CreateExecutionInProgressResult();
                    }
                }
                else
                {
                    using CancellationTokenSource waitCancellationTokenSource =
                        CancellationTokenSource.CreateLinkedTokenSource(
                            cancellationToken,
                            _lifetimeCancellationTokenSource.Token);
                    await _executionSemaphore.WaitAsync(waitCancellationTokenSource.Token);
                    entered = true;
                }
            }

            CancellationTokenSource executionCancellationTokenSource = null;
            try
            {
                AfterSemaphoreEnteredForTests?.Invoke();
                ThrowIfDisposed();
                executionCancellationTokenSource =
                    CreateExecutionCancellationTokenSource(cancellationToken);
                SetExecutionState(request, executionCancellationTokenSource);
                return await ExecuteCoreAsync(request, executionCancellationTokenSource.Token);
            }
            finally
            {
                ClearExecutionState(request, executionCancellationTokenSource);
                executionCancellationTokenSource?.Dispose();
                DisposeResourcesIfRequested();
                _executionSemaphore.Release();
            }
        }

        public async Task<(bool Entered, ExecutionResult Result)> TryExecuteIfIdleAsync(
            DynamicCodeExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            Debug.Assert(request != null, "request must not be null");
            Debug.Assert(!string.IsNullOrWhiteSpace(request.Code), "request.Code must not be empty");

            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            if (!request.YieldToForegroundRequests)
            {
                return await TryExecuteWithoutForegroundYieldAsync(request, cancellationToken);
            }

            bool entered;
            CancellationTokenSource executionCancellationTokenSource = null;
            lock (_backgroundPrewarmTransitionLock)
            {
                entered = _executionSemaphore.Wait(0);
                if (!entered)
                {
                    return (false, null);
                }

                ThrowIfDisposed();
                executionCancellationTokenSource =
                    CreateExecutionCancellationTokenSource(cancellationToken);
                SetExecutionState(request, executionCancellationTokenSource);
            }

            try
            {
                await InvokeAfterBackgroundExecutionStatePublishedHookAsync();
                AfterSemaphoreEnteredForTests?.Invoke();
                ExecutionResult result = await ExecuteCoreAsync(request, executionCancellationTokenSource.Token);
                return (true, result);
            }
            finally
            {
                ClearExecutionState(request, executionCancellationTokenSource);
                executionCancellationTokenSource?.Dispose();
                DisposeResourcesIfRequested();
                _executionSemaphore.Release();
            }
        }

        private async Task<(bool Entered, ExecutionResult Result)> TryExecuteWithoutForegroundYieldAsync(
            DynamicCodeExecutionRequest request,
            CancellationToken cancellationToken)
        {
            bool entered = await _executionSemaphore.WaitAsync(0, cancellationToken);
            if (!entered)
            {
                return (false, null);
            }

            CancellationTokenSource executionCancellationTokenSource = null;
            try
            {
                AfterSemaphoreEnteredForTests?.Invoke();
                ThrowIfDisposed();
                executionCancellationTokenSource =
                    CreateExecutionCancellationTokenSource(cancellationToken);
                SetExecutionState(request, executionCancellationTokenSource);
                ExecutionResult result = await ExecuteCoreAsync(request, executionCancellationTokenSource.Token);
                return (true, result);
            }
            finally
            {
                ClearExecutionState(request, executionCancellationTokenSource);
                executionCancellationTokenSource?.Dispose();
                DisposeResourcesIfRequested();
                _executionSemaphore.Release();
            }
        }

        private CancellationTokenSource CreateExecutionCancellationTokenSource(
            CancellationToken cancellationToken)
        {
            return CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _lifetimeCancellationTokenSource.Token);
        }

        private async Task<ExecutionResult> ExecuteCoreAsync(
            DynamicCodeExecutionRequest request,
            CancellationToken cancellationToken)
        {
            IDynamicCodeExecutor executor = _executorPool.GetOrCreate(request.SecurityLevel);
            return await executor.ExecuteCodeAsync(
                request.Code,
                request.ClassName,
                request.Parameters,
                cancellationToken,
                request.CompileOnly);
        }

        private bool TryCancelBackgroundPrewarmAfterTransition(DynamicCodeExecutionRequest request)
        {
            lock (_backgroundPrewarmTransitionLock)
            {
                return TryCancelBackgroundPrewarm(request);
            }
        }

        private bool TryCancelBackgroundPrewarm(DynamicCodeExecutionRequest request)
        {
            if (request.YieldToForegroundRequests)
            {
                return false;
            }

            lock (_executionStateLock)
            {
                if (!_backgroundPrewarmInProgress || _backgroundPrewarmCancellationTokenSource == null)
                {
                    return false;
                }

                _backgroundPrewarmCancellationTokenSource.Cancel();
                return true;
            }
        }

        private static async Task InvokeAfterBackgroundExecutionStatePublishedHookAsync()
        {
            Func<Task> hook = AfterBackgroundExecutionStatePublishedForTests;
            if (hook == null)
            {
                return;
            }

            await hook();
        }

        private static async Task InvokeAfterBusySemaphoreProbeFailedHookAsync()
        {
            Func<Task> hook = AfterBusySemaphoreProbeFailedForTests;
            if (hook == null)
            {
                return;
            }

            await hook();
        }

        private async Task<bool> TryAcquireAfterBusyHandoffAsync(CancellationToken cancellationToken)
        {
            using CancellationTokenSource handoffCancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            handoffCancellationTokenSource.CancelAfter(BusyHandoffWindowMilliseconds);

            try
            {
                await _executionSemaphore.WaitAsync(handoffCancellationTokenSource.Token);
                return true;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return false;
            }
        }

        private void SetExecutionState(
            DynamicCodeExecutionRequest request,
            CancellationTokenSource executionCancellationTokenSource)
        {
            lock (_executionStateLock)
            {
                _backgroundPrewarmInProgress = request.YieldToForegroundRequests;
                _backgroundPrewarmCancellationTokenSource = request.YieldToForegroundRequests
                    ? executionCancellationTokenSource
                    : null;
            }
        }

        private void ClearExecutionState(
            DynamicCodeExecutionRequest request,
            CancellationTokenSource executionCancellationTokenSource)
        {
            if (!request.YieldToForegroundRequests)
            {
                return;
            }

            lock (_executionStateLock)
            {
                if (!ReferenceEquals(_backgroundPrewarmCancellationTokenSource, executionCancellationTokenSource))
                {
                    return;
                }

                _backgroundPrewarmInProgress = false;
                _backgroundPrewarmCancellationTokenSource = null;
            }
        }

        private static ExecutionResult CreateExecutionInProgressResult()
        {
            return new ExecutionResult
            {
                Success = false,
                ErrorMessage = McpConstants.ERROR_MESSAGE_EXECUTION_IN_PROGRESS
            };
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lifetimeCancellationTokenSource.Cancel();
            if (!_executionSemaphore.Wait(0))
            {
                return;
            }

            try
            {
                DisposeResourcesIfRequested();
            }
            finally
            {
                _executionSemaphore.Release();
            }
        }

        public Task ShutdownAsync()
        {
            Dispose();
            return _shutdownCompletionSource.Task;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(DynamicCodeExecutionFacade));
            }
        }

        private void DisposeResourcesIfRequested()
        {
            if (!_disposed)
            {
                return;
            }

            lock (_disposeLock)
            {
                if (_resourcesDisposed)
                {
                    return;
                }

                _resourcesDisposed = true;
            }

            _executorPool.Dispose();
            _lifetimeCancellationTokenSource.Dispose();
            _shutdownCompletionSource.TrySetResult(true);
        }
    }
}
