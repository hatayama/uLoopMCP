using System;
using System.Threading;
using System.Threading.Tasks;
using io.github.hatayama.UnityCliLoop.Factory;

namespace io.github.hatayama.UnityCliLoop
{
    internal sealed class DynamicCodeServicesRegistry
    {
        private readonly object _serverScopedServicesLock = new();
        private TimeSpan _serverScopedDrainTimeout = TimeSpan.FromSeconds(5);
        private Task _serverScopedDrainTask = Task.CompletedTask;
        private CancellationTokenSource _serverScopedLifetimeCancellationTokenSource;
        private IDynamicCodeExecutorPool _executorPool;
        private IDynamicCodeExecutionRuntime _runtimeFacade;
        private IPrewarmDynamicCodeUseCase _prewarmDynamicCodeUseCase;

        private readonly Lazy<IDynamicCodeSourcePreparationService> _sourcePreparationServiceValue =
            new Lazy<IDynamicCodeSourcePreparationService>(
                DynamicCompilationRuntimeServicesRegistry.CreateSourcePreparationService);

        private readonly Lazy<ICompiledAssemblyBuilder> _assemblyBuilderValue =
            new Lazy<ICompiledAssemblyBuilder>(
                DynamicCompilationRuntimeServicesRegistry.CreateAssemblyBuilder);

        public IDynamicCodeSourcePreparationService SourcePreparationService => _sourcePreparationServiceValue.Value;

        public ICompiledAssemblyBuilder AssemblyBuilder => _assemblyBuilderValue.Value;

        public CompiledCommandEntryPointResolver CommandEntryPointResolver { get; } =
            new CompiledCommandEntryPointResolver();

        private readonly Lazy<RegistryDynamicCodeExecutorFactory> _executorFactoryValue;

        public DynamicCodeServicesRegistry()
        {
            _executorFactoryValue = new Lazy<RegistryDynamicCodeExecutorFactory>(
                () => new RegistryDynamicCodeExecutorFactory(
                    SourcePreparationService,
                    CommandEntryPointResolver));
        }

        public RegistryDynamicCodeExecutorFactory ExecutorFactory => _executorFactoryValue.Value;

        public async Task<IExecuteDynamicCodeUseCase> GetExecuteDynamicCodeUseCaseAsync()
        {
            IDynamicCodeExecutionRuntime runtimeFacade = await GetRuntimeFacadeAsync();
            return new ExecuteDynamicCodeUseCase(runtimeFacade);
        }

        public async Task<IPrewarmDynamicCodeUseCase> GetPrewarmDynamicCodeUseCaseAsync(
            string serverStartingLockToken = null)
        {
            await EnsureServerScopedServicesInitializedAsync(serverStartingLockToken);

            lock (_serverScopedServicesLock)
            {
                return _prewarmDynamicCodeUseCase;
            }
        }

        public void ResetServerScopedServices()
        {
            CancellationTokenSource lifetimeCancellationTokenSource;
            IDynamicCodeExecutionRuntime runtimeFacade;
            Task shutdownTask;

            lock (_serverScopedServicesLock)
            {
                lifetimeCancellationTokenSource = _serverScopedLifetimeCancellationTokenSource;
                runtimeFacade = _runtimeFacade;

                _serverScopedLifetimeCancellationTokenSource = null;
                _executorPool = null;
                _runtimeFacade = null;
                _prewarmDynamicCodeUseCase = null;

                shutdownTask = CreateShutdownTask(lifetimeCancellationTokenSource, runtimeFacade);
                _serverScopedDrainTask = ChainDrainTask(_serverScopedDrainTask, shutdownTask);
            }
        }

        private async Task<IDynamicCodeExecutionRuntime> GetRuntimeFacadeAsync()
        {
            await EnsureServerScopedServicesInitializedAsync();

            lock (_serverScopedServicesLock)
            {
                return _runtimeFacade;
            }
        }

