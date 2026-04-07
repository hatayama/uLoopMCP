using io.github.hatayama.uLoopMCP.Factory;

namespace io.github.hatayama.uLoopMCP
{
    internal static class DynamicCodeServices
    {
        public static DynamicCodeSourcePreparationService SourcePreparationService { get; } =
            new DynamicCodeSourcePreparationService();

        public static ExternalCompilerPathResolutionService ExternalCompilerPathResolver { get; } =
            new ExternalCompilerPathResolutionService();

        public static DynamicReferenceSetBuilderService ReferenceSetBuilder { get; } =
            new DynamicReferenceSetBuilderService();

        public static CompiledAssemblyLoadService AssemblyLoadService { get; } =
            new CompiledAssemblyLoadService();

        public static DynamicCompilationBackend CompilationBackend { get; } =
            new DynamicCompilationBackend();

        public static CompiledCommandEntryPointResolver CommandEntryPointResolver { get; } =
            new CompiledCommandEntryPointResolver();

        public static RegistryDynamicCodeExecutorFactory ExecutorFactory { get; } =
            new RegistryDynamicCodeExecutorFactory(
                SourcePreparationService,
                CommandEntryPointResolver);

        public static DynamicCodeExecutionFacade ExecutionFacade { get; } =
            new DynamicCodeExecutionFacade(
                ExternalCompilerPathResolver,
                ExecutorFactory);

        public static DynamicCodePrewarmService PrewarmService { get; } =
            new DynamicCodePrewarmService(
                ExecutionFacade);
    }
}
