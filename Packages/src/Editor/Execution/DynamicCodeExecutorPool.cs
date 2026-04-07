using System;
using System.Collections.Generic;
using io.github.hatayama.uLoopMCP.Factory;

namespace io.github.hatayama.uLoopMCP
{
    internal sealed class DynamicCodeExecutorPool : IDynamicCodeExecutorPool
    {
        private readonly IDynamicCodeExecutorProvider _executorProvider;
        private readonly Dictionary<DynamicCodeSecurityLevel, IDynamicCodeExecutor> _executorsBySecurityLevel = new();
        private readonly object _executorsLock = new();
        private bool _disposed;

        public DynamicCodeExecutorPool(IDynamicCodeExecutorProvider executorProvider)
        {
            _executorProvider = executorProvider ?? throw new ArgumentNullException(nameof(executorProvider));
        }

        public IDynamicCodeExecutor GetOrCreate(DynamicCodeSecurityLevel securityLevel)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(DynamicCodeExecutorPool));
            }

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

            _disposed = true;
        }
    }
}
