namespace io.github.hatayama.UnityCliLoop.Factory
{
    internal sealed class RegistryDynamicCodeExecutorFactory : IDynamicCodeExecutorProvider
    {
        private readonly DynamicCompilationServiceRegistryService _compilationServiceRegistry;
        private readonly IDynamicCodeSourcePreparationService _sourcePreparationService;
        private readonly CompiledCommandEntryPointResolver _entryPointResolver;

        public RegistryDynamicCodeExecutorFactory(
            DynamicCompilationServiceRegistryService compilationServiceRegistry,
            IDynamicCodeSourcePreparationService sourcePreparationService,
            CompiledCommandEntryPointResolver entryPointResolver)
        {
            _compilationServiceRegistry = compilationServiceRegistry;
            _sourcePreparationService = sourcePreparationService;
            _entryPointResolver = entryPointResolver;
        }

        public IDynamicCodeExecutor Create(DynamicCodeSecurityLevel securityLevel)
        {
            string correlationId = UnityCliLoopConstants.GenerateCorrelationId();
            IDynamicCompilationService compiler;
            if (!_compilationServiceRegistry.TryCreate(securityLevel, out compiler))
            {
                VibeLogger.LogWarning(
                    "dynamic_executor_stub_created",
                    "DynamicCodeExecutorStub created (compilation provider unavailable)",
                    new
                    {
                        security_level = securityLevel.ToString()
                    },
                    correlationId,
                    "Dynamic code execution provider was not registered",
                    "Verify Roslyn assembly loading and define configuration");

                return new DynamicCodeExecutorStub();
            }

            ICompiledCommandInvoker invoker = new CommandRunner(_entryPointResolver);
            DynamicCodeExecutor executor = new DynamicCodeExecutor(
                compiler,
                invoker,
                _sourcePreparationService);

            VibeLogger.LogInfo(
                "dynamic_executor_created",
                $"DynamicCodeExecutor created with security level: {securityLevel}",
                new
                {
                    security_level = securityLevel.ToString(),
                    compiler_type = compiler.GetType().Name,
                    runner_type = invoker.GetType().Name
                },
                correlationId,
                "Dynamic code execution system initialization completed",
                "Ready for execution");

            return executor;
        }
    }
}
