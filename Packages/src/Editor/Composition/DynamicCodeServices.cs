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

        public static IDynamicCompilationPlanner CompilationPlanner { get; } =
            new DynamicCompilationPlanner(SourcePreparationService);

        public static ICompiledAssemblyLoader AssemblyLoadService { get; } =
            new CompiledAssemblyLoadService();

        public static DynamicCompilationBackend CompilationBackend { get; } =
            new DynamicCompilationBackend();

        public static ICompiledAssemblyBuilder AssemblyBuilder { get; } =
            new CompiledAssemblyBuilder(
                ExternalCompilerPathResolver,
                ReferenceSetBuilder,
                CompilationBackend);

        public static CompiledCommandEntryPointResolver CommandEntryPointResolver { get; } =
            new CompiledCommandEntryPointResolver();

        public static RegistryDynamicCodeExecutorFactory ExecutorFactory { get; } =
            new RegistryDynamicCodeExecutorFactory(
                SourcePreparationService,
                CommandEntryPointResolver);

        public static IDynamicCodeExecutorPool ExecutorPool { get; } =
            new DynamicCodeExecutorPool(ExecutorFactory);

        public static IDynamicCodeExecutionRuntime RuntimeFacade { get; } =
            new DynamicCodeExecutionFacade(
                AssemblyBuilder,
                ExecutorPool);

        public static IExecuteDynamicCodeUseCase ExecuteDynamicCodeUseCase { get; } =
            new ExecuteDynamicCodeUseCase(RuntimeFacade);

        public static IPrewarmDynamicCodeUseCase PrewarmDynamicCodeUseCase { get; } =
            new PrewarmDynamicCodeUseCase(RuntimeFacade);
    }
}
