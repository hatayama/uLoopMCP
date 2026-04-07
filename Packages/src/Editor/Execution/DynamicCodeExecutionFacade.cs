using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using io.github.hatayama.uLoopMCP.Factory;
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
        private readonly IDynamicCodeExecutorProvider _executorProvider;
        private readonly Dictionary<DynamicCodeSecurityLevel, IDynamicCodeExecutor> _executorsBySecurityLevel = new();
        private readonly object _executorsLock = new();
        private readonly SemaphoreSlim _executionSemaphore = new(1, 1);
        private bool _disposed;

        public DynamicCodeExecutionFacade(
            ExternalCompilerPathResolutionService externalCompilerPathResolver,
            IDynamicCodeExecutorProvider executorProvider)
        {
            _externalCompilerPathResolver = externalCompilerPathResolver ?? throw new ArgumentNullException(nameof(externalCompilerPathResolver));
            _executorProvider = executorProvider ?? throw new ArgumentNullException(nameof(executorProvider));
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
                IDynamicCodeExecutor executor = GetOrCreateExecutor(request.SecurityLevel);
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

            lock (_executorsLock)
            {
                foreach (IDynamicCodeExecutor executor in _executorsBySecurityLevel.Values)
                {
                    executor.Dispose();
                }

                _executorsBySecurityLevel.Clear();
            }

            _executionSemaphore.Dispose();
            _disposed = true;
        }

        private IDynamicCodeExecutor GetOrCreateExecutor(DynamicCodeSecurityLevel securityLevel)
        {
            lock (_executorsLock)
            {
                if (_executorsBySecurityLevel.TryGetValue(securityLevel, out IDynamicCodeExecutor executor))
                {
                    return executor;
                }

                IDynamicCodeExecutor createdExecutor = _executorProvider.Create(securityLevel);
                _executorsBySecurityLevel.Add(securityLevel, createdExecutor);
                return createdExecutor;
            }
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