        private async Task EnsureServerScopedServicesInitializedAsync(
            string serverStartingLockToken = null)
        {
            Task drainTask;

            lock (_serverScopedServicesLock)
            {
                if (_runtimeFacade != null)
                {
                    AttachServerStartingLockTokenIfNeeded(serverStartingLockToken);
                    return;
                }

                drainTask = _serverScopedDrainTask;
            }

            await AwaitDrainTaskAsync(drainTask);

            lock (_serverScopedServicesLock)
            {
                if (_runtimeFacade != null)
                {
                    AttachServerStartingLockTokenIfNeeded(serverStartingLockToken);
                    return;
                }

                _serverScopedLifetimeCancellationTokenSource = new CancellationTokenSource();
                _executorPool = new DynamicCodeExecutorPool(ExecutorFactory);
                _runtimeFacade = new DynamicCodeExecutionFacade(
                    AssemblyBuilder,
                    _executorPool);
                _prewarmDynamicCodeUseCase = new PrewarmDynamicCodeUseCase(
                    _runtimeFacade,
                    _serverScopedLifetimeCancellationTokenSource.Token,
                    null,
                    serverStartingLockToken);
            }
        }

        private void AttachServerStartingLockTokenIfNeeded(string serverStartingLockToken)
        {
            if (string.IsNullOrEmpty(serverStartingLockToken))
            {
                return;
            }

            if (_prewarmDynamicCodeUseCase is PrewarmDynamicCodeUseCase prewarmDynamicCodeUseCase)
            {
                prewarmDynamicCodeUseCase.AttachServerStartingLockToken(serverStartingLockToken);
            }
        }

        private Task CreateShutdownTask(
            CancellationTokenSource lifetimeCancellationTokenSource,
            IDynamicCodeExecutionRuntime runtimeFacade)
        {
            if (lifetimeCancellationTokenSource == null && runtimeFacade == null)
            {
                return Task.CompletedTask;
            }

            return ShutdownServerScopedServicesAsync(lifetimeCancellationTokenSource, runtimeFacade);
        }

        private Task ChainDrainTask(Task currentDrainTask, Task nextDrainTask)
        {
            Task observedCurrentDrainTask = CreateObservedDrainTask(
                currentDrainTask,
                "server_scoped_shutdown_previous_failed");
            Task observedNextDrainTask = CreateObservedDrainTask(
                nextDrainTask,
                "server_scoped_shutdown_failed");

            if (observedCurrentDrainTask.IsCompletedSuccessfully)
            {
                return observedNextDrainTask;
            }

            return ContinueAfterDrainAsync(observedCurrentDrainTask, observedNextDrainTask);
        }

        private static async Task ContinueAfterDrainAsync(Task currentDrainTask, Task nextDrainTask)
        {
            await currentDrainTask;
            await nextDrainTask;
        }

