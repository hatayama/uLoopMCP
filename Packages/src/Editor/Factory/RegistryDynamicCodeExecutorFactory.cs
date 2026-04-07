namespace io.github.hatayama.uLoopMCP.Factory
{
    internal sealed class RegistryDynamicCodeExecutorFactory
    {
        private readonly DynamicCodeSourcePreparationService _sourcePreparationService;
        private readonly CompiledCommandEntryPointResolver _entryPointResolver;

        public RegistryDynamicCodeExecutorFactory(
            DynamicCodeSourcePreparationService sourcePreparationService,
            CompiledCommandEntryPointResolver entryPointResolver)
        {
            _sourcePreparationService = sourcePreparationService;
            _entryPointResolver = entryPointResolver;
        }

        public IDynamicCodeExecutor Create(DynamicCodeSecurityLevel securityLevel)
        {
            string correlationId = McpConstants.GenerateCorrelationId();
            IDynamicCompilationService compiler;
            if (!DynamicCompilationServiceRegistry.TryCreate(securityLevel, out compiler))
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

            CommandRunner runner = new CommandRunner(_entryPointResolver);
            DynamicCodeExecutor executor = new DynamicCodeExecutor(
                compiler,
                runner,
                _sourcePreparationService);

            VibeLogger.LogInfo(
                "dynamic_executor_created",
                $"DynamicCodeExecutor created with security level: {securityLevel}",
                new
                {
                    security_level = securityLevel.ToString(),
                    compiler_type = compiler.GetType().Name,
                    runner_type = runner.GetType().Name
                },
                correlationId,
                "Dynamic code execution system initialization completed",
                "Ready for execution");

            return executor;
        }
    }
}
