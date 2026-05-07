namespace io.github.hatayama.UnityCliLoop
{
    /// <summary>
    /// Composition-root implementation of host capabilities exposed to tool plugins.
    /// </summary>
    internal sealed class UnityCliLoopToolHostServices : IUnityCliLoopToolHostServices
    {
        public IUnityCliLoopConsoleLogService ConsoleLogs { get; }
        public IUnityCliLoopConsoleClearService ConsoleClear { get; }
        public IUnityCliLoopCompilationService Compilation { get; }
        public IUnityCliLoopDynamicCodeExecutionService DynamicCodeExecution { get; }
        public IUnityCliLoopHierarchyService Hierarchy { get; }
        public IUnityCliLoopTestExecutionService TestExecution { get; }
        public IUnityCliLoopGameObjectSearchService GameObjectSearch { get; }
        public IUnityCliLoopScreenshotService Screenshot { get; }
        public IUnityCliLoopRecordInputService RecordInput { get; }
        public IUnityCliLoopReplayInputService ReplayInput { get; }
        public IUnityCliLoopKeyboardSimulationService KeyboardSimulation { get; }
        public IUnityCliLoopMouseInputSimulationService MouseInputSimulation { get; }
        public IUnityCliLoopMouseUiSimulationService MouseUiSimulation { get; }

        public UnityCliLoopToolHostServices(DynamicCodeServicesRegistry dynamicCodeServices)
        {
            UnityEngine.Debug.Assert(dynamicCodeServices != null, "dynamicCodeServices must not be null");

            ConsoleLogs = new LogRetrievalService();
            ConsoleClear = new ConsoleClearService();
            Compilation = new CompileUseCase();
            DynamicCodeExecution = new UnityCliLoopDynamicCodeExecutionHostService(dynamicCodeServices);
            Hierarchy = new GetHierarchyUseCase(new HierarchyService(), new HierarchySerializer());
            TestExecution = new RunTestsUseCase();
            GameObjectSearch = new FindGameObjectsUseCase(new GameObjectFinderService(), new ComponentSerializer());
            Screenshot = new ScreenshotUseCase();
            RecordInput = new RecordInputUseCase();
            ReplayInput = new ReplayInputUseCase();
            KeyboardSimulation = new SimulateKeyboardUseCase();
            MouseInputSimulation = new SimulateMouseInputUseCase();
            MouseUiSimulation = new SimulateMouseUiUseCase();
        }
    }
}
