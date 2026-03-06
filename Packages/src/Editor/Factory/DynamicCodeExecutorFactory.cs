namespace io.github.hatayama.uLoopMCP.Factory
{
    /// <summary>
    /// Factory for creating DynamicCodeExecutor instances
    /// Related classes: DynamicCodeExecutor, DynamicCodeExecutorStub, CommandRunner
    /// </summary>
    public static class DynamicCodeExecutorFactory
    {
        /// <summary>
        /// Returns a stub when the compilation provider is not registered.
        /// The main editor assembly must stay independent from Roslyn implementation details.
        /// </summary>
        public static IDynamicCodeExecutor Create(DynamicCodeSecurityLevel securityLevel)
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
                    "Verify Roslyn assembly loading and define configuration"
                );

                return new DynamicCodeExecutorStub();
            }

            CommandRunner runner = new CommandRunner();
            DynamicCodeExecutor executor = new DynamicCodeExecutor(compiler, securityLevel, runner);

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
                "Ready for execution"
            );

            return executor;
        }
    }
}
