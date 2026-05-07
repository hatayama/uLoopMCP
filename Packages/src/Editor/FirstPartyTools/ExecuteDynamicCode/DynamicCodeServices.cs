using System;
using io.github.hatayama.UnityCliLoop.FirstPartyTools.Factory;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Keeps the registered Dynamic Code Services entries for lookup by the owning module.
    /// </summary>
    internal sealed class DynamicCodeServicesRegistry
    {
        private const int StartupPrewarmDelayFrameCount = 1;
        private readonly object _serverScopedServicesLock = new();
        private IDynamicCodeExecutionRuntime _runtimeFacade;

        private readonly Lazy<IDynamicCodeSourcePreparationService> _sourcePreparationServiceValue;

        internal IDynamicCodeSourcePreparationService SourcePreparationService => _sourcePreparationServiceValue.Value;

        internal CompiledCommandEntryPointResolver CommandEntryPointResolver { get; } =
            new CompiledCommandEntryPointResolver();

        private readonly Lazy<RegistryDynamicCodeExecutorFactory> _executorFactoryValue;
        private readonly Lazy<DynamicCodeStartupPrewarmer> _startupPrewarmerValue;

        internal DynamicCodeServicesRegistry()
        {
            _sourcePreparationServiceValue = new Lazy<IDynamicCodeSourcePreparationService>(
                () => new DynamicCodeSourcePreparationService());
            _executorFactoryValue = new Lazy<RegistryDynamicCodeExecutorFactory>(
                () => new RegistryDynamicCodeExecutorFactory(
                    new DynamicCodeCompilationServiceFactory(),
                    SourcePreparationService,
                    CommandEntryPointResolver));
            _startupPrewarmerValue = new Lazy<DynamicCodeStartupPrewarmer>(
                () => new DynamicCodeStartupPrewarmer(
                    GetRuntimeFacade(),
                    StartupPrewarmDelayFrameCount));
        }

        internal RegistryDynamicCodeExecutorFactory ExecutorFactory => _executorFactoryValue.Value;

        internal IExecuteDynamicCodeUseCase GetExecuteDynamicCodeUseCase()
        {
            IDynamicCodeExecutionRuntime runtimeFacade = GetRuntimeFacade();
            return new ExecuteDynamicCodeUseCase(runtimeFacade);
        }

        internal void RequestStartupPrewarm()
        {
            _startupPrewarmerValue.Value.Request();
        }

        private IDynamicCodeExecutionRuntime GetRuntimeFacade()
        {
            lock (_serverScopedServicesLock)
            {
                if (_runtimeFacade == null)
                {
                    IDynamicCodeExecutorPool executorPool = new DynamicCodeExecutorPool(ExecutorFactory);
                    _runtimeFacade = new DynamicCodeExecutionFacade(executorPool);
                }

                return _runtimeFacade;
            }
        }
    }

    /// <summary>
    /// Provides Dynamic Code Services behavior for Unity CLI Loop.
    /// </summary>
    internal static class DynamicCodeServices
    {
        private static readonly DynamicCodeServicesRegistry RegistryValue = new DynamicCodeServicesRegistry();

        internal static DynamicCodeServicesRegistry GetRegistry()
        {
            return RegistryValue;
        }

        internal static IDynamicCodeSourcePreparationService SourcePreparationService
        {
            get { return GetRegistry().SourcePreparationService; }
        }

        internal static CompiledCommandEntryPointResolver CommandEntryPointResolver
        {
            get { return GetRegistry().CommandEntryPointResolver; }
        }

        internal static RegistryDynamicCodeExecutorFactory ExecutorFactory
        {
            get { return GetRegistry().ExecutorFactory; }
        }

        internal static IExecuteDynamicCodeUseCase GetExecuteDynamicCodeUseCase()
        {
            return GetRegistry().GetExecuteDynamicCodeUseCase();
        }

        internal static void RequestStartupPrewarm()
        {
            GetRegistry().RequestStartupPrewarm();
        }
    }
}
