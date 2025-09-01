namespace io.github.hatayama.uLoopMCP.Factory
{
    /// <summary>
    /// Factory for creating DynamicCodeExecutor instances
    /// Related classes: DynamicCodeExecutor, DynamicCodeExecutorStub, RoslynCompiler, SecurityValidator, CommandRunner
    /// </summary>
    public static class DynamicCodeExecutorFactory
    {
        /// <summary>
        /// Create DynamicCodeExecutor with specified security level
        /// Returns Stub implementation when Roslyn is disabled
        /// </summary>
        public static IDynamicCodeExecutor Create(DynamicCodeSecurityLevel securityLevel)
        {
#if ULOOPMCP_HAS_ROSLYN
            string correlationId = McpConstants.GenerateCorrelationId();

            try
            {
                // Initialize compiler (with explicit security level specification)
                RoslynCompiler compiler = new(securityLevel);

                // Initialize security validator (with explicit security level specification)
                SecurityValidator validator = new(securityLevel);

                // Initialize command runner
                CommandRunner runner = new();

                // Create integrated executor
                DynamicCodeExecutor executor = new(compiler, validator, securityLevel, runner);

                VibeLogger.LogInfo(
                    "dynamic_executor_created",
                    $"DynamicCodeExecutor created with security level: {securityLevel}",
                    new
                    {
                        security_level = securityLevel.ToString(),
                        compiler_type = compiler.GetType().Name,
                        validator_type = validator.GetType().Name,
                        runner_type = runner.GetType().Name
                    },
                    correlationId,
                    "Dynamic code execution system initialization completed",
                    "Ready for execution"
                );

                return executor;
            }
            catch (System.Exception ex)
            {
                VibeLogger.LogError(
                    "dynamic_executor_creation_failed",
                    "Failed to create DynamicCodeExecutor",
                    new
                    {
                        error_type = ex.GetType().Name,
                        error_message = ex.Message
                    },
                    correlationId,
                    "Dynamic code execution system initialization failed",
                    "Investigate dependency issues"
                );

                throw;
            }
#else
            // Return Stub implementation when Roslyn is disabled
            VibeLogger.LogInfo(
                "dynamic_executor_stub_created",
                "DynamicCodeExecutorStub created (Roslyn disabled)",
                new { },
                McpConstants.GenerateCorrelationId(),
                "Using Stub implementation due to Roslyn being disabled",
                "Dynamic code execution is not available"
            );
            
            return new DynamicCodeExecutorStub();
#endif
        }
    }
}