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
        private readonly ExternalCompilerPathResolutionService _externalCompilerPathResolver;
        private readonly IDynamicCodeExecutorPool _executorPool;
        private readonly SemaphoreSlim _executionSemaphore = new(1, 1);
        private bool _disposed;

        public DynamicCodeExecutionFacade(
            ExternalCompilerPathResolutionService externalCompilerPathResolver,
            IDynamicCodeExecutorPool executorPool)
        {
            _externalCompilerPathResolver = externalCompilerPathResolver ?? throw new ArgumentNullException(nameof(externalCompilerPathResolver));
            _executorPool = executorPool ?? throw new ArgumentNullException(nameof(executorPool));
        }

        public bool SupportsAutoPrewarm()
        {
            ThrowIfDisposed();
            return _externalCompilerPathResolver.Resolve() != null;
        }

        public async Task<ExecutionResult> ExecuteAsync(
            DynamicCodeExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            Debug.Assert(request != null, "request must not be null");
            Debug.Assert(!string.IsNullOrWhiteSpace(request.Code), "request.Code must not be empty");

            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            await _executionSemaphore.WaitAsync(cancellationToken);
            try
            {
                IDynamicCodeExecutor executor = _executorPool.GetOrCreate(request.SecurityLevel);
                return await executor.ExecuteCodeAsync(
                    request.Code,
                    request.ClassName,
                    request.Parameters,
                    cancellationToken,
                    request.CompileOnly);
            }
            finally
            {
                _executionSemaphore.Release();
            }
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
