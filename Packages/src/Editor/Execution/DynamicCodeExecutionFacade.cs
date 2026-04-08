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
    internal sealed class DynamicCodeExecutionFacade : IDynamicCodeExecutionRuntime, IDisposable
    {
        private readonly ICompiledAssemblyBuilder _assemblyBuilder;
        private readonly IDynamicCodeExecutorPool _executorPool;
        private readonly SemaphoreSlim _executionSemaphore = new(1, 1);
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
                return CreateExecutionInProgressResult();
            }

            try
            {
                return await ExecuteCoreAsync(request, cancellationToken);
            }
            finally
            {
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

            bool entered = await _executionSemaphore.WaitAsync(0, cancellationToken);
            if (!entered)
            {
                return (false, null);
            }

            try
            {
                ExecutionResult result = await ExecuteCoreAsync(request, cancellationToken);
                return (true, result);
            }
            finally
            {
                _executionSemaphore.Release();
            }
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

            _executorPool.Dispose();
            _executionSemaphore.Dispose();
            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(DynamicCodeExecutionFacade));
            }
        }
    }
}
