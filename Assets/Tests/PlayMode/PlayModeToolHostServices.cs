using System;
using io.github.hatayama.UnityCliLoop;

namespace Tests.PlayMode
{
    /// <summary>
    /// Supplies real application use cases to first-party tools exercised directly by PlayMode tests.
    /// </summary>
    internal sealed class PlayModeToolHostServices : IUnityCliLoopToolHostServices
    {
        public IUnityCliLoopConsoleLogService ConsoleLogs => throw new NotSupportedException();
        public IUnityCliLoopConsoleClearService ConsoleClear => throw new NotSupportedException();
        public IUnityCliLoopCompilationService Compilation => throw new NotSupportedException();
        public IUnityCliLoopDynamicCodeExecutionService DynamicCodeExecution => throw new NotSupportedException();
        public IUnityCliLoopHierarchyService Hierarchy => throw new NotSupportedException();
        public IUnityCliLoopTestExecutionService TestExecution => throw new NotSupportedException();
        public IUnityCliLoopGameObjectSearchService GameObjectSearch => throw new NotSupportedException();
        public IUnityCliLoopScreenshotService Screenshot => throw new NotSupportedException();
        public IUnityCliLoopRecordInputService RecordInput => throw new NotSupportedException();
        public IUnityCliLoopReplayInputService ReplayInput => throw new NotSupportedException();
        public IUnityCliLoopKeyboardSimulationService KeyboardSimulation { get; } = new SimulateKeyboardUseCase();
        public IUnityCliLoopMouseInputSimulationService MouseInputSimulation { get; } = new SimulateMouseInputUseCase();
        public IUnityCliLoopMouseUiSimulationService MouseUiSimulation { get; } = new SimulateMouseUiUseCase();
    }
}
