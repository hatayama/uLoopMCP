namespace io.github.hatayama.UnityCliLoop
{
    /// <summary>
    /// Application-owned implementation of host capabilities exposed to tool plugins.
    /// </summary>
    internal sealed class UnityCliLoopToolHostServices : IUnityCliLoopToolHostServices
    {
        public IUnityCliLoopConsoleLogService ConsoleLogs { get; }
        public IUnityCliLoopCompilationService Compilation { get; }
        public IUnityCliLoopDynamicCodeExecutionService DynamicCodeExecution { get; }

        public UnityCliLoopToolHostServices()
        {
            ConsoleLogs = new LogRetrievalService();
            Compilation = new CompileUseCase();
            DynamicCodeExecution = new UnityCliLoopDynamicCodeExecutionHostService();
        }
    }
}