        private static Task CreateObservedDrainTask(Task drainTask, string operation)
        {
            if (drainTask == null || drainTask.IsCompletedSuccessfully)
            {
                return Task.CompletedTask;
            }

            return drainTask.ContinueWith(
                task => LogDrainFailure(operation, task),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private static void LogDrainFailure(string operation, Task drainTask)
        {
            if (drainTask.IsFaulted)
            {
                Exception exception = drainTask.Exception?.InnerException ?? drainTask.Exception;
                VibeLogger.LogWarning(
                    operation,
                    "Server-scoped dynamic code shutdown failed; continuing with a fresh runtime",
                    new
                    {
                        exception_type = exception?.GetType().Name,
                        exception_message = exception?.Message
                    });
                return;
            }

            if (drainTask.IsCanceled)
            {
                VibeLogger.LogInfo(
                    operation,
                    "Server-scoped dynamic code shutdown was cancelled; continuing with a fresh runtime");
            }
        }

        internal async Task AwaitDrainTaskAsync(Task drainTask)
        {
            if (drainTask == null)
            {
                return;
            }

            if (drainTask.IsCompleted)
            {
                await drainTask;
                return;
            }

            TimeSpan timeout = _serverScopedDrainTimeout;
            UnityEngine.Debug.Assert(timeout > TimeSpan.Zero, "server-scoped drain timeout must be positive");

            Task completedTask = await Task.WhenAny(drainTask, Task.Delay(timeout));
            if (completedTask == drainTask)
            {
                await drainTask;
                return;
            }

            VibeLogger.LogWarning(
                "server_scoped_shutdown_timeout",
                "Server-scoped dynamic code shutdown exceeded the drain timeout; continuing with a fresh runtime",
                new
                {
                    timeout_ms = (int)timeout.TotalMilliseconds
                });
        }

        private static async Task ShutdownServerScopedServicesAsync(
            CancellationTokenSource lifetimeCancellationTokenSource,
            IDynamicCodeExecutionRuntime runtimeFacade)
        {
            lifetimeCancellationTokenSource?.Cancel();
            DynamicCompilationRuntimeServicesRegistry.ShutdownForServerReset();

            if (runtimeFacade is IShutdownAwareDynamicCodeExecutionRuntime shutdownAwareRuntime)
            {
                await shutdownAwareRuntime.ShutdownAsync();
            }
            else
            {
                (runtimeFacade as IDisposable)?.Dispose();
            }

            lifetimeCancellationTokenSource?.Dispose();
        }

        internal TimeSpan SwapDrainTimeoutForTests(TimeSpan timeout)
        {
            UnityEngine.Debug.Assert(timeout > TimeSpan.Zero, "timeout must be positive");

            TimeSpan previous = _serverScopedDrainTimeout;
            _serverScopedDrainTimeout = timeout;
            return previous;
        }

        internal void SetServerScopedServicesForTests(
            CancellationTokenSource lifetimeCancellationTokenSource,
            IDynamicCodeExecutorPool executorPool,
            IDynamicCodeExecutionRuntime runtimeFacade,
            IPrewarmDynamicCodeUseCase prewarmDynamicCodeUseCase)
        {
            lock (_serverScopedServicesLock)
            {
                _serverScopedLifetimeCancellationTokenSource = lifetimeCancellationTokenSource;
                _executorPool = executorPool;
                _runtimeFacade = runtimeFacade;
                _prewarmDynamicCodeUseCase = prewarmDynamicCodeUseCase;
            }
        }

        internal Task GetServerScopedDrainTaskForTests()
        {
            lock (_serverScopedServicesLock)
            {
                return _serverScopedDrainTask;
            }
        }

        internal void ResetStateForTests()
        {
            lock (_serverScopedServicesLock)
            {
                _serverScopedLifetimeCancellationTokenSource = null;
                _executorPool = null;
                _runtimeFacade = null;
                _prewarmDynamicCodeUseCase = null;
                _serverScopedDrainTask = Task.CompletedTask;
            }
        }
    }

    internal static class DynamicCodeServices
    {
        private static readonly DynamicCodeServicesRegistry RegistryValue =
            new DynamicCodeServicesRegistry();

        public static IDynamicCodeSourcePreparationService SourcePreparationService
        {
            get { return RegistryValue.SourcePreparationService; }
        }

        public static ICompiledAssemblyBuilder AssemblyBuilder
        {
            get { return RegistryValue.AssemblyBuilder; }
        }

        public static CompiledCommandEntryPointResolver CommandEntryPointResolver
        {
            get { return RegistryValue.CommandEntryPointResolver; }
        }

        public static RegistryDynamicCodeExecutorFactory ExecutorFactory
        {
            get { return RegistryValue.ExecutorFactory; }
        }

        public static Task<IExecuteDynamicCodeUseCase> GetExecuteDynamicCodeUseCaseAsync()
        {
            return RegistryValue.GetExecuteDynamicCodeUseCaseAsync();
        }

        public static Task<IPrewarmDynamicCodeUseCase> GetPrewarmDynamicCodeUseCaseAsync(
            string serverStartingLockToken = null)
        {
            return RegistryValue.GetPrewarmDynamicCodeUseCaseAsync(serverStartingLockToken);
        }

        public static void ResetServerScopedServices()
        {
            RegistryValue.ResetServerScopedServices();
        }

        internal static Task AwaitDrainTaskAsync(Task drainTask)
        {
            return RegistryValue.AwaitDrainTaskAsync(drainTask);
        }

        internal static TimeSpan SwapDrainTimeoutForTests(TimeSpan timeout)
        {
            return RegistryValue.SwapDrainTimeoutForTests(timeout);
        }

        internal static void SetServerScopedServicesForTests(
            CancellationTokenSource lifetimeCancellationTokenSource,
            IDynamicCodeExecutorPool executorPool,
            IDynamicCodeExecutionRuntime runtimeFacade,
            IPrewarmDynamicCodeUseCase prewarmDynamicCodeUseCase)
        {
            RegistryValue.SetServerScopedServicesForTests(
                lifetimeCancellationTokenSource,
                executorPool,
                runtimeFacade,
                prewarmDynamicCodeUseCase);
        }

        internal static Task GetServerScopedDrainTaskForTests()
        {
            return RegistryValue.GetServerScopedDrainTaskForTests();
        }

        internal static void ResetStateForTests()
        {
            RegistryValue.ResetStateForTests();
        }
    }
}
