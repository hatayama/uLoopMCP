namespace io.github.hatayama.UnityCliLoop
{
    // Keeps bundled tool initialization inside the bundled-tool assembly.
    internal static class FirstPartyToolsEditorStartup
    {
        internal static void Initialize()
        {
            AssemblyTypeIndex.InvalidateForEditorStartup();
            DynamicReferenceSetBuilder.InvalidateReferenceCacheForEditorStartup();
            SharedRoslynCompilerWorkerHost.RegisterLifecycleForEditorStartup();
            LogGetter.InitializeForEditorStartup();
#if ULOOP_HAS_INPUT_SYSTEM
            InputRecorder.InitializeForEditorStartup();
            InputReplayer.InitializeForEditorStartup();
            KeyboardKeyState.InitializeForEditorStartup();
            MouseInputState.InitializeForEditorStartup();
            MouseDragState.InitializeForEditorStartup();
#endif
        }
    }
}
