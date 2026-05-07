using System;
using System.Threading;
using System.Threading.Tasks;
using io.github.hatayama.UnityCliLoop.FirstPartyTools.Factory;

using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    internal sealed class DynamicCodeServicesRegistry
    {
        private readonly IDynamicCompilationRuntimeServicesFactory _runtimeServicesFactory;
        private readonly DynamicCompilationServiceRegistryService _compilationServiceRegistry;
        private readonly object _serverScopedServicesLock = new();
        private TimeSpan _serverScopedDrainTimeout = TimeSpan.FromSeconds(5);
        private Task _serverScopedDrainTask = Task.CompletedTask;
        private CancellationTokenSource _serverScopedLifetimeCancellationTokenSource;
        private IDynamicCodeExecutorPool _executorPool;
        private IDynamicCodeExecutionRuntime _runtimeFacade;
        private IPrewarmDynamicCodeUseCase _prewarmDynamicCodeUseCase;

        private readonly Lazy<IDynamicCodeSourcePreparationService> _sourcePreparationServiceValue;

        private readonly Lazy<ICompiledAssemblyBuilder> _assemblyBuilderValue;

        public IDynamicCodeSourcePreparationService SourcePreparationService => _sourcePreparationServiceValue.Value;

        public ICompiledAssemblyBuilder AssemblyBuilder => _assemblyBuilderValue.Value;

        public CompiledCommandEntryPointResolver CommandEntryPointResolver { get; } =
            new CompiledCommandEntryPointResolver();

        private readonly Lazy<RegistryDynamicCodeExecutorFactory> _executorFactoryValue;

        public DynamicCodeServicesRegistry(
            IDynamicCompilationRuntimeServicesFactory runtimeServicesFactory,
            DynamicCompilationServiceRegistryService compilationServiceRegistry)
        {
            UnityEngine.Debug.Assert(runtimeServicesFactory != null, "runtimeServicesFactory must not be null");
            UnityEngine.Debug.Assert(compilationServiceRegistry != null, "compilationServiceRegistry must not be null");

            _runtimeServicesFactory = runtimeServicesFactory ?? throw new ArgumentNullException(nameof(runtimeServicesFactory));
            _compilationServiceRegistry = compilationServiceRegistry ?? throw new ArgumentNullException(nameof(compilationServiceRegistry));
            _sourcePreparationServiceValue = new Lazy<IDynamicCodeSourcePreparationService>(
                _runtimeServicesFactory.CreateSourcePreparationService);
            _assemblyBuilderValue = new Lazy<ICompiledAssemblyBuilder>(
                _runtimeServicesFactory.CreateAssemblyBuilder);
            _executorFactoryValue = new Lazy<RegistryDynamicCodeExecutorFactory>(
                () => new RegistryDynamicCodeExecutorFactory(
                    _compilationServiceRegistry,
                    SourcePreparationService,
                    CommandEntryPointResolver));
        }

        public RegistryDynamicCodeExecutorFactory ExecutorFactory => _executorFactoryValue.Value;

        public async Task<IExecuteDynamicCodeUseCase> GetExecuteDynamicCodeUseCaseAsync()
        {
            IDynamicCodeExecutionRuntime runtimeFacade = await GetRuntimeFacadeAsync();
            return new ExecuteDynamicCodeUseCase(runtimeFacade);
        }

        public async Task<IPrewarmDynamicCodeUseCase> GetPrewarmDynamicCodeUseCaseAsync()
        {
            await EnsureServerScopedServicesInitializedAsync();

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

        private async Task EnsureServerScopedServicesInitializedAsync()
        {
            Task drainTask;

            lock (_serverScopedServicesLock)
            {
                if (_runtimeFacade != null)
                {
                    return;
                }

                drainTask = _serverScopedDrainTask;
            }

            await AwaitDrainTaskAsync(drainTask);

            lock (_serverScopedServicesLock)
            {
                if (_runtimeFacade != null)
                {
                    return;
                }

                _serverScopedLifetimeCancellationTokenSource = new CancellationTokenSource();
                _executorPool = new DynamicCodeExecutorPool(ExecutorFactory);
                _runtimeFacade = new DynamicCodeExecutionFacade(
                    AssemblyBuilder,
                    _executorPool);
                _prewarmDynamicCodeUseCase = new PrewarmDynamicCodeUseCase(
                    _runtimeFacade,
                    _serverScopedLifetimeCancellationTokenSource.Token);
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

        private async Task ShutdownServerScopedServicesAsync(
            CancellationTokenSource lifetimeCancellationTokenSource,
            IDynamicCodeExecutionRuntime runtimeFacade)
        {
            lifetimeCancellationTokenSource?.Cancel();
            _runtimeServicesFactory.ShutdownForServerReset();

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

        internal IDynamicCompilationServiceFactory SwapCompilationFactoryForTests(
            IDynamicCompilationServiceFactory factory)
        {
            return _compilationServiceRegistry.SwapFactoryForTests(factory);
        }
    }

    internal static class DynamicCodeServices
    {
        private static readonly object SyncRoot = new object();
        private static DynamicCodeServicesRegistry RegistryValue;

        internal static void RegisterRegistry(DynamicCodeServicesRegistry registry)
        {
            UnityEngine.Debug.Assert(registry != null, "registry must not be null");

            lock (SyncRoot)
            {
                RegistryValue = registry ?? throw new ArgumentNullException(nameof(registry));
            }
        }

        internal static DynamicCodeServicesRegistry GetRegistry()
        {
            lock (SyncRoot)
            {
                if (RegistryValue == null)
                {
                    RegistryValue = CreateDefaultRegistry();
                }

                return RegistryValue;
            }
        }

        private static DynamicCodeServicesRegistry CreateDefaultRegistry()
        {
            return new DynamicCodeServicesRegistry(
                new DynamicCompilationRuntimeServicesFactory(),
                new DynamicCompilationServiceRegistryService(new DynamicCodeCompilationServiceFactory()));
        }

        public static IDynamicCodeSourcePreparationService SourcePreparationService
        {
            get { return GetRegistry().SourcePreparationService; }
        }

        public static ICompiledAssemblyBuilder AssemblyBuilder
        {
            get { return GetRegistry().AssemblyBuilder; }
        }

        public static CompiledCommandEntryPointResolver CommandEntryPointResolver
        {
            get { return GetRegistry().CommandEntryPointResolver; }
        }

        public static RegistryDynamicCodeExecutorFactory ExecutorFactory
        {
            get { return GetRegistry().ExecutorFactory; }
        }

        public static Task<IExecuteDynamicCodeUseCase> GetExecuteDynamicCodeUseCaseAsync()
        {
            return GetRegistry().GetExecuteDynamicCodeUseCaseAsync();
        }

        public static Task<IPrewarmDynamicCodeUseCase> GetPrewarmDynamicCodeUseCaseAsync()
        {
            return GetRegistry().GetPrewarmDynamicCodeUseCaseAsync();
        }

        public static void ResetServerScopedServices()
        {
            GetRegistry().ResetServerScopedServices();
        }

        internal static Task AwaitDrainTaskAsync(Task drainTask)
        {
            return GetRegistry().AwaitDrainTaskAsync(drainTask);
        }

        internal static TimeSpan SwapDrainTimeoutForTests(TimeSpan timeout)
        {
            return GetRegistry().SwapDrainTimeoutForTests(timeout);
        }

        internal static void SetServerScopedServicesForTests(
            CancellationTokenSource lifetimeCancellationTokenSource,
            IDynamicCodeExecutorPool executorPool,
            IDynamicCodeExecutionRuntime runtimeFacade,
            IPrewarmDynamicCodeUseCase prewarmDynamicCodeUseCase)
        {
            GetRegistry().SetServerScopedServicesForTests(
                lifetimeCancellationTokenSource,
                executorPool,
                runtimeFacade,
                prewarmDynamicCodeUseCase);
        }

        internal static Task GetServerScopedDrainTaskForTests()
        {
            return GetRegistry().GetServerScopedDrainTaskForTests();
        }

        internal static void ResetStateForTests()
        {
            GetRegistry().ResetStateForTests();
        }

        internal static IDynamicCompilationServiceFactory SwapCompilationFactoryForTests(
            IDynamicCompilationServiceFactory factory)
        {
            return GetRegistry().SwapCompilationFactoryForTests(factory);
        }
    }
}
